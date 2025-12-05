# MARL-QMIX-Warehouse-Robots

Multi-Agent Reinforcement Learning using QMIX algorithm for training cooperative warehouse robots in Unity ML-Agents environment.

## Overview

The MARL-QMIX-Warehouse-Robots project focuses on multi-agent reinforcement learning (MARL) within a cooperative warehouse environment built in Unity. The system trains multiple autonomous robots using the QMIX value-factorization algorithm. The project integrates Unity ML-Agents for simulation and EPyMARL for multi-agent learning.

### Key Features

- **QMIX Algorithm**: Value-based MARL with centralized training and decentralized execution
- **Unity ML-Agents 4.0**: Integration for physics-based warehouse simulation
- **EPyMARL Framework**: Extended PyMARL for multi-agent training
- **Procedural Generation**: Randomized warehouse layouts with configurable dimensions
- **Grid-Based Navigation**: Discrete action space for robot movement and interaction

## Quarto Book

View the complete team deliverables compiled in a Quarto Book format:

**[MARL Warehouse Robots - Team Deliverables Book](https://pallman14.github.io/MARL-QMIX-Warehouse-Robots/)**

## arXiv Paper

**[Multi-Agent Reinforcement Learning for Cooperative Warehouse Automation: QMIX Value Decomposition for Sparse-Reward Coordination](https://arxiv.org/html/2512.04463v1)**

### Performance

**Note**: Initial training runs show a significant gap between training and test performance, indicating the agents have not yet learned an effective policy. Current best run (#93) achieved **207.96 training return** but only **0.21 test return** in pure greedy evaluation, suggesting the high training returns come primarily from epsilon-greedy exploration rather than learned behavior. This is an active area of investigation - see [Known Issues](#training-vs-test-performance-gap) for details.

## Repository Structure

```
MARL-QMIX-Warehouse-Robots/
├── docs/                             # GitHub Pages (Quarto Book output)
├── paper/                            # arXiv paper (LaTeX source + PDF)
├── QuartoBook/                       # Quarto Book source files
├── epymarl/                          # Multi-agent RL framework
│   ├── src/                          # Python source code
│   │   ├── config/algs/              # Algorithm configurations
│   │   ├── envs/                     # Environment wrappers
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
├── DresDeliverables/                 # Dre's weekly deliverables
├── LiansDeliverables/                # Lian's weekly deliverables
├── PricesDeliverables/               # Price's weekly deliverables
├── SalmonsDeliverables/              # Salmon's weekly deliverables
├── com.unity.perception/             # Unity Perception package
├── com.unity.simulation.capture/     # Unity Simulation Capture package
├── com.unity.robotics.warehouse.base/  # Shared warehouse code
└── com.unity.robotics.warehouse.urp/   # URP-specific warehouse package
```

## Installation

### Prerequisites

- **Python**: 3.8-3.10
- **Unity**: Unity 6
- **OS**: Linux, macOS, or Windows
- **RAM**: 8GB+ recommended
- **CUDA** (optional): For GPU training

### Setup Steps

#### STEP 1. Installing Python (First-Time Setup) - If not needed, skip down to 'Install Unity Hub + Unity Editor'

This project uses **Python 3.9** and has been tested with that version.  
If you’ve never installed Python before, follow the steps below for your operating system.

---

### Check if Python is already installed

Open a terminal on Mac (or Command Prompt on Windows) and run:

**macOS / Linux Users:**
```bash
python3 --version
```
**Windows Users:**
```bash
py --version
```
If you see something like Python 3.9.x, you’re good to go and can skip to "Clone Repository” below.
If you get an error or see a version lower than 3.8, follow the install steps next:

--- 

**Install Python on macOS**
Go to: https://www.python.org/downloads/
Download the latest Python 3.9.x installer for macOS.

Open the .pkg file and run through the installer using the default options.
When it finishes, close and re-open your Terminal, then run:

```bash
python3 --version
```

You should see something like python 3.9.x, if you do then python is installed correctly.

**Install Python on Windows**
Go to: https://www.python.org/downloads/
Download the latest Python 3.9.x installer for Windows.

Run the installer and make sure to check the box:
“Add Python 3.9 to PATH”
Choose “Install Now” and let it finish.
Open Command Prompt and run:

```bash
py --version
```

You should see python 3.9.x. If you do, Python is installed correctly.

**NOTE: This project must be run with Python 3.8, 3.9, or 3.10.**
- Versions 3.11+ (including 3.12, 3.13, 3.14) are not supported and will cause installation failures.

**Why these versions are required**
Several core libraries used in this project only provide stable builds for Python 3.8–3.10, including:
- Unity ML-Agents (mlagents_envs 0.30.0) — officially supports Python 3.8–3.10 only.
- SMAC, SMACv2, SMACLITE, matrix-games — multi-agent RL environments built for Python 3.8–3.10.
- PyTorch — ARM-compatible wheels for this project’s version are stable on Python 3.9–3.10.
- gym / gymnasium / pysc2 — older RL environments that depend on numpy versions incompatible with Python 3.11+.

Because these packages do not publish wheels for newer versions of Python, attempting to install on Python 3.11+ results in:
- missing dependencies
- syntax errors
- incompatible numpy/scipy versions
- ML-Agents failing to import 

---

#### STEP 2. Install Unity Hub + Unity Editor (Step-by-Step Guide - With Processor Check)

- This project includes a full Unity environment (`WarehouseProjectURP`). This folder only exists after you clone the repo (this will occur during steps 3).
- You must install the correct Unity editor for your computer’s processor. Unity provides separate installers for:

- **Intel (x86_64) Macs**
- **Apple Silicon (ARM64 / M1 / M2 / M3) Macs**
- **Windows PCs**

Follow these steps exactly.

---

## 1. Check Your Computer’s Processor (IMPORTANT)

Before downloading Unity, determine which processor you have.

### macOS:
Click the Apple logo → **About This Mac**

Look for:

- **Chip: Apple M1 / M2 / M3** → *You have Apple Silicon (ARM64)*
- **Processor: Intel** → *You have an Intel Mac*

### Windows:
Settings → System → About → Processor  
(It will say Intel, AMD, etc.)

You MUST download the correct Unity version for your architecture.

---

## 2. Go to the Unity Download Page

Open this link:

👉 **https://unity.com/download**

You’ll see a big button:

**Download Unity Hub**

Click it.

---

## 3. Install Unity Hub

Unity Hub manages Unity versions and projects.

### macOS:
- If your Mac is **Apple Silicon** → Unity Hub will auto-detect and install the ARM64 version.
- If your Mac is **Intel** → It will install the Intel version automatically.

Just follow the installer instructions.

### Windows:
Run the `.exe` installer and complete setup.

---

## 4. Open Unity Hub

When Unity Hub opens:

- Sign in (or create a free account)
- Go to the **Installs** tab

---

## 5. Install the Correct Unity Version for This Project

Unity projects require a specific Unity version. In this case it would be version Unity 6.x (6000.x). If you install a version that is not compatible, you will get a warning.

**Let Unity finish installing the editor.**

At this stage, you’ve installed Unity Hub and a Unity Editor, but you still don’t have the 'MARL-QMIX-Warehouse-Robots' folder yet. That comes next when you clone the repository. 


#### STEP 3. Clone Repository (Create the Project Folder)

Now we pull the GitHub project to your machine. This step creates the 'MARL-QMIX-Warehouse-Robots' folder that we’ll later open in Unity.

Open up a terminal and enter the following command lines:

```bash
git clone https://github.com/pallman14/MARL-QMIX-Warehouse-Robots.git
cd MARL-QMIX-Warehouse-Robots
```
After this, your filesystem will contain `MARL-QMIX-Warehouse-Robots` and will open that directory.

**If you want to clone only the project files needed to save storage space, do the following:**

```bash
git clone --no-checkout --depth=1 --filter=blob:none https://github.com/pallman14/MARL-QMIX-Warehouse-Robots.git MARL-QMIX-Warehouse-Robots
cd MARL-QMIX-Warehouse-Robots
git sparse-checkout set WarehouseProjectURP com.unity.perception com.unity.robotics.warehouse.base com.unity.robotics.warehouse.urp com.unity.simulation.capture epymarl .gitignore README.md
git checkout
```
#### STEP 4. Create Python Virtual Environment

Once in the MARL-QMIX-Warehouse-Robots folder, we create a virtual enviornment:

```bash
python3 -m venv epymarl_env
source epymarl_env/bin/activate  # On Windows: epymarl_env\Scripts\activate
```
NOTE: We create a virual environment to isolate spaces for each project, preventing package and version conflicts between them. This ensures that each project can have its own specific set of dependencies, making development more reliable and projects easier to reproduce and share.

#### 5. Install Python Dependencies

```bash
cd epymarl
pip install --upgrade pip
pip install -r requirements.txt
pip install -r env_requirements.txt
```
**NOTE:**
This installs all necessary:
- Core MARL libraries (PyTorch, gym/gymnasium, Sacred)
- Environment libraries (RWARE, SMAC, SMACv2, pysc2, etc.)
- Unity Python API (mlagents_envs==0.30.0 if used/required)

#### STEP5. Add the Unity Project to Unity Hub

1. Open Unity Hub
2. On the left-side click `Projects` 
3. On the right-side click `Add` and select `Add project from disk`
4. Go to `MARL-QMIX-Warehouse-Robots` if not there already, double-click to go inside folder and single-click to highlight `WarehouseProjectURP` and then click open.
5. Install the correct editor (Unity 6) - go with recommended version
6. Wait for package import and compilation
7. Open the Unity project by clicking `WarehouseProjectURP` under name to ensure no package errors occur. You will most likely see the following: This project contains one or more deprecated packages. Do you want to open Package Manager? in this case click dimiss forever
8. Once everything is installed and verified, do not close out Unity, instead move to the runtime sequence below.



#### RUNTIME SEQUENCE

Open terminal/command prompt and run:

```bash
cd MARL-QMIX-Warehouse-Robots/epymarl
source ../epymarl_env/bin/activate
python src/main.py --config=qmix_warehouse_improved --env-config=unity_warehouse with t_max=500000
```

You should see this message:
`[INFO] Listening on port 5004. Start training by pressing the Play button in the Unity Editor.`

This means Python is waiting for unity to connect.

Do NOT open Unity and Press Play before this step. Unity must wait for Python.

## Step 2 - Open Unity SECOND
1. Open Unity Hub
2. Launch project:
MARL-QMIX-Warehouse-Robots/WarehouseProjectURP/
3. Inside Unity, make sure the `Assets` folder on the bottom left under `Project` is open then double click `Scenes` and then click `Warehouse`

## Step 3 - Start Unity Play Mode LAST
Click `Play` toward the top in Unity after Python is already running.

You will now see:
- Unity logs:
  "Listening on port 5004"
- Python logs:
  "Environment connected. Starting training..."
  
Training will now run:

You can put the terminal and Unity side by side so you can see the log from the terminal as the training is in progress. In the terminal you can focus your attention on the return mean and the target mean and if you want to see the amount of packages delivered you can click `Console` in Unity and in the search bar type `delivered to`. 


#### Troubleshooting

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

### Unity Package Dependencies Issues

**Symptoms:**
- Unity console shows "Project has invalid dependencies"
- Errors about missing `com.unity.perception` or `com.unity.simulation.capture`
- Compiler errors: `The type or namespace name 'Perception' does not exist`

**Root Cause:**
The Unity packages (`com.unity.perception` and `com.unity.simulation.capture`) are included in the repository but may not be pulled correctly.

**Solution:**

1. **Pull Latest Changes**:
   ```bash
   cd /path/to/MARL-QMIX-Warehouse-Robots
   git pull origin main
   ```

2. **Verify Packages Exist Locally**:
   ```bash
   # Check if package directories exist
   ls -la com.unity.perception/
   ls -la com.unity.simulation.capture/

   # Verify package.json files are present
   ls -la com.unity.perception/package.json
   ls -la com.unity.simulation.capture/package.json
   ```

3. **If Packages Are Missing**:
   - The repository uses local file references for these packages
   - Packages should be in the root directory of the repository
   - If `git pull` doesn't fetch them, they may have been excluded by `.gitignore` (they shouldn't be)
   - Check your `.gitignore` file to ensure `*.json` files aren't excluded

4. **Refresh Unity**:
   After pulling, completely close Unity and reopen it:
   ```bash
   # macOS/Linux: Kill Unity processes
   pkill -9 Unity

   # Windows: Close Unity through Task Manager
   ```
   Then reopen the Unity project at: `Robotics-Warehouse-main/WarehouseProjectURP/`

5. **Clear Unity Package Cache** (if packages still not recognized):
   ```bash
   # Close Unity first, then:
   rm -rf Library/PackageCache
   rm -rf Library/Artifacts
   ```
   Reopen Unity and let it reimport all packages.

6. **Verify manifest.json** (last resort):
   Open `WarehouseProjectURP/Packages/manifest.json` and verify these entries exist:
   ```json
   {
     "dependencies": {
       "com.unity.perception": "file:../../com.unity.perception",
       "com.unity.simulation.capture": "file:../../com.unity.simulation.capture",
       ...
     }
   }
   ```
   The paths should be relative to the `Packages/` directory (two levels up from manifest location).

**Note:** These packages contain 831 files (~100k lines) and are required for the warehouse environment randomization and simulation capture.

### Common Error Messages

| Error | Cause | Solution |
|-------|-------|----------|
| `UnityTimeOutException` | Unity not running or wrong port | Start Unity first, verify port 5004 |
| `sacred.config.ConfigError` | Missing config file | Check path to YAML config files |
| `torch.cuda.OutOfMemoryError` | GPU memory exhausted | Reduce `batch_size` or use CPU |
| `RuntimeError: CUDA not available` | PyTorch CPU-only install | Install PyTorch with CUDA support |
| `Project has invalid dependencies` | Missing Unity packages | Pull latest changes, verify `com.unity.perception` and `com.unity.simulation.capture` directories exist |
| `Perception does not exist in namespace` | Unity packages not loaded | Close/reopen Unity, clear package cache if needed |

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

- [Deliverable 1](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/DresDeliverables/Week1-5/W1D/W1D_Simmons.html)
- [Deliverable 2](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/DresDeliverables/Week1-5/W2D/W2D_Simmons.html)
- [Deliverable 3](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/DresDeliverables/Week1-5/W3D/W3D_Simmons.html)
- [Deliverable 4](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/DresDeliverables/Week1-5/W4D/W4D_Simmons.html)
- [Deliverable 5](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/DresDeliverables/Week1-5/W5D/W5D_Simmons.html)

### Lian's Deliverables

- [Deliverable 1](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/LiansDeliverables/D1/Lian%20Deliverable%201.html)
- [Deliverable 2](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/LiansDeliverables/D2/Lian%20Deliverable%202.html)
- [Deliverable 3](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/LiansDeliverables/D3/Lian%20Deliverable%203.html)
- [Deliverable 4](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/LiansDeliverables/D4/Lian%20Deliverable%204.html)
- [Deliverable 5](https://htmlpreview.github.io/?https://github.com/pallman14/MARL-QMIX-Warehouse-Robots/blob/main/LiansDeliverables/D5/Lian%20Deliverable%205.html)


### Price's Deliverables

- [Deliverable 1](https://pallman14.github.io/MARL-QMIX-Warehouse-Robots/price/week1.html)
- [Deliverable 2](https://pallman14.github.io/MARL-QMIX-Warehouse-Robots/price/week2.html)
- [Deliverable 3](https://pallman14.github.io/MARL-QMIX-Warehouse-Robots/price/week3.html)
- [Deliverable 4](https://pallman14.github.io/MARL-QMIX-Warehouse-Robots/price/week4.html)
- [Deliverable 5](https://pallman14.github.io/MARL-QMIX-Warehouse-Robots/price/week5.html)

### Salmon's Deliverables

- [Deliverable 1](https://pallman14.github.io/MARL-QMIX-Warehouse-Robots/salmon/week1.html)
- [Deliverable 2](https://pallman14.github.io/MARL-QMIX-Warehouse-Robots/salmon/week2.html)
- [Deliverable 3](https://pallman14.github.io/MARL-QMIX-Warehouse-Robots/salmon/week3.html)
- [Deliverable 4](https://pallman14.github.io/MARL-QMIX-Warehouse-Robots/salmon/week4.html)
- [Deliverable 5](https://pallman14.github.io/MARL-QMIX-Warehouse-Robots/salmon/week5.html)

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

## AI Assistance Disclosure

This project utilized Large Language Model (LLM) assistance for code explanation, overall clarification of complex MARL concepts, and help with our lousy writing skills. All code implementation, experiments, and research decisions were made by the team members.
