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

**Note**: Initial training runs show a significant gap between training and test performance, indicating the agents have not yet learned an effective policy. Current best run (#93) achieved **207.96 training return** but only **0.21 test return** in pure greedy evaluation, suggesting the high training returns come primarily from epsilon-greedy exploration rather than learned behavior. This is an active area of investigation - see [Known Issues](#training-vs-test-performance-gap) for details.

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

## Troubleshooting

This section documents common installation and runtime issues encountered during setup and training.

### ML-Agents Installation Issues

#### Version Mismatch Errors

**Symptoms:**
- `ImportError: No module named 'mlagents'`
- Unity editor communication failures
- Training script hanging while waiting on the environment

**Solution:**

```bash
pip uninstall mlagents mlagents-envs -y
pip install mlagents==0.30.0
pip install mlagents-envs==0.30.0
```

**Note**: ML-Agents 4.0 (Unity package) corresponds to Python package version **0.30.0**.

#### Port Connection Failures

**Symptoms:**
- Python timeout errors
- Unity does not display "Listening on port 5004"
- Training cannot establish connection to Unity

**Solution:**

1. Close all Unity instances
2. Reopen the Warehouse project
3. Press Play and confirm port 5004 message
4. Start Python script only after Unity is listening

To kill a blocked port:

```bash
# Linux/macOS
sudo lsof -i :5004
kill -9 <PID>

# Windows
netstat -ano | findstr :5004
taskkill /PID <PID> /F
```

#### Barracuda Inference Errors

**Symptoms:**
- Unity error messages referencing the inference engine or model loading

**Solution:**
- Reinstall **Barracuda** package in Unity's Package Manager (Window → Package Manager)
- Ensure version compatibility with ML-Agents 4.0

### Sacred Installation Issues

#### Sacred Import Failures

**Symptoms:**
- `ImportError: No module named 'sacred'`
- Sacred-related errors when starting training

**Solution:**

```bash
pip uninstall sacred -y
pip install sacred==0.8.7
```

#### Sacred Logging Errors

**Symptoms:**
- `KeyError: 'config'`
- Missing or incomplete run directories in `results/sacred/`

**Solution:**

Ensure the Sacred logging directory exists:

```bash
mkdir -p epymarl/results/sacred
```

Fix permissions if needed:

```bash
# Linux/macOS
chmod -R 755 epymarl/results

# Windows: Right-click → Properties → Security → Edit permissions
```

Test Sacred integration:

```bash
python src/main.py --config=qmix_warehouse_improved --env-config=unity_warehouse with t_max=1000
```

Expected output:
```
INFO - qmix - Started run with ID "1"
```

### Virtual Environment Issues

**Symptoms:**
- Packages installed but not detected
- `pip` points to system Python instead of virtual environment

**Diagnosis:**

Check which Python/pip is active:

```bash
which python
which pip
```

Expected output should point to your virtual environment:
```
.../epymarl_env/bin/python
.../epymarl_env/bin/pip
```

**Solution:**

If incorrect, reactivate the virtual environment:

```bash
source epymarl_env/bin/activate  # Linux/macOS
# or
epymarl_env\Scripts\activate     # Windows
```

Verify activation by checking the prompt shows `(epymarl_env)`.

### Unity/Python Training Freezing

**Symptoms:**
- Unity Editor becomes unresponsive after 3-4 hours (~350k steps)
- Training stops progressing
- High memory usage

**Solutions:**

1. **Use Unity Standalone Builds** for long training runs:
   - Build Settings → Build
   - Run the standalone executable instead of Editor
   - Configure worker ID if running multiple instances

2. **Enable Checkpointing**:
   - Set `save_model_interval: 100000` in config
   - Resume training from checkpoints if interrupted

3. **Monitor System Resources**:
   ```bash
   # Linux
   htop

   # Monitor Unity process
   ps aux | grep Unity
   ```

4. **Split Training Sessions**:
   - Train in 100k-200k step increments
   - Use `checkpoint_path` and `load_step` to resume

### Missing Dependencies

**Symptoms:**
- Import errors for specific packages
- Module not found errors

**Solution:**

Reinstall all dependencies:

```bash
cd epymarl
pip install --upgrade pip
pip install -r requirements.txt
pip install -r env_requirements.txt
```

For specific package issues, check versions:

```bash
pip list | grep <package-name>
```

### Common Error Messages

| Error | Cause | Solution |
|-------|-------|----------|
| `UnityTimeOutException` | Unity not running or wrong port | Start Unity first, verify port 5004 |
| `sacred.config.ConfigError` | Missing config file | Check path to YAML config files |
| `torch.cuda.OutOfMemoryError` | GPU memory exhausted | Reduce `batch_size` or use CPU |
| `RuntimeError: CUDA not available` | PyTorch CPU-only install | Install PyTorch with CUDA support |

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

**Critical Finding:**
The large gap between training returns (207.96) and test returns (0.02-49.29) indicates that agents are not learning an effective policy. High training returns appear to result from random exploration (epsilon-greedy) rather than learned behavior. When tested with pure greedy policy (epsilon=0), agents perform minimal useful actions.

## Known Issues

### Training vs Test Performance Gap

**Issue**: Agents show significantly higher returns during training (with epsilon-greedy exploration) compared to testing (pure greedy policy).

**Observed Behavior**:
- Training returns: 13.6 → 207.96 (steadily increasing)
- Test returns: 0.02 → 0.08 (consistently near zero throughout training)
- Final evaluation with greedy policy: 0.21 return (agents barely move)

**Root Cause**: Agents are not learning an effective task policy. High training returns result from random exploration finding occasional rewards, not from learned coordinated behavior. The learned Q-values do not generalize to effective greedy action selection.

**Hypothesis Testing**:

To confirm that agents rely entirely on random exploration rather than learned Q-values, we conducted controlled evaluation experiments:

| Test Configuration | Test Return | Interpretation |
|-------------------|-------------|----------------|
| **Training** (epsilon 1.0→0.1) | 207.96 | Baseline performance with exploration |
| **Evaluation epsilon=0.0** (pure greedy) | **0.21** | Learned Q-values produce no useful behavior |
| **Evaluation epsilon=0.1** (10% random) | **191.22 - 253.52** | Random actions restore performance |

**Key Finding**: Adding just 10% random exploration during evaluation resulted in a **904-1207x improvement** over pure greedy evaluation. This proves that:

1. The Q-network's learned values don't encode useful warehouse task behavior
2. All task performance comes from randomly stumbling upon packages and goals
3. Agents never learned coordinated pick-and-place strategies

If agents had learned properly, we would expect:
- Pure greedy (epsilon=0.0) performance to match or exceed epsilon=0.1 performance
- Only a small gap between training and test returns
- Improving performance as epsilon decreases during training

Instead, performance **collapses completely** without randomness, confirming the Q-network learned nothing meaningful after 350k training steps.

**Potential Solutions** (under investigation):
1. **Reward shaping**: Current sparse rewards may not provide sufficient learning signal
2. **Observation space**: Agents may lack critical environmental information
3. **Hyperparameter tuning**: Learning rate, network capacity, or exploration schedule may need adjustment
4. **Curriculum learning**: Start with simpler tasks and gradually increase complexity
5. **Intrinsic motivation**: Add exploration bonuses or curiosity-driven rewards

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

## Deliverables

### Dre's Deliverables

### Lian's Deliverables

- [Deliverable 1](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/LiansDeliverables/D1/Lian%20Deliverable%201.html)
- [Deliverable 2](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/LiansDeliverables/D2/Lian%20Deliverable%202.html)
- [Deliverable 3](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/LiansDeliverables/D3/Lian%20Deliverable%203.html)
- [Deliverable 4](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/LiansDeliverables/D4/Lian%20Deliverable%204.html)
- [Deliverable 5](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/LiansDeliverables/D5/Lian%20Deliverable%205.html)


### Price's Deliverables

### Salmon's Deliverables

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
