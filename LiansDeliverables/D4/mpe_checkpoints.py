import os
import copy
import random
import glob
import re
from dataclasses import dataclass
from typing import Dict, List, Tuple, Any

import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
import matplotlib.pyplot as plt
import imageio.v2 as imageio
from pettingzoo.mpe import simple_spread_v3
from PIL import Image # Needed to handle environment rendering output

# --- Configuration (Adjust if your checkpoint path is different) ---
# Assuming the script runs from the D4 folder and checkpoints are in the mpe subfolder
CHECKPOINT_DIR = './mpe' 
FILE_PATTERN = 'qmix_ep*.pt'
PERFORMANCE_GIF_FILENAME = 'qmix_evaluation_animation.gif'
ENVIRONMENT_GIF_FILENAME = 'qmix_environment_episode.gif' # New GIF file for the environment playback
EVAL_EPISODES = 10 
TEMP_FRAMES_DIR = 'temp_eval_frames'
ENV_FRAMES_DIR = 'temp_env_frames' # New temporary directory for environment frames


# ----------------------- Copied Helpers (Required for loading) -----------------------
DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")

def unwrap_reset(x):
    return x[0] if isinstance(x, tuple) else x

def flatten_obs(o):
    if isinstance(o, dict):
        if 'observation' in o:
            return np.asarray(o['observation'], dtype=np.float32).ravel()
        parts = [flatten_obs(v) for v in o.values()]
        if not parts:
            return np.empty((0,), dtype=np.float32)
        return np.concatenate(parts).astype(np.float32)
    return np.asarray(o, dtype=np.float32).ravel()

def dict_obs_to_matrix(obs_dict: Dict[str, Any], agent_ids: List[str]) -> np.ndarray:
    mats = [flatten_obs(obs_dict[a]) for a in agent_ids]
    obs_dim = max(m.shape[0] for m in mats)
    padded = []
    for m in mats:
        if m.shape[0] != obs_dim:
            pad = np.zeros((obs_dim - m.shape[0],), dtype=np.float32)
            m = np.concatenate([m, pad]).astype(np.float32)
        padded.append(m)
    return np.stack(padded, axis=0)

def make_env(n_agents: int = 3, n_landmarks: int = 3, max_cycles: int = 25, render_mode: str = None):
    """Modified to accept render_mode for frame capture."""
    env = simple_spread_v3.parallel_env(
        N=n_agents,
        local_ratio=0.5,
        max_cycles=max_cycles,
        continuous_actions=False,
        render_mode=render_mode # Pass render_mode here
    )
    # Reset is outside the factory method if we want to determine dims, 
    # but here we reset it for consistency and possible seed setting.
    env.reset(seed=None)
    return env

def get_episode_number(filename):
    base_name = os.path.basename(filename) 
    match = re.search(r'ep(\d+)\.pt$', base_name)
    return int(match.group(1)) if match else -1

# ----------------------- Copied Networks -----------------------
class AgentNet(nn.Module):
    def __init__(self, obs_dim: int, act_dim: int, hidden: int = 128):
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(obs_dim, hidden), nn.ReLU(),
            nn.Linear(hidden, hidden), nn.ReLU(),
            nn.Linear(hidden, act_dim),
        )
    def forward(self, x: torch.Tensor) -> torch.Tensor: return self.net(x)

class MixerNet(nn.Module):
    def __init__(self, n_agents: int, state_dim: int, hyper_hidden: int = 128):
        super().__init__()
        self.n_agents = n_agents
        self.hyper_w = nn.Sequential(
            nn.Linear(state_dim, hyper_hidden), nn.ReLU(),
            nn.Linear(hyper_hidden, n_agents),
        )
        self.hyper_b = nn.Sequential(
            nn.Linear(state_dim, hyper_hidden), nn.ReLU(),
            nn.Linear(hyper_hidden, 1),
        )
    def forward(self, q_agents: torch.Tensor, state: torch.Tensor) -> torch.Tensor:
        w = torch.abs(self.hyper_w(state))
        b = self.hyper_b(state)
        q_tot = torch.sum(w * q_agents, dim=1, keepdim=True) + b
        return q_tot

# ----------------------- Modified QMIX -----------------------
class QMIX(nn.Module):
    def __init__(self, agent_ids: List[str], obs_dim: int, act_dim: int, state_dim: int, lr: float = 1e-3):
        super().__init__()
        self.agent_ids = list(agent_ids)
        self.n_agents = len(self.agent_ids)
        self.act_dim = act_dim
        self.agents = nn.ModuleList([AgentNet(obs_dim, act_dim) for _ in range(self.n_agents)]).to(DEVICE)
        self.mixer = MixerNet(self.n_agents, state_dim).to(DEVICE)
        self.target_agents = copy.deepcopy(self.agents).to(DEVICE)
        self.target_mixer = copy.deepcopy(self.mixer).to(DEVICE)
        self.opt = optim.Adam(list(self.agents.parameters()) + list(self.mixer.parameters()), lr=lr)
        self.update_targets(tau=1.0)
        
    @torch.no_grad()
    def update_targets(self, tau: float = 1.0): pass 

    def select_actions(self, obs_dict: Dict[str, Any], epsilon: float) -> Dict[str, int]:
        actions: Dict[str, int] = {}
        for i, aid in enumerate(self.agent_ids):
            o = flatten_obs(obs_dict[aid])
            o_t = torch.from_numpy(o).unsqueeze(0).to(DEVICE)
            if random.random() < epsilon:
                a = random.randrange(self.act_dim)
            else:
                with torch.no_grad():
                    q = self.agents[i](o_t)
                    a = int(torch.argmax(q, dim=1))
            actions[aid] = a
        return actions


# ----------------------- Evaluation and Plotting Logic -----------------------

def load_qmix_agent(ckpt_path: str) -> Tuple[QMIX, int, List[str]]:
    """Loads checkpoint and returns an initialized QMIX agent, episode num, and agent_ids."""
    
    episode_num = get_episode_number(ckpt_path)
    data = torch.load(ckpt_path, map_location=DEVICE, weights_only=False) 

    agent_ids = data['agent_ids']
    obs_dim = data['obs_dim']
    act_dim = data['act_dim']
    state_dim = data['state_dim']
    agent = QMIX(agent_ids=agent_ids, obs_dim=obs_dim, act_dim=act_dim, state_dim=state_dim)
    
    for a, sd in zip(agent.agents, data['agents']):
        a.load_state_dict(sd)
    agent.mixer.load_state_dict(data['mixer'])

    return agent, episode_num, agent_ids

def evaluate_checkpoint(ckpt_path: str, episodes: int, max_cycles: int = 25) -> Tuple[int, float]:
    """Evaluates a checkpoint for performance plot generation."""
    try:
        agent, episode_num, agent_ids = load_qmix_agent(ckpt_path)
    except Exception as e:
        print(f"Error loading {os.path.basename(ckpt_path)}: {e}")
        return get_episode_number(ckpt_path), np.nan

    env = make_env(max_cycles=max_cycles)
    total_reward = 0.0
    for _ in range(episodes):
        obs = unwrap_reset(env.reset())
        ep_r = 0.0
        for _ in range(max_cycles):
            acts = agent.select_actions(obs, epsilon=0.0)
            obs, rewards, terms, truncs, _ = env.step(acts)
            r = float(np.mean([rewards[a] for a in agent_ids]))
            ep_r += r
            if any(terms.values()) or all(truncs.values()):
                break
        total_reward += ep_r
    env.close()
    
    avg_reward = total_reward / episodes
    print(f"Evaluated {os.path.basename(ckpt_path)} (Episode {episode_num}): Avg Reward = {avg_reward:.3f}")
    return episode_num, avg_reward


def generate_env_gif_for_checkpoint(ckpt_path: str, max_cycles: int = 25):
    """
    Loads the final checkpoint and runs one episode, saving frames as a GIF.
    """
    os.makedirs(ENV_FRAMES_DIR, exist_ok=True)
    image_paths = []
    
    try:
        agent, episode_num, agent_ids = load_qmix_agent(ckpt_path)
    except Exception as e:
        print(f"Error loading final checkpoint {os.path.basename(ckpt_path)} for GIF: {e}")
        return

    # Use 'rgb_array' for capturing frames
    env = make_env(max_cycles=max_cycles, render_mode='rgb_array')
    obs = unwrap_reset(env.reset())
    
    print(f"\n--- Generating Environment GIF for Episode {episode_num} ---")
    
    # Capture initial frame
    frame = env.render()
    img = Image.fromarray(frame)
    temp_filename = os.path.join(ENV_FRAMES_DIR, f'env_frame_0000.png')
    img.save(temp_filename)
    image_paths.append(temp_filename)
    
    # Run one episode and capture frames
    for t in range(max_cycles):
        acts = agent.select_actions(obs, epsilon=0.0)
        obs, rewards, terms, truncs, _ = env.step(acts)

        frame = env.render()
        if frame is not None:
            img = Image.fromarray(frame)
            temp_filename = os.path.join(ENV_FRAMES_DIR, f'env_frame_{t+1:04d}.png')
            img.save(temp_filename)
            image_paths.append(temp_filename)

        if any(terms.values()) or all(truncs.values()):
            break

    env.close()
    
    # Compile GIF
    if image_paths:
        print(f"Compiling GIF: {ENVIRONMENT_GIF_FILENAME}...")
        images = [imageio.imread(file) for file in image_paths]
        # Use a faster duration (e.g., 50ms = 20 FPS) for video playback
        imageio.mimsave(ENVIRONMENT_GIF_FILENAME, images, duration=50) 
        print(f"Environment GIF saved to {os.path.abspath(ENVIRONMENT_GIF_FILENAME)}")
    
    # Clean up
    for file in image_paths:
        os.remove(file)
    os.rmdir(ENV_FRAMES_DIR)


# ----------------------- Main Plotting Logic (Combined) -----------------------
def generate_plot_and_env_gifs():
    # 1. Find and sort checkpoints
    search_path = os.path.join(CHECKPOINT_DIR, FILE_PATTERN)
    all_files = glob.glob(search_path)
    if not all_files:
        print(f"No files matching '{FILE_PATTERN}' found in '{CHECKPOINT_DIR}'.")
        return

    sorted_files = sorted(all_files, key=get_episode_number)
    if not sorted_files:
        print("No valid checkpoints found.")
        return

    # --- Part A: Performance Evaluation and GIF ---
    results = []
    print(f"\n--- Starting Performance Evaluation of {len(sorted_files)} Checkpoints ---")
    for file_path in sorted_files:
        ep_num, avg_r = evaluate_checkpoint(file_path, EVAL_EPISODES)
        if not np.isnan(avg_r):
            results.append((ep_num, avg_r))
    
    if results:
        results.sort(key=lambda x: x[0])
        episodes = np.array([r[0] for r in results])
        rewards = np.array([r[1] for r in results])

        # Generate GIF frames
        os.makedirs(TEMP_FRAMES_DIR, exist_ok=True)
        plot_image_paths = []

        print("\n--- Generating Performance Plot GIF Frames ---")
        for i in range(1, len(results) + 1):
            current_episodes = episodes[:i]
            current_rewards = rewards[:i]

            fig, ax = plt.subplots(figsize=(10, 6))
            ax.plot(current_episodes, current_rewards, marker='o', linestyle='-', color='blue', label='Avg Episode Reward')
            ax.plot(current_episodes[-1], current_rewards[-1], marker='o', color='red', markersize=8)
            ax.set_title(f'QMIX Performance Progression (Episode: {current_episodes[-1]})')
            ax.set_xlabel('Training Episode')
            ax.set_ylabel(f'Average Evaluation Reward (over {EVAL_EPISODES} episodes)')
            ax.grid(True, linestyle='--')
            ax.legend()
            ax.set_xlim(0, episodes.max() * 1.05)
            ax.set_ylim(rewards.min() * 0.9, rewards.max() * 1.1 if rewards.max() > 0 else rewards.max() * 0.9)

            temp_filename = os.path.join(TEMP_FRAMES_DIR, f'frame_{i:04d}.png')
            fig.savefig(temp_filename, dpi=100)
            plt.close(fig)
            plot_image_paths.append(temp_filename)

        # Compile Performance GIF
        print(f"Compiling Performance GIF: {PERFORMANCE_GIF_FILENAME}...")
        if plot_image_paths:
            images = [imageio.imread(file) for file in plot_image_paths]
            imageio.mimsave(PERFORMANCE_GIF_FILENAME, images, duration=500, loop=0) 
            print(f"Performance GIF saved to {os.path.abspath(PERFORMANCE_GIF_FILENAME)}")

        # Clean up plots
        for file in plot_image_paths: os.remove(file)
        os.rmdir(TEMP_FRAMES_DIR)
        
    else:
        print("Performance plot/GIF skipped due to evaluation failure.")


    # --- Part B: Environment GIF of Final Agent ---
    final_checkpoint = sorted_files[-1]
    generate_env_gif_for_checkpoint(final_checkpoint)


# ----------------------- Execution -----------------------
if __name__ == "__main__":
    generate_plot_and_env_gifs()