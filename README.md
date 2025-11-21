# MARL-QMIX-Warehouse-Robots

Multi-Agent Reinforcement Learning using QMIX algorithm for training cooperative warehouse robots in Unity ML-Agents environment.

## Overview

This project implements multi-agent reinforcement learning (MARL) for autonomous warehouse robots using the QMIX (Q-Mixing) algorithm. Robots learn to coordinate pick-and-place tasks in a procedurally generated Unity warehouse environment.

### Key Features

- **QMIX Algorithm**: Value-based MARL with centralized training and decentralized execution
- **Unity ML-Agents 4.0**: Integration for physics-based warehouse simulation
- **EPyMARL Framework**: Extended PyMARL for multi-agent training
- **Procedural Generation**: Randomized warehouse layouts with configurable dimensions
- **Grid-Based Navigation**: Discrete action space for robot movement and interaction

### Performance

Trained agents achieve **207.96 mean return** after 350,000 timesteps (~3.3 hours training), demonstrating effective coordination for warehouse tasks.

## Repository Structure

```
MARL-QMIX-Warehouse-Robots/
├── epymarl/                          # Multi-agent RL framework
│   ├── src/                          # Python source code
│   │   ├── config/algs/              # Algorithm configurations
│   │   │   └── qmix_warehouse_improved.yaml  # Optimized QMIX config
│   │   ├── envs/                     # Environment wrappers
│   │   │   ├── unity_wrapper.py      # Unity ML-Agents interface
│   │   │   └── warehouse_env.py      # RWARE environment
│   │   ├── learners/                 # Training algorithms
│   │   ├── modules/                  # Neural network modules
│   │   └── main.py                   # Training entry point
│   ├── requirements.txt              # Python dependencies
│   └── env_requirements.txt          # Environment dependencies
├── WarehouseProjectURP/              # Unity project (URP)
│   ├── Assets/                       # Unity assets
│   │   ├── Scenes/Warehouse.unity    # Main training scene
│   │   ├── Scripts/                  # C# ML-Agents scripts
│   │   └── ML-Agents/                # ML-Agents configurations
│   └── Packages/                     # Unity packages
├── com.unity.robotics.warehouse.base/  # Shared warehouse code
└── com.unity.robotics.warehouse.urp/   # URP-specific warehouse package
```

## Installation

### Prerequisites

- **Python**: 3.8-3.10
- **Unity**: 2021.1+ (tested with Unity 6.0)
- **OS**: Linux, macOS, or Windows
- **RAM**: 8GB+ recommended
- **CUDA** (optional): For GPU training

### Setup Steps

#### 1. Clone Repository

```bash
git clone git@github.com:pallman14/MARL-QMIX-Warehouse-Robots.git
cd MARL-QMIX-Warehouse-Robots
```

#### 2. Create Python Virtual Environment

```bash
python3 -m venv epymarl_env
source epymarl_env/bin/activate  # On Windows: epymarl_env\Scripts\activate
```

#### 3. Install Python Dependencies

```bash
cd epymarl
pip install --upgrade pip
pip install -r requirements.txt
pip install -r env_requirements.txt
```

**Key Dependencies:**
- `torch==2.9.0` (or appropriate version for your system)
- `mlagents==4.0.0`
- `sacred==0.8.7`
- `numpy==2.1.2`
- `pyyaml==5.3.1`

#### 4. Open Unity Project

1. Open Unity Hub
2. Add project: `WarehouseProjectURP/`
3. Open with Unity 2021.1+ (Unity 6.0 recommended)
4. Wait for package import and compilation

## Training

### Quick Start

#### 1. Start Python Training Script

```bash
cd epymarl
source ../epymarl_env/bin/activate
python src/main.py --config=qmix_warehouse_improved --env-config=unity_warehouse with t_max=500000
```

You should see: `[INFO] Listening on port 5004. Start training by pressing the Play button in the Unity Editor.`

#### 2. Start Unity Environment

1. In Unity Editor, open `Assets/Scenes/Warehouse.unity`
2. Press the **Play** button ▶️
3. Training will begin automatically

#### 3. Monitor Training

Training logs appear in the console:

```
[INFO] t_env: 10000 / 500000
[INFO] Recent Stats | Episode: 50
return_mean: 25.42
q_taken_mean: 0.512
epsilon: 0.95
```

### Configuration

#### Hyperparameters

Edit `epymarl/src/config/algs/qmix_warehouse_improved.yaml`:

```yaml
# Core QMIX settings
lr: 0.001                        # Learning rate
batch_size: 16                   # Batch size
buffer_size: 5000                # Replay buffer size (episodes)
target_update_interval: 200      # Target network update frequency

# Exploration
epsilon_start: 1.0
epsilon_finish: 0.1
epsilon_anneal_time: 200000      # Steps to anneal epsilon

# Network architecture
agent: "rnn"                     # Use RNN agents
rnn_hidden_dim: 64               # GRU hidden units
mixer: "qmix"                    # QMIX mixing network
mixing_embed_dim: 32
hypernet_layers: 2
hypernet_embed: 64

# Training
t_max: 500000                    # Total timesteps
test_interval: 20000             # Test every N steps
save_model_interval: 100000      # Save checkpoint every N steps
```

#### Command-Line Overrides

```bash
python src/main.py --config=qmix_warehouse_improved --env-config=unity_warehouse \
  with t_max=1000000 lr=0.0005 batch_size=32
```

### Resuming Training

```bash
python src/main.py --config=qmix_warehouse_improved --env-config=unity_warehouse \
  with t_max=500000 \
  checkpoint_path="results/models/qmix_seed123_unity_warehouse_2025-11-20_01:46:29/" \
  load_step=300000
```

## Environment Details

### RWARE (Robotic Warehouse)

- **Grid Size**: Configurable (default: varies by scene)
- **Agents**: Multiple cooperative robots
- **Observation Space**: Local grid observations + agent state
- **Action Space**: 5 discrete actions
  - `0`: Turn Left
  - `1`: Turn Right
  - `2`: Move Forward
  - `3`: Load/Unload Shelf
  - `4`: No-op

### Reward Structure

- **Shelf Delivery**: +reward for delivering shelf to goal
- **Collision Penalty**: -reward for agent collisions
- **Time Penalty**: Small negative reward each step

## Results

### Training Performance

Training Run #93 (QMIX with optimized hyperparameters):

| Metric | Value |
|--------|-------|
| Final Return (Mean) | 207.96 |
| Final Test Return | 49.29 |
| Training Steps | 350,199 / 500,000 |
| Training Time | 3h 18min (active training) |
| Q-Value (Final) | 2.398 |
| Epsilon (Final) | 0.10 |

### Learning Curve

```
Steps    | Return  | Test Return | Epsilon
---------|---------|-------------|--------
10k      | 13.6    | 0.03        | 0.95
100k     | 50.6    | 0.05        | 0.55
200k     | 156.8   | 0.03        | 0.10
300k     | 228.4   | 0.08        | 0.10
350k     | 207.96  | 49.29       | 0.10
```

**Key Observations:**
- Rapid learning in first 100k steps
- Stable improvement through 300k steps
- 17.5× improvement in returns (13.6 → 238.1 at peak)

## Known Issues

### Unity Editor Timeout

**Issue**: Unity Editor becomes unresponsive around 350,000 timesteps (~3-4 hours) during training.

**Workaround**:
- Use Unity **standalone builds** for long training runs (>350k steps)
- Split training into multiple sessions with checkpointing every 100k steps
- Monitor system resources during extended training

## File Locations

### Training Outputs

- **Models**: `epymarl/results/models/qmix_seed{SEED}_{ENV}_{TIMESTAMP}/`
- **Sacred Logs**: `epymarl/results/sacred/qmix/{ENV}/{RUN_ID}/`
  - `config.json` - Full configuration
  - `run.json` - Run metadata and status
  - `metrics.json` - Training metrics
  - `cout.txt` - Console output

### Checkpoints

Model checkpoints saved at intervals (default: every 100k steps):
- `agent.th` - Agent network weights
- `mixer.th` - QMIX mixer network weights
- `opt.th` - Optimizer state

## Citation

If you use this code in your research, please cite:

```bibtex
@software{qmix_warehouse_robots,
  author = {Allman, Price},
  title = {MARL-QMIX-Warehouse-Robots},
  year = {2025},
  url = {https://github.com/pallman14/MARL-QMIX-Warehouse-Robots}
}
```

## License

This project builds upon:
- [EPyMARL](https://github.com/uoe-agents/epymarl) (Apache 2.0 License)
- [Unity ML-Agents](https://github.com/Unity-Technologies/ml-agents) (Apache 2.0 License)

## Acknowledgments

- EPyMARL framework by University of Edinburgh
- Unity ML-Agents by Unity Technologies
- QMIX algorithm by Rashid et al. (2018)
