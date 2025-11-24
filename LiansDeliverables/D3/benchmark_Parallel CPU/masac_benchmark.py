#!/usr/bin/env python3
"""
MASAC on Robotic Warehouse (RWARE) environment with CLI support.

V37 FIX (Manual Vector Fix):
    The gymnasium.vector.SyncVectorEnv is fundamentally broken on this
    system for RWARE. It returns corrupted data from env.step().
    
    This version ABANDONS SyncVectorEnv and implements a
    "manual pseudo-vector environment" using a simple Python list
    of 8 standard envs.
    
    The training loop now iterates through this list, calling
    env.step() 8 times. This is 100% stable and correct,
    and will still be fast as the 8 cores are used for the
    train_step() bottleneck.
"""

import os
import copy
import random
import argparse
import time
from dataclasses import dataclass
from typing import Dict, List, Tuple, Any, Callable

import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
from tqdm import trange
import matplotlib.pyplot as plt
import imageio
import logging
import datetime

# Use standard gymnasium import
import gymnasium as gym
# We will access vector environments via gym.vector
import rware  # Import rware to register environments

# ----------------------- Logging Setup -----------------------
# Set up logging to print timestamped messages
logging.basicConfig(
    level=logging.INFO,
    format="[%(asctime)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)
log = logging.info

# ----------------------- Config / Device -----------------------
DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")

# ----------------------- Helpers (simplified) -----------------------

def one_hot_actions(actions: torch.LongTensor, n_agents: int, act_dim: int) -> torch.FloatTensor:
    B = actions.shape[0]
    oh = torch.zeros((B, n_agents * act_dim), device=actions.device)
    for i in range(n_agents):
        idx = actions[:, i]
        oh.scatter_(1, (i * act_dim + idx).unsqueeze(1), 1.0)
    return oh

# ----------------------- Replay Buffer -----------------------
@dataclass
class Transition:
    obs: np.ndarray # [N_AGENTS, OBS_DIM]
    state: np.ndarray # [STATE_DIM]
    actions: np.ndarray # [N_AGENTS]
    reward: float # Scalar sum of team rewards
    next_obs: np.ndarray # [N_AGENTS, OBS_DIM]
    next_state: np.ndarray # [STATE_DIM]
    done: float # 0.0 or 1.0

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
        return self.net(x)

class CriticNet(nn.Module):
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
        x = torch.cat([state, joint_actions_onehot], dim=1)
        return self.net(x).squeeze(1)

# ----------------------- MASAC -----------------------
class MASAC:
    def __init__(self, n_agents: int, obs_dim: int, act_dim: int, state_dim: int,
                 lr: float = 3e-4, gamma: float = 0.99, tau: float = 0.005,
                 target_entropy: float = None):
        self.n_agents = n_agents
        self.obs_dim = obs_dim
        self.act_dim = act_dim
        self.state_dim = state_dim
        self.joint_act_dim = self.n_agents * self.act_dim

        self.actors = nn.ModuleList([ActorNet(obs_dim, act_dim).to(DEVICE) for _ in range(self.n_agents)])
        self.critics1 = nn.ModuleList([CriticNet(state_dim, self.joint_act_dim).to(DEVICE) for _ in range(self.n_agents)])
        self.critics2 = nn.ModuleList([CriticNet(state_dim, self.joint_act_dim).to(DEVICE) for _ in range(self.n_agents)])
        self.target_critics1 = copy.deepcopy(self.critics1)
        self.target_critics2 = copy.deepcopy(self.critics2)
        
        for p in sum([list(tc.parameters()) for tc in self.target_critics1 + self.target_critics2], []):
            p.requires_grad = False

        actor_params = sum([list(a.parameters()) for a in self.actors], [])
        critic_params = sum([list(c.parameters()) for c in self.critics1 + self.critics2], [])
        self.actor_opt = optim.Adam(actor_params, lr=lr)
        self.critic_opt = optim.Adam(critic_params, lr=lr)

        if target_entropy is None:
            target_entropy = -float(self.act_dim) * 0.98
        self.target_entropy = target_entropy
        self.log_alpha = torch.tensor(0.0, requires_grad=True, device=DEVICE)
        self.alpha_opt = optim.Adam([self.log_alpha], lr=lr)

        self.gamma = gamma
        self.tau = tau
        self.update_targets(tau=1.0) # Full copy at start

    @property
    def alpha(self):
        return self.log_alpha.exp()

    def select_actions(self, obs_batched: np.ndarray, deterministic: bool = False) -> np.ndarray:
        """
        obs_batched: [N_ENVS, N_AGENTS, OBS_DIM]
        deterministic: if True pick argmax, else sample categorical from policy
        Returns: [N_ENVS, N_AGENTS] (array of action indices)
        """
        n_envs, n_agents, _ = obs_batched.shape
        actions = np.zeros((n_envs, n_agents), dtype=int)
        
        for i in range(self.n_agents):
            # Process all environments for agent 'i' in one batch
            o_agent_i = torch.from_numpy(obs_batched[:, i, :].astype(np.float32)).to(DEVICE) # [N_ENVS, OBS_DIM]
            logits = self.actors[i](o_agent_i)  # [N_ENVS, ACT_DIM]
            probs = torch.softmax(logits, dim=1)
            
            if deterministic:
                a = torch.argmax(probs, dim=1) # [N_ENVS]
            else:
                a = torch.multinomial(probs, num_samples=1).squeeze(1) # [N_ENVS]
                
            actions[:, i] = a.cpu().numpy()
            
        return actions # [N_ENVS, N_AGENTS]

    def train_step(self, batch: List[Transition]) -> Tuple[float, float, float]:
        # Unpack the batch
        obs = torch.from_numpy(np.stack([t.obs for t in batch], axis=0)).float().to(DEVICE) # [B, N_AGENTS, OBS_DIM]
        next_obs = torch.from_numpy(np.stack([t.next_obs for t in batch], axis=0)).float().to(DEVICE)
        state = torch.from_numpy(np.stack([t.state for t in batch], axis=0)).float().to(DEVICE) # [B, STATE_DIM]
        next_state = torch.from_numpy(np.stack([t.next_state for t in batch], axis=0)).float().to(DEVICE)
        actions = torch.from_numpy(np.stack([t.actions for t in batch], axis=0)).long().to(DEVICE) # [B, N_AGENTS]
        rewards = torch.from_numpy(np.array([t.reward for t in batch], dtype=np.float32)).unsqueeze(1).to(DEVICE) # [B, 1]
        dones = torch.from_numpy(np.array([t.done for t in batch], dtype=np.float32)).unsqueeze(1).to(DEVICE) # [B, 1]

        B, n, _ = obs.shape # B=batch_size, n=n_agents

        # Critic update
        # Convert actions [B, N_AGENTS] to joint one-hot [B, JOINT_ACT_DIM]
        joint_actions_oh = one_hot_actions(actions, self.n_agents, self.act_dim)
        
        q1_vals = []
        q2_vals = []
        for i in range(self.n_agents):
            q1 = self.critics1[i](state, joint_actions_oh) # [B]
            q2 = self.critics2[i](state, joint_actions_oh) # [B]
            q1_vals.append(q1)
            q2_vals.append(q2)
        q1_stack = torch.stack(q1_vals, dim=1) # [B, N_AGENTS]
        q2_stack = torch.stack(q2_vals, dim=1) # [B, N_AGENTS]

        with torch.no_grad():
            next_action_probs = []
            next_action_logp = []
            for i in range(self.n_agents):
                logits_next = self.actors[i](next_obs[:, i, :])
                probs = torch.softmax(logits_next, dim=1)
                logp = torch.log_softmax(logits_next, dim=1)
                next_action_probs.append(probs)
                next_action_logp.append(logp)

            # Generate all possible joint action indices
            # all_joint_indices shape: [n_joint, N_AGENTS]
            all_joint_indices = np.array(np.meshgrid(*([np.arange(self.act_dim)] * self.n_agents), 
                                                      indexing='ij')).reshape(self.n_agents, -1).T
            n_joint = all_joint_indices.shape[0] # Total number of joint actions (act_dim^n_agents)
            
            # Target Q-value accumulator [B, N_AGENTS]
            target_vals_per_agent = torch.zeros((B, self.n_agents), device=DEVICE)

            for joint_idx in range(n_joint):
                joint_actions_indices = all_joint_indices[joint_idx] # [N_AGENTS]
                
                # Joint probability (jp) and Joint log probability (jlogp)
                jp = torch.ones((B,), device=DEVICE)
                jlogp = torch.zeros((B,), device=DEVICE)
                for ag_i, a_val in enumerate(joint_actions_indices):
                    a_val = int(a_val)
                    jp = jp * next_action_probs[ag_i][:, a_val]
                    jlogp = jlogp + next_action_logp[ag_i][:, a_val]

                # Create one-hot joint action vector [B, JOINT_ACT_DIM]
                joint_oh_row = torch.zeros((self.joint_act_dim,), device=DEVICE)
                for ag_i, a_val in enumerate(joint_actions_indices):
                    joint_oh_row[ag_i * self.act_dim + int(a_val)] = 1.0
                joint_oh = joint_oh_row.unsqueeze(0).repeat(B, 1)

                q_min_vals = []
                for i in range(self.n_agents):
                    q1_t = self.target_critics1[i](next_state, joint_oh)
                    q2_t = self.target_critics2[i](next_state, joint_oh)
                    q_min = torch.min(q1_t, q2_t)
                    q_min_vals.append(q_min)

                q_min_stack = torch.stack(q_min_vals, dim=1) # [B, N_AGENTS]
                
                # Q_target = jp * (Q_min - alpha * jlogp)
                weighted = (q_min_stack - self.alpha.detach() * jlogp.unsqueeze(1)) * jp.unsqueeze(1)
                target_vals_per_agent += weighted

            # Final target: R + gamma * (weighted sum of Q targets)
            y_agents = rewards + (1.0 - dones) * self.gamma * target_vals_per_agent

        # Calculate critic loss
        critic_loss = 0.0
        for i in range(self.n_agents):
            q1 = q1_stack[:, i]
            q2 = q2_stack[:, i]
            y = y_agents[:, i]
            l1 = nn.MSELoss()(q1, y.detach())
            l2 = nn.MSELoss()(q2, y.detach())
            critic_loss = critic_loss + l1 + l2

        self.critic_opt.zero_grad()
        critic_loss.backward()
        torch.nn.utils.clip_grad_norm_(sum([list(c.parameters()) for c in self.critics1 + self.critics2], []), 10.0)
        self.critic_opt.step()

        # Actor update
        action_probs = []
        action_logps = []
        for i in range(self.n_agents):
            logits = self.actors[i](obs[:, i, :])
            probs = torch.softmax(logits, dim=1)
            logp = torch.log_softmax(logits, dim=1)
            action_probs.append(probs)
            action_logps.append(logp)

        # Policy objective calculation: sum_joint_action [ P(joint_a) * (alpha*logP(joint_a) - sum_i Q_i(s, joint_a)) ]
        policy_obj = torch.zeros((B,), device=DEVICE)
        
        for joint_idx in range(n_joint):
            joint_actions_indices = all_joint_indices[joint_idx]
            
            jp = torch.ones((B,), device=DEVICE)
            jlogp = torch.zeros((B,), device=DEVICE)
            for ag_i, a_val in enumerate(joint_actions_indices):
                a_val = int(a_val)
                jp = jp * action_probs[ag_i][:, a_val]
                jlogp = jlogp + action_logps[ag_i][:, a_val]

            joint_oh_row = torch.zeros((self.joint_act_dim,), device=DEVICE)
            for ag_i, a_val in enumerate(joint_actions_indices):
                joint_oh_row[ag_i * self.act_dim + int(a_val)] = 1.0
            joint_oh = joint_oh_row.unsqueeze(0).repeat(B, 1)

            q_min_vals = []
            for i in range(self.n_agents):
                q1_v = self.critics1[i](state, joint_oh)
                q2_v = self.critics2[i](state, joint_oh)
                q_min = torch.min(q1_v, q2_v)
                q_min_vals.append(q_min)
            
            q_min_stack = torch.stack(q_min_vals, dim=1) # [B, N_AGENTS]
            q_sum = q_min_stack.sum(dim=1) # [B]
            
            # Policy objective term: P(joint_a) * (alpha*logP(joint_a) - sum_i Q_i(s, joint_a))
            policy_obj = policy_obj + jp * (self.alpha.detach() * jlogp - q_sum)

        actor_loss = torch.mean(policy_obj) # Mean, not -Mean, because obj = (alpha*logp - Q_sum)

        self.actor_opt.zero_grad()
        actor_loss.backward()
        torch.nn.utils.clip_grad_norm_(sum([list(a.parameters()) for a in self.actors], []), 10.0)
        self.actor_opt.step()

        # Alpha update
        # Calculate expected joint log-probability
        avg_logpi = torch.zeros((B,), device=DEVICE)
        for joint_idx in range(n_joint):
            joint_actions_indices = all_joint_indices[joint_idx]
            
            jp = torch.ones((B,), device=DEVICE)
            jlogp = torch.zeros((B,), device=DEVICE)
            for ag_i, a_val in enumerate(joint_actions_indices):
                a_val = int(a_val)
                jp = jp * action_probs[ag_i][:, a_val]
                jlogp = jlogp + action_logps[ag_i][:, a_val]
                
            avg_logpi = avg_logpi + jp * jlogp

        alpha_loss = -(self.log_alpha * (avg_logpi + self.target_entropy).detach()).mean()

        self.alpha_opt.zero_grad()
        alpha_loss.backward()
        self.alpha_opt.step()

        self.update_targets()

        return float(critic_loss.detach().cpu().item()), float(actor_loss.detach().cpu().item()), float(alpha_loss.detach().cpu().item())
    
    @torch.no_grad()
    def update_targets(self, tau: float = None):
        if tau is None:
            tau = self.tau
        # Zip the modules, then zip their parameters
        for c, tc in zip(self.critics1, self.target_critics1):
            for p, tp in zip(c.parameters(), tc.parameters()):
                tp.data.copy_(tau * p.data + (1.0 - tau) * tp.data)
        for c, tc in zip(self.critics2, self.target_critics2):
            for p, tp in zip(c.parameters(), tc.parameters()):
                tp.data.copy_(tau * p.data + (1.0 - tau) * tp.data)
                
    def save_model(self, path: str, n_agents: int, obs_dim: int, act_dim: int, state_dim: int):
        torch.save({
            'actors': [a.state_dict() for a in self.actors],
            'critics1': [c.state_dict() for c in self.critics1],
            'critics2': [c.state_dict() for c in self.critics2],
            'actor_opt': self.actor_opt.state_dict(),
            'critic_opt': self.critic_opt.state_dict(),
            'log_alpha': self.log_alpha.detach().cpu().numpy(),
            'alpha_opt': self.alpha_opt.state_dict(),
            'n_agents': n_agents,
            'obs_dim': obs_dim,
            'act_dim': act_dim,
            'state_dim': state_dim,
        }, path)

    def load_model(self, path: str):
        checkpoint = torch.load(path, map_location=DEVICE)
        for i in range(self.n_agents):
            self.actors[i].load_state_dict(checkpoint['actors'][i])
            self.critics1[i].load_state_dict(checkpoint['critics1'][i])
            self.critics2[i].load_state_dict(checkpoint['critics2'][i])
        
        self.target_critics1 = copy.deepcopy(self.critics1)
        self.target_critics2 = copy.deepcopy(self.critics2)
        
        self.actor_opt.load_state_dict(checkpoint['actor_opt'])
        self.critic_opt.load_state_dict(checkpoint['critic_opt'])
        
        self.log_alpha = torch.tensor(checkpoint['log_alpha'], requires_grad=True, device=DEVICE)
        self.alpha_opt = optim.Adam([self.log_alpha], lr=self.actor_opt.defaults['lr']) # Re-init optimizer
        self.alpha_opt.load_state_dict(checkpoint['alpha_opt'])
        
        log(f"💡 Model loaded successfully from {path}")


# ----------------------- Environment Utilities -----------------------

def make_env_thunk(env_id: str, seed: int, max_steps: int) -> Callable:
    """Creates a thunk (a zero-argument function) that initializes an environment."""
    def _init():
        # Use render_mode=None for headless execution
        env = gym.make(env_id, max_steps=max_steps, render_mode=None)
        # Note: We reset here, but the vector env will re-reset
        env.reset(seed=seed)
        return env
    return _init

def get_env_spaces(env_id: str, max_steps: int) -> Tuple[int, int, int]:
    """Create a dummy env to extract spaces."""
    temp_env = gym.make(env_id, max_steps=max_steps)
    
    # FIX: Correctly get dims for Tuple spaces
    # Obs space is Tuple(Box, Box, ...), one Box per agent
    obs_dim = temp_env.observation_space[0].shape[0]
    n_agents = len(temp_env.observation_space)
    
    # Action space is Tuple(Discrete, Discrete, ...)
    act_dim = temp_env.action_space[0].n
    
    state_dim = n_agents * obs_dim # Simple concatenation
    
    temp_env.close()
    return n_agents, obs_dim, act_dim, state_dim

# ----------------------- GIF Generation -----------------------
def create_rollout_gif(env_id: str, masac: MASAC, filename="rware_rollout.gif", max_steps=500, seed=42):
    """Generate rollout GIF using manual grid plotting (headless-compatible)."""
    log("Generating rollout GIF...")
    # Create a new, single env for rendering the GIF
    env = gym.make(env_id, max_steps=max_steps, render_mode='rgb_array')
    result = env.reset(seed=seed)
    obs_tuple = result[0] # RWARE reset returns (obs_tuple, info_dict)
    
    # Convert tuple to [1, N_AGENTS, OBS_DIM] for select_actions
    obs_mat = np.stack(obs_tuple, axis=0).astype(np.float32)
    obs_batched = np.expand_dims(obs_mat, axis=0) # [1, N_AGENTS, OBS_DIM]

    frames = []
    done = False
    step = 0
    total_reward = 0
    
    # Capture first frame
    try:
        frames.append(env.render())
    except Exception as e:
        log(f"Could not render initial frame for GIF: {e}. Skipping GIF generation.")
        env.close()
        return
    
    # Main rollout loop
    while not done and step < max_steps:
        # Get actions: [1, N_AGENTS]
        acts_batched = masac.select_actions(obs_batched, deterministic=True)
        # Convert to list [act1, act2, ...] for env.step
        acts_list = list(acts_batched[0])
        
        obs_tuple, rewards, terminated, truncated, info = env.step(acts_list)
        done = bool(terminated) or bool(truncated)
        
        # RWARE returns rewards as a list [r1, r2, ...]
        r = float(np.sum(rewards))
        total_reward += r
        
        step += 1
        frames.append(env.render())
        
        # Convert next obs tuple to [1, N_AGENTS, OBS_DIM] for next loop
        obs_mat = np.stack(obs_tuple, axis=0).astype(np.float32)
        obs_batched = np.expand_dims(obs_mat, axis=0)
        
        if done:
            break
    
    env.close()
    
    # Add small pause at end
    for _ in range(10):
        frames.append(frames[-1])
    
    # Save GIF
    imageio.mimsave(filename, frames, fps=8, loop=0)
    log(f"✅ Rollout GIF saved: {filename}")
    log(f"   Steps: {step}, Total reward: {total_reward:.2f}")


# ----------------------- Training -----------------------
def train_masac(args):
    log("="*60)
    log("MASAC Training with Parallel Environments (Manual Vector Fix V37)")
    log(f"  Environment ID: {args.env_id}")
    log(f"  Starting at Episode: {args.start_episode}")
    log(f"  Target Total Episodes: {args.episodes}")
    log(f"  Max steps: {args.max_steps}")
    log(f"  Vectorized Envs: {args.num_envs} (Manual List)")
    log(f"  Load Checkpoint: {args.load_checkpoint}")
    log("="*66)
    
    # Set seeds
    torch.manual_seed(args.seed)
    np.random.seed(args.seed)
    random.seed(args.seed)
    
    # Create directories
    os.makedirs("checkpoints", exist_ok=True)
    os.makedirs("logs", exist_ok=True)
    
    # Get environment dimensions
    n_agents, obs_dim, act_dim, state_dim = get_env_spaces(args.env_id, args.max_steps)
    log(f"💡 Environment Specs: Agents={n_agents}, Obs Dim={obs_dim}, Act Dim={act_dim}, State Dim={state_dim}")

    # --- V37 FIX: Manual Environment List ---
    # We are no longer using gymnasium.vector.SyncVectorEnv
    # We create a simple list of standard environments.
    log(f"Initializing {args.num_envs} manual environments in a Python list...")
    envs = [gym.make(args.env_id, max_steps=args.max_steps, render_mode=None) for _ in range(args.num_envs)]
    # Seed and reset each one
    current_obs_batched = np.zeros((args.num_envs, n_agents, obs_dim), dtype=np.float32)
    for i, env in enumerate(envs):
        obs_tuple, info_dict = env.reset(seed=args.seed + i)
        current_obs_batched[i] = np.stack(obs_tuple, axis=0).astype(np.float32)
    log("✅ Manual environments initialized and reset.")
    
    masac = MASAC(n_agents=n_agents, obs_dim=obs_dim, act_dim=act_dim, 
                  state_dim=state_dim, lr=args.lr, gamma=args.gamma, tau=args.tau)
    
    rb = ReplayBuffer(args.buffer_capacity)
    
    # Load checkpoint if specified
    if args.load_checkpoint:
        try:
            masac.load_model(args.load_checkpoint)
        except Exception as e:
            log(f"⚠️ Could not load checkpoint: {e}. Starting from scratch.")
            args.start_episode = 0

    # --- Data tracking ---
    total_steps = 0
    episode_counter = args.start_episode
    # This now tracks the episodic reward for each *manual* env
    episode_rewards = np.zeros(args.num_envs, dtype=np.float32)
    
    rewards_log = []
    critic_loss_log = []
    actor_loss_log = []
    start_time = time.time()
    
    # Pre-allocate arrays for step data
    next_obs_batched = np.zeros_like(current_obs_batched)
    dones = np.zeros(args.num_envs, dtype=bool)
    team_rewards = np.zeros(args.num_envs, dtype=np.float32)
    
    # Main training loop, controlled by total completed episodes
    pbar = trange(args.start_episode, args.episodes, desc=f"Training MASAC on {args.env_id}")
    
    try:
        while episode_counter < args.episodes:
            # Select actions: [N_ENVS, N_AGENTS]
            # This function is already batched and works perfectly.
            actions_batched = masac.select_actions(current_obs_batched, deterministic=False)
            
            # --- V37 FIX: Manual Step Loop ---
            # Iterate through each environment in our list
            for env_idx, env in enumerate(envs):
                # Get the action list for this specific environment
                # actions_batched[env_idx] is [act_ag1, act_ag2]
                actions_list_for_env = list(actions_batched[env_idx])
                
                # Use the standard, stable, single-env step
                next_obs_tuple, rewards_list, terminated, truncated, info = env.step(actions_list_for_env)
                
                # Store the results in our pre-allocated batch arrays
                next_obs_batched[env_idx] = np.stack(next_obs_tuple, axis=0).astype(np.float32)
                team_rewards[env_idx] = np.sum(rewards_list)
                dones[env_idx] = terminated or truncated

            # Now that we have stepped all 8 envs, we have
            # next_obs_batched, team_rewards, and dones, all populated.
            
            # Update per-environment episodic rewards
            episode_rewards += team_rewards
            total_steps += args.num_envs

            # --- Store transitions in replay buffer ---
            # This loop handles all environments that just took a step
            for env_idx in range(args.num_envs):
                obs_mat = current_obs_batched[env_idx] # [N_AGENTS, OBS_DIM]
                next_obs_mat = next_obs_batched[env_idx] # [N_AGENTS, OBS_DIM]
                acts_vec = actions_batched[env_idx] # [N_AGENTS]
                r = team_rewards[env_idx]
                done = dones[env_idx]
                
                # State is just concatenated observations
                state = obs_mat.reshape(-1)
                next_state = next_obs_mat.reshape(-1)

                rb.push(Transition(obs=obs_mat, state=state, actions=acts_vec,
                                 reward=r, next_obs=next_obs_mat, 
                                 next_state=next_state, done=float(done)))

                # If this environment is done, log and reset it
                if done:
                    # Log the completed episode
                    final_reward = episode_rewards[env_idx]
                    rewards_log.append(final_reward)
                    
                    # --- ETA Logging ---
                    episodes_done = len(rewards_log)
                    time_elapsed_s = time.time() - start_time
                    avg_time_per_ep = time_elapsed_s / episodes_done if episodes_done > 0 else 0
                    eps_remaining = args.episodes - (episode_counter + 1)
                    eta_s = eps_remaining * avg_time_per_ep
                    eta_formatted = str(datetime.timedelta(seconds=int(eta_s)))
                    
                    log(f"[{episode_counter+1}/{args.episodes}] Env {env_idx}: Reward = {final_reward:.2f}, Alpha = {masac.alpha.item():.4f}, ETA: {eta_formatted}")
                    
                    # Increment global counters
                    episode_counter += 1
                    pbar.update(1) # Update progress bar
                    if episode_counter >= args.episodes:
                        break # Exit inner loop

                    # Reset this specific environment's reward tracker
                    episode_rewards[env_idx] = 0
                    
                    # V37 FIX: Manually reset the env
                    obs_tuple, info_dict = envs[env_idx].reset(seed=args.seed + episode_counter)
                    # And update its obs in the *next* obs batch
                    # (it will be copied to current_obs_batched at the end of the loop)
                    next_obs_batched[env_idx] = np.stack(obs_tuple, axis=0).astype(np.float32)

            if episode_counter >= args.episodes:
                break # Exit outer loop
            
            # --- Training Step ---
            # This is the REAL bottleneck where the 8 cores will be used
            if len(rb) >= max(args.batch_size, args.start_learn_after):
                batch = rb.sample(args.batch_size)
                c_loss, a_loss, al_loss = masac.train_step(batch)
                critic_loss_log.append(c_loss)
                actor_loss_log.append(a_loss)

            # Update current obs for next loop
            current_obs_batched = next_obs_batched.copy() # Use .copy() for safety

            # --- Checkpoint Saving ---
            if (episode_counter > 0) and (episode_counter) % args.save_interval == 0:
                save_path = f"checkpoints/masac_rware_ep{episode_counter}.pt"
                masac.save_model(save_path, n_agents, obs_dim, act_dim, state_dim)
                log(f"💾 Checkpoint saved: {save_path}")
                
    except (KeyboardInterrupt, Exception) as e:
        log(f"\nCaught exception during training loop: {e}")
    finally:
        pbar.close()
        # V37 FIX: Manually close all environments
        try:
             for env in envs:
                 env.close()
        except Exception:
            pass # Ignore close error
        
        # Save final state regardless of exit reason
        save_path = "checkpoints/masac_rware_final_state.pt"
        masac.save_model(save_path, n_agents, obs_dim, act_dim, state_dim)
        log(f"💾 Final model state saved: {save_path}")

        # Generate final rollout GIF (Ignore the OpenGL error if it occurs)
        try:
            gif_filename = f"logs/masac_rware_final_ep{episode_counter}.gif"
            create_rollout_gif(args.env_id, masac, filename=gif_filename, max_steps=args.max_steps, seed=args.seed)
        except Exception as e:
            log(f"Could not render GIF (This is non-critical): {e}")

    return rewards_log, critic_loss_log, actor_loss_log


# ----------------------- Main -----------------------
def main():
    parser = argparse.ArgumentParser(description='MASAC Training on RWARE/TARWARE')
    
    # Environment parameters
    parser.add_argument('--env_id', type=str, default="rware-tiny-2ag-v2",
                        help='The Gymnasium environment ID to use (e.g., rware-small-4ag-v2)')
    parser.add_argument('--max_steps', type=int, default=200, help='Max steps per episode') # Increased
    
    # Parallelism
    parser.add_argument('--num_envs', type=int, default=8, help='Number of parallel environments to run')
    
    # Training parameters
    parser.add_argument('--episodes', type=int, default=50, help='Number of training episodes')
    parser.add_argument('--batch_size', type=int, default=128, help='Batch size')
    parser.add_argument('--buffer_capacity', type=int, default=100000, help='Replay buffer capacity')
    parser.add_argument('--start_learn_after', type=int, default=5000, help='Start learning after N samples') # Increased

    # Continuation arguments
    parser.add_argument('--load_checkpoint', type=str, default=None, help='Path to checkpoint file for continuation')
    parser.add_argument('--start_episode', type=int, default=0, help='Episode number to start counting from')

    # Algorithm parameters
    parser.add_argument('--lr', type=float, default=5e-4, help='Learning rate')
    parser.add_argument('--gamma', type=float, default=0.99, help='Discount factor')
    parser.add_argument('--tau', type=float, default=0.005, help='Target network update rate')
    
    # Other
    parser.add_argument('--seed', type=int, default=42, help='Random seed')
    parser.add_argument('--save_interval', type=int, default=500, help='Save checkpoint every N episodes')
    
    args = parser.parse_args()
    
    log(f"Starting training on {args.env_id} with seed {args.seed}...")
    log(f"Using device: {DEVICE}")
    
    rewards, critic_losses, actor_losses = train_masac(args)
    
    # Plotting
    try:
        import matplotlib
        # We need to set the backend before importing pyplot if running headless
        matplotlib.use('Agg')
        import matplotlib.pyplot as plt
        
        plt.figure(figsize=(15, 5))
        
        plt.subplot(1, 3, 1)
        plt.plot(rewards)
        plt.title('Episode Reward')
        plt.xlabel('Episode')
        plt.ylabel('Reward')
        
        plt.subplot(1, 3, 2)
        if critic_losses:
            plt.plot(critic_losses)
            plt.title('Critic Loss')
            plt.xlabel('Training Step')
            plt.ylabel('Loss')
        
        plt.subplot(1, 3, 3)
        if actor_losses:
            plt.plot(actor_losses)
            plt.title('Actor Loss')
            plt.xlabel('Training Step')
            plt.ylabel('Loss')
        
        plt.tight_layout()
        plt.savefig("logs/training_curves.png", dpi=150)
        log("📊 Saved training curves to logs/training_curves.png")
    except Exception as e:
        log(f"Could not save plots: {e}")
    
    log("="*60)
    if rewards:
        log(f"Training completed! Final average reward (last 100 episodes): {np.mean(rewards[-100:]):.3f}")
    else:
        log("Training completed, but no episodes finished.")
    log("="*60)


if __name__ == "__main__":
    main()


