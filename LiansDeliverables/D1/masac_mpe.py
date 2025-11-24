#!/usr/bin/env python3
"""
MASAC on PettingZoo MPE simple_spread_v3 (parallel API, discrete actions).

- Per-agent stochastic discrete policy (softmax).
- Centralized critics per agent that take joint state + joint actions (one-hot).
- Target critics, polyak updates.
- Shared automatic entropy temperature (alpha) with tuning.
- Replay buffer stores joint observations/state/actions.
- Designed for small discrete action spaces (exact expectation over actions).
"""

import os
import copy
import random
from dataclasses import dataclass
from typing import Dict, List, Tuple, Any

import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
from tqdm import trange
from pettingzoo.mpe import simple_spread_v3

# ----------------------- Config / Device -----------------------
DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")
os.makedirs("checkpoints", exist_ok=True)
torch.manual_seed(0)
np.random.seed(0)
random.seed(0)


# ----------------------- Helpers -----------------------
def unwrap_reset(x):
    """PettingZoo parallel reset may return (obs, infos) or just obs. We only need obs."""
    return x[0] if isinstance(x, tuple) else x


def flatten_obs(o):
    """Return a 1D float32 array from nested dicts/lists/arrays (handles PettingZoo dict obs)."""
    if isinstance(o, dict):
        if 'observation' in o:  # common PettingZoo structure
            return np.asarray(o['observation'], dtype=np.float32).ravel()
        parts = [flatten_obs(v) for v in o.values()]
        if not parts:
            return np.empty((0,), dtype=np.float32)
        return np.concatenate(parts).astype(np.float32)
    return np.asarray(o, dtype=np.float32).ravel()


def dict_obs_to_matrix(obs_dict: Dict[str, Any], agent_ids: List[str]) -> np.ndarray:
    """Stack per-agent observations into [n_agents, obs_dim], padding if dims differ."""
    mats = [flatten_obs(obs_dict[a]) for a in agent_ids]
    obs_dim = max(m.shape[0] for m in mats)
    padded = []
    for m in mats:
        if m.shape[0] != obs_dim:
            pad = np.zeros((obs_dim - m.shape[0],), dtype=np.float32)
            m = np.concatenate([m, pad]).astype(np.float32)
        padded.append(m)
    return np.stack(padded, axis=0)  # [n_agents, obs_dim]


def dict_actions_to_vec(act_dict: Dict[str, int], agent_ids: List[str]) -> np.ndarray:
    return np.array([int(act_dict[a]) for a in agent_ids], dtype=np.int64)


def one_hot_actions(actions: torch.LongTensor, n_agents: int, act_dim: int) -> torch.FloatTensor:
    # actions: [B, n_agents] long
    B = actions.shape[0]
    oh = torch.zeros((B, n_agents * act_dim), device=actions.device)
    for i in range(n_agents):
        idx = actions[:, i]
        oh.scatter_(1, (i * act_dim + idx).unsqueeze(1), 1.0)
    return oh  # [B, n_agents * act_dim]


# ----------------------- Replay Buffer -----------------------
@dataclass
class Transition:
    obs: np.ndarray        # [n_agents, obs_dim]
    state: np.ndarray      # [state_dim]
    actions: np.ndarray    # [n_agents] int
    reward: float          # scalar team reward
    next_obs: np.ndarray   # [n_agents, obs_dim]
    next_state: np.ndarray # [state_dim]
    done: float            # 0.0/1.0


class ReplayBuffer:
    def __init__(self, capacity: int):
        self.capacity = capacity
        self.buffer: List[Transition] = []
        self.idx = 0

    def push(self, tr: Transition):
        if len(self.buffer) < self.capacity:
            self.buffer.append(tr)
        else:
            self.buffer[self.idx] = tr
        self.idx = (self.idx + 1) % self.capacity

    def sample(self, batch_size: int) -> List[Transition]:
        return random.sample(self.buffer, batch_size)

    def __len__(self):
        return len(self.buffer)


# ----------------------- Networks -----------------------
class ActorNet(nn.Module):
    """Per-agent discrete policy: outputs logits for actions given local observation."""
    def __init__(self, obs_dim: int, act_dim: int, hidden: int = 128):
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(obs_dim, hidden),
            nn.ReLU(),
            nn.Linear(hidden, hidden),
            nn.ReLU(),
            nn.Linear(hidden, act_dim),
        )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        # returns logits [B, act_dim]
        return self.net(x)


class CriticNet(nn.Module):
    """Centralized critic: takes global state (concat per-agent obs) and joint actions (one-hot concat)."""
    def __init__(self, state_dim: int, joint_act_dim: int, hidden: int = 256):
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(state_dim + joint_act_dim, hidden),
            nn.ReLU(),
            nn.Linear(hidden, hidden),
            nn.ReLU(),
            nn.Linear(hidden, 1)
        )

    def forward(self, state: torch.Tensor, joint_actions_onehot: torch.Tensor) -> torch.Tensor:
        # state: [B, state_dim], joint_actions_onehot: [B, joint_act_dim]
        x = torch.cat([state, joint_actions_onehot], dim=1)
        return self.net(x).squeeze(1)  # [B]


# ----------------------- MASAC (multi-agent SAC) -----------------------
class MASAC:
    def __init__(self,
                 agent_ids: List[str],
                 obs_dim: int,
                 act_dim: int,
                 state_dim: int,
                 lr: float = 3e-4,
                 gamma: float = 0.99,
                 tau: float = 0.005,
                 target_entropy: float = None):
        self.agent_ids = list(agent_ids)
        self.n_agents = len(self.agent_ids)
        self.obs_dim = obs_dim
        self.act_dim = act_dim
        self.state_dim = state_dim
        self.joint_act_dim = self.n_agents * self.act_dim

        # Actors (decentralized)
        self.actors = nn.ModuleList([ActorNet(obs_dim, act_dim).to(DEVICE) for _ in range(self.n_agents)])
        # Critics (centralized) - two Q networks per agent (double Q)
        self.critics1 = nn.ModuleList([CriticNet(state_dim, self.joint_act_dim).to(DEVICE) for _ in range(self.n_agents)])
        self.critics2 = nn.ModuleList([CriticNet(state_dim, self.joint_act_dim).to(DEVICE) for _ in range(self.n_agents)])
        # Target critics
        self.target_critics1 = copy.deepcopy(self.critics1)
        self.target_critics2 = copy.deepcopy(self.critics2)
        for p in sum([list(tc.parameters()) for tc in self.target_critics1 + self.target_critics2], []):
            p.requires_grad = False

        # Optimizers
        actor_params = sum([list(a.parameters()) for a in self.actors], [])
        critic_params = sum([list(c.parameters()) for c in self.critics1 + self.critics2], [])
        self.actor_opt = optim.Adam(actor_params, lr=lr)
        self.critic_opt = optim.Adam(critic_params, lr=lr)

        # Entropy temperature (shared)
        if target_entropy is None:
            # heuristic: -log(|A|) per agent times n_agents -> scale down to per-joint? simpler: -act_dim
            target_entropy = -float(self.act_dim) * 0.98
        self.target_entropy = target_entropy
        self.log_alpha = torch.tensor(0.0, requires_grad=True, device=DEVICE)
        self.alpha_opt = optim.Adam([self.log_alpha], lr=lr)

        # Hyperparams
        self.gamma = gamma
        self.tau = tau

        # Initialize targets
        self.update_targets(tau=1.0)

    @property
    def alpha(self):
        return self.log_alpha.exp()

    @torch.no_grad()
    def update_targets(self, tau: float = None):
        if tau is None:
            tau = self.tau
        for c, tc in zip(self.critics1, self.target_critics1):
            for p, tp in zip(c.parameters(), tc.parameters()):
                tp.data.copy_(tau * p.data + (1.0 - tau) * tp.data)
        for c, tc in zip(self.critics2, self.target_critics2):
            for p, tp in zip(c.parameters(), tc.parameters()):
                tp.data.copy_(tau * p.data + (1.0 - tau) * tp.data)

    def select_actions(self, obs_dict: Dict[str, Any], deterministic: bool = False) -> Dict[str, int]:
        """
        obs_dict: mapping agent_id -> observation (raw obs)
        deterministic: if True pick argmax, else sample categorical from policy
        """
        actions: Dict[str, int] = {}
        for i, aid in enumerate(self.agent_ids):
            o = flatten_obs(obs_dict[aid])
            o_t = torch.from_numpy(o).unsqueeze(0).to(DEVICE)  # [1, obs_dim]
            logits = self.actors[i](o_t)  # [1, act_dim]
            probs = torch.softmax(logits, dim=1)
            if deterministic:
                a = int(torch.argmax(probs, dim=1).item())
            else:
                a = int(torch.multinomial(probs, num_samples=1).item())
            actions[aid] = a
        return actions

    def train_step(self, batch: List[Transition]) -> Tuple[float, float, float]:
        """
        Single gradient step for critics, actors, and alpha on a sampled batch.
        Returns (critic_loss, actor_loss, alpha_loss) scalar floats.
        """
        # Stack batch
        obs = torch.from_numpy(np.stack([t.obs for t in batch], axis=0)).float().to(DEVICE)            # [B, n, obs]
        next_obs = torch.from_numpy(np.stack([t.next_obs for t in batch], axis=0)).float().to(DEVICE)  # [B, n, obs]
        state = torch.from_numpy(np.stack([t.state for t in batch], axis=0)).float().to(DEVICE)        # [B, s]
        next_state = torch.from_numpy(np.stack([t.next_state for t in batch], axis=0)).float().to(DEVICE)
        actions = torch.from_numpy(np.stack([t.actions for t in batch], axis=0)).long().to(DEVICE)     # [B, n]
        rewards = torch.from_numpy(np.array([t.reward for t in batch], dtype=np.float32)).unsqueeze(1).to(DEVICE)  # [B,1]
        dones = torch.from_numpy(np.array([t.done for t in batch], dtype=np.float32)).unsqueeze(1).to(DEVICE)      # [B,1]

        B, n, _ = obs.shape
        assert n == self.n_agents, "Batch agent count mismatch"

        # ---------- Critic update ----------
        # Compute current joint actions one-hot
        joint_actions_oh = one_hot_actions(actions, self.n_agents, self.act_dim)  # [B, joint_act_dim]

        # Current Q estimates per agent (two critics)
        q1_vals = []
        q2_vals = []
        for i in range(self.n_agents):
            q1 = self.critics1[i](state, joint_actions_oh)  # [B]
            q2 = self.critics2[i](state, joint_actions_oh)
            q1_vals.append(q1)
            q2_vals.append(q2)
        q1_stack = torch.stack(q1_vals, dim=1)  # [B, n]
        q2_stack = torch.stack(q2_vals, dim=1)  # [B, n]

        # Compute target values:
        # For next state, compute actor probs for each agent and compute expected joint Q under joint-action distribution.
        # Assuming independence: joint prob = prod_i pi_i(a_i). We'll compute expectation by summing over all joint actions.
        with torch.no_grad():
            # Per-agent next action probs and log probs: lists of [B, act_dim]
            next_action_probs = []
            next_action_logp = []
            for i in range(self.n_agents):
                logits_next = self.actors[i](next_obs[:, i, :])  # [B, act_dim]
                probs = torch.softmax(logits_next, dim=1)
                logp = torch.log_softmax(logits_next, dim=1)
                next_action_probs.append(probs)      # [B, act_dim]
                next_action_logp.append(logp)        # [B, act_dim]

            # We need expected value of min(Q1_target, Q2_target) - alpha * sum(log pi) under joint action distribution
            # For small action spaces, sum across joint combos is feasible: (act_dim**n_agents). This can blow up quickly.
            # For moderate n_agents (3) and small act_dim (5-10), it is acceptable. We'll compute via outer product approach.
            # Build per-agent axis lists and compute joint probabilities and joint one-hot actions.
            # We'll iterate over all joint-action combinations (cartesian product)
            all_joint_indices = np.array(np.meshgrid(*([np.arange(self.act_dim)] * self.n_agents), indexing='ij')).reshape(self.n_agents, -1).T
            # all_joint_indices: [n_joint, n_agents] each row is one joint action tuple
            n_joint = all_joint_indices.shape[0]

            # Prepare storage
            target_vals_per_agent = torch.zeros((B, self.n_agents), device=DEVICE)  # [B, n_agents]

            # Compute joint probs [B, n_joint] by multiplying per-agent probs
            # For efficiency compute per-agent probs expanded:
            probs_list = [p.unsqueeze(2) for p in next_action_probs]  # each [B, act_dim, 1]
            # We'll compute joint probabilities by multiplying across agents for each joint index inside loop
            # Loop over joint combos (n_joint)
            # Precompute one-hot mapping for joint actions to feed target critic
            for joint_idx in range(n_joint):
                joint_actions = all_joint_indices[joint_idx]  # shape [n_agents]
                # joint prob per batch
                jp = torch.ones((B,), device=DEVICE)
                for ag_i, a_val in enumerate(joint_actions):
                    jp = jp * next_action_probs[ag_i][:, a_val]
                # joint log prob sum (sum of per-agent logp at chosen actions)
                jlogp = torch.zeros((B,), device=DEVICE)
                for ag_i, a_val in enumerate(joint_actions):
                    jlogp = jlogp + next_action_logp[ag_i][:, a_val]

                # build joint actions one-hot vector
                # create a [B, joint_act_dim] vector with the one-hot corresponding to this joint action
                # For repeated batches, we can tile the same one-hot row
                # Build index array for scatter
                joint_oh_idx = []
                for ag_i, a_val in enumerate(joint_actions):
                    joint_oh_idx.append(ag_i * self.act_dim + int(a_val))
                # Create the one-hot [joint_act_dim] row
                joint_oh_row = torch.zeros((self.joint_act_dim,), device=DEVICE)
                joint_oh_row[joint_oh_idx] = 1.0
                joint_oh = joint_oh_row.unsqueeze(0).repeat(B, 1)  # [B, joint_act_dim]

                # Evaluate target critics
                q1_t_vals = []
                q2_t_vals = []
                for i in range(self.n_agents):
                    q1_t = self.target_critics1[i](next_state, joint_oh)  # [B]
                    q2_t = self.target_critics2[i](next_state, joint_oh)
                    # take min for bias reduction
                    q_min = torch.min(q1_t, q2_t)  # [B]
                    q1_t_vals.append(q_min)

                q_min_stack = torch.stack(q1_t_vals, dim=1)  # [B, n_agents]
                # Expected target for each agent accumulates joint-prob-weighted terms
                # term = joint_prob * (q_min - alpha * joint_logprob)
                weighted = (q_min_stack - self.alpha.detach() * jlogp.unsqueeze(1)) * jp.unsqueeze(1)  # [B, n_agents]
                target_vals_per_agent += weighted  # accumulate
            # Now target_vals_per_agent is expectation over joint actions under pi_next
            # Compute y = r + (1 - done) * gamma * target_vals_sum? Note: For MASAC in team reward setup we treat each agent's critic learning to predict the team reward; our environment returns a team reward scalar r.
            # We will compute per-agent target as: y_agent = r + gamma * expected_target_agent
            # target_vals_per_agent: [B, n_agents]
            y_agents = rewards + (1.0 - dones) * self.gamma * target_vals_per_agent  # [B, n_agents]

        # Critic losses: MSE between current Q estimates and y_agents
        critic_loss = 0.0
        q1_list = []
        q2_list = []
        for i in range(self.n_agents):
            q1 = q1_stack[:, i]  # [B]
            q2 = q2_stack[:, i]
            y = y_agents[:, i]
            l1 = nn.MSELoss()(q1, y.detach())
            l2 = nn.MSELoss()(q2, y.detach())
            critic_loss = critic_loss + l1 + l2
            q1_list.append(q1)
            q2_list.append(q2)

        # Gradient step for critics
        self.critic_opt.zero_grad()
        critic_loss.backward()
        torch.nn.utils.clip_grad_norm_(sum([list(c.parameters()) for c in self.critics1 + self.critics2], []), 10.0)
        self.critic_opt.step()

        # ---------- Actor update ----------
        # For current state (obs), compute per-agent policy logits/probs and log-probs
        # Then compute actor loss: E_pi[ alpha * sum_i log pi_i(a_i) - Q_min(state, joint_a) ]
        # We compute expectation exactly by summing over joint actions (like above)
        B = obs.shape[0]
        action_probs = [None] * self.n_agents
        action_logps = [None] * self.n_agents
        for i in range(self.n_agents):
            logits = self.actors[i](obs[:, i, :])  # [B, act_dim]
            probs = torch.softmax(logits, dim=1)   # [B, act_dim]
            logp = torch.log_softmax(logits, dim=1)
            action_probs[i] = probs
            action_logps[i] = logp

        # compute expected value of (alpha * joint_logprob - Q_min) under current policy for current state
        all_joint_indices = np.array(np.meshgrid(*([np.arange(self.act_dim)] * self.n_agents), indexing='ij')).reshape(self.n_agents, -1).T
        n_joint = all_joint_indices.shape[0]

        policy_obj = torch.zeros((B,), device=DEVICE)  # scalar per batch (we'll average later)
        expected_entropy = torch.zeros((B,), device=DEVICE)
        for joint_idx in range(n_joint):
            joint_actions = all_joint_indices[joint_idx]  # [n_agents]
            # joint prob and joint logprob
            jp = torch.ones((B,), device=DEVICE)
            jlogp = torch.zeros((B,), device=DEVICE)
            for ag_i, a_val in enumerate(joint_actions):
                jp = jp * action_probs[ag_i][:, int(a_val)]
                jlogp = jlogp + action_logps[ag_i][:, int(a_val)]
            # joint one-hot
            joint_oh_row = torch.zeros((self.joint_act_dim,), device=DEVICE)
            for ag_i, a_val in enumerate(joint_actions):
                joint_oh_row[ag_i * self.act_dim + int(a_val)] = 1.0
            joint_oh = joint_oh_row.unsqueeze(0).repeat(B, 1)  # [B, joint_act_dim]

            # Q targets: use current critics (not target) to evaluate Q for policy gradient
            q1_vals = []
            q2_vals = []
            for i in range(self.n_agents):
                q1_v = self.critics1[i](state, joint_oh)  # [B]
                q2_v = self.critics2[i](state, joint_oh)
                q_min = torch.min(q1_v, q2_v)
                q1_vals.append(q_min)
            q_min_stack = torch.stack(q1_vals, dim=1)  # [B, n_agents]

            # For MASAC with team reward, the actor objective uses team Q (or sum of per-agent Qs). We'll use sum across agents.
            q_sum = q_min_stack.sum(dim=1)  # [B]
            # Contribution to expectation: joint_prob * (alpha * joint_logprob - q_sum)
            policy_obj = policy_obj + jp * (self.alpha.detach() * jlogp - q_sum)
            expected_entropy = expected_entropy + jp * (-jlogp)  # -logp inside entropy

        # actor_loss = mean over batch of (-policy_obj) because we want to minimize alpha*logp - Q -> equivalent to maximizing Q - alpha*logp
        actor_loss = torch.mean(-policy_obj)

        self.actor_opt.zero_grad()
        actor_loss.backward()
        torch.nn.utils.clip_grad_norm_(sum([list(a.parameters()) for a in self.actors], []), 10.0)
        self.actor_opt.step()

        # ---------- Alpha (temperature) update ----------
        # alpha loss: -E_pi[ log_alpha * (log_pi + target_entropy) ]
        # compute average log_pi across joint distribution: expected joint log prob under current policy
        avg_logpi = torch.zeros((B,), device=DEVICE)
        for joint_idx in range(n_joint):
            joint_actions = all_joint_indices[joint_idx]
            jp = torch.ones((B,), device=DEVICE)
            jlogp = torch.zeros((B,), device=DEVICE)
            for ag_i, a_val in enumerate(joint_actions):
                jp = jp * action_probs[ag_i][:, int(a_val)]
                jlogp = jlogp + action_logps[ag_i][:, int(a_val)]
            avg_logpi = avg_logpi + jp * jlogp
        # normalize by total probability (should be 1), but numeric safety:
        # we can average avg_logpi across batch
        alpha_loss = -(self.log_alpha * (avg_logpi + self.target_entropy).detach()).mean()

        self.alpha_opt.zero_grad()
        alpha_loss.backward()
        self.alpha_opt.step()

        # Update targets
        self.update_targets()

        return float(critic_loss.detach().cpu().item()), float(actor_loss.detach().cpu().item()), float(alpha_loss.detach().cpu().item())


# ----------------------- Env Factory -----------------------
def make_env(n_agents: int = 3, n_landmarks: int = 3, max_cycles: int = 25):
    env = simple_spread_v3.parallel_env(
        N=n_agents,
        local_ratio=0.5,
        max_cycles=max_cycles,
        continuous_actions=False
    )
    env.reset(seed=None)
    return env


# ----------------------- Training -----------------------
def train_masac(episodes: int = 2000,
                buffer_capacity: int = 50000,
                batch_size: int = 128,
                start_learn_after: int = 1000,
                gamma: float = 0.99,
                lr: float = 3e-4,
                target_tau: float = 0.005,
                max_cycles: int = 25) -> Tuple[List[float], List[float], List[float]]:
    env = make_env(max_cycles=max_cycles)
    agent_ids = list(env.possible_agents)
    assert len(agent_ids) > 0, "No agents found in the environment"

    # Reset + probe spaces
    obs0 = unwrap_reset(env.reset())
    obs_mat0 = dict_obs_to_matrix(obs0, agent_ids)
    obs_dim = obs_mat0.shape[1]
    act_dim = env.action_space(agent_ids[0]).n
    state_dim = len(agent_ids) * obs_dim

    masac = MASAC(agent_ids=agent_ids, obs_dim=obs_dim, act_dim=act_dim, state_dim=state_dim,
                  lr=lr, gamma=gamma, tau=target_tau)

    rb = ReplayBuffer(buffer_capacity)
    rewards_log: List[float] = []
    critic_loss_log: List[float] = []
    actor_loss_log: List[float] = []
    alpha_loss_log: List[float] = []

    for ep in trange(episodes, desc="Training MASAC"):
        obs = unwrap_reset(env.reset())
        ep_reward = 0.0

        for step in range(max_cycles):
            acts_dict = masac.select_actions(obs, deterministic=False)
            next_obs, rewards, terminations, truncations, infos = env.step(acts_dict)

            # Team reward (mean across agents) - keep same scalar reward semantics as original
            r = float(np.mean([rewards[a] for a in agent_ids]))

            done = any(terminations.values()) or all(truncations.values())

            obs_mat = dict_obs_to_matrix(obs, agent_ids)
            next_obs_mat = dict_obs_to_matrix(next_obs, agent_ids)
            state = obs_mat.reshape(-1)
            next_state = next_obs_mat.reshape(-1)
            acts_vec = dict_actions_to_vec(acts_dict, agent_ids)

            rb.push(Transition(
                obs=obs_mat,
                state=state,
                actions=acts_vec,
                reward=r,
                next_obs=next_obs_mat,
                next_state=next_state,
                done=float(done)
            ))

            obs = next_obs
            ep_reward += r

            # Learn
            if len(rb) >= max(batch_size, start_learn_after):
                batch = rb.sample(batch_size)
                c_loss, a_loss, al_loss = masac.train_step(batch)
                critic_loss_log.append(c_loss)
                actor_loss_log.append(a_loss)
                alpha_loss_log.append(al_loss)

            if done:
                break

        rewards_log.append(ep_reward)

        # Save occasionally
        if (ep + 1) % 500 == 0:
            torch.save({
                'actors': [a.state_dict() for a in masac.actors],
                'critics1': [c.state_dict() for c in masac.critics1],
                'critics2': [c.state_dict() for c in masac.critics2],
                'obs_dim': obs_dim,
                'act_dim': act_dim,
                'state_dim': state_dim,
                'n_agents': len(agent_ids),
                'agent_ids': agent_ids,
                'log_alpha': masac.log_alpha.detach().cpu().numpy(),
            }, f"checkpoints/masac_ep{ep+1}.pt")

    env.close()
    return rewards_log, critic_loss_log, actor_loss_log


# ----------------------- Evaluation -----------------------
def evaluate_masac(ckpt_path: str, episodes: int = 5, max_cycles: int = 25) -> float:
    data = torch.load(ckpt_path, map_location=DEVICE)
    agent_ids = data['agent_ids']
    n_agents = data['n_agents']
    obs_dim = data['obs_dim']
    act_dim = data['act_dim']
    state_dim = data['state_dim']

    masac = MASAC(agent_ids=agent_ids, obs_dim=obs_dim, act_dim=act_dim, state_dim=state_dim)
    for a, sd in zip(masac.actors, data['actors']):
        a.load_state_dict(sd)
    for c1, sd in zip(masac.critics1, data['critics1']):
        c1.load_state_dict(sd)
    for c2, sd in zip(masac.critics2, data['critics2']):
        c2.load_state_dict(sd)
    masac.log_alpha.data = torch.tensor(data.get('log_alpha', 0.0), device=DEVICE)

    env = make_env(max_cycles=max_cycles)
    total = 0.0
    for _ in range(episodes):
        obs = unwrap_reset(env.reset())
        ep_r = 0.0
        for _ in range(max_cycles):
            acts = masac.select_actions(obs, deterministic=True)
            obs, rewards, terms, truncs, infos = env.step(acts)
            r = float(np.mean([rewards[a] for a in agent_ids]))
            ep_r += r
            if any(terms.values()) or all(truncs.values()):
                break
        total += ep_r
    env.close()
    avg = total / episodes
    print(f"Average eval reward over {episodes} episodes: {avg:.3f}")
    return avg


# ----------------------- Main -----------------------
if __name__ == "__main__":
    import matplotlib.pyplot as plt

    # Short smoke test run
    rewards, critic_losses, actor_losses = train_masac(episodes=200, batch_size=128, start_learn_after=1000)

    # Plotting
    plt.figure(figsize=(12,5))
    plt.subplot(1,3,1)
    plt.plot(rewards)
    plt.title('Episode Reward')
    plt.subplot(1,3,2)
    plt.plot(critic_losses[:len(rewards)])
    plt.title('Critic Loss (sampled)')
    plt.subplot(1,3,3)
    plt.plot(actor_losses[:len(rewards)])
    plt.title('Actor Loss (sampled)')
    plt.tight_layout()
    out_png = "checkpoints/masac_training_curves.png"
    plt.savefig(out_png)
    print(f"Saved training curves to {out_png}")
