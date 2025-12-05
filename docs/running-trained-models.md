# Running Trained QMIX Models in Unity

This guide explains how to evaluate pre-trained QMIX models with the Unity warehouse environment.

## Prerequisites

- Python environment with EPyMARL dependencies installed
- Unity project (`WarehouseProjectURP/`) ready to open
- Trained model checkpoints in `epymarl/results/models/`

## Model Checkpoint Structure

Trained models are saved with the following structure:

```
epymarl/results/models/
└── qmix_seed{SEED}_{ENV}_{TIMESTAMP}/
    ├── 199/           # Checkpoint at timestep 199
    │   ├── agent.th   # Agent neural network weights
    │   ├── mixer.th   # QMIX mixer network weights
    │   └── opt.th     # Optimizer state
    ├── 100199/        # Checkpoint at timestep 100,199
    ├── 200199/
    ├── ...
    └── 1000199/       # Final checkpoint (1M steps)
```

Each numbered folder represents a training checkpoint at that timestep. The files contain:
- `agent.th` - The agent policy network (makes decisions)
- `mixer.th` - The QMIX value decomposition mixer
- `opt.th` - Optimizer state (only needed to resume training)

## How to Run a Trained Model

### Step 1: Open Unity

1. Open Unity Hub
2. Open the `WarehouseProjectURP/` project
3. Open the warehouse scene (e.g., `Assets/Scenes/Warehouse.unity`)
4. **Do not press Play yet** - wait until the Python script is running

### Step 2: Run the Evaluation Script

Open a terminal and navigate to the epymarl directory:

```bash
cd /path/to/MARL-QMIX-Warehouse-Robots/epymarl
```

Run the evaluation command:

```bash
python src/main.py \
  --config=qmix_warehouse_improved \
  --env-config=unity_warehouse \
  with checkpoint_path="results/models/qmix_seed185725254_unity_warehouse_2025-12-04_06-39-24" \
  evaluate=True \
  test_nepisode=5 \
  use_cuda=False
```

**Important:** The `with` keyword is required before config parameters (this is Sacred's syntax).

### Step 3: Connect Unity

Once you see the Python script waiting for a connection, press **Play** in the Unity Editor. The environment will connect via port 5004.

## Command Parameters Explained

| Parameter | Description |
|-----------|-------------|
| `--config=qmix_warehouse_improved` | Use QMIX algorithm configuration |
| `--env-config=unity_warehouse` | Use Unity warehouse environment settings |
| `with` | Required keyword before config parameters (Sacred syntax) |
| `checkpoint_path="..."` | Path to the model folder (containing timestep subfolders) |
| `load_step=0` | Which checkpoint to load. `0` = load the latest/max timestep |
| `evaluate=True` | Run in evaluation mode (no training) |
| `test_nepisode=5` | Number of episodes to run |
| `use_cuda=False` | Run on CPU (recommended for evaluation) |

## Example: Running Different Checkpoints

### Run the best/final model (1M steps):
```bash
python src/main.py \
  --config=qmix_warehouse_improved \
  --env-config=unity_warehouse \
  with checkpoint_path="results/models/qmix_seed185725254_unity_warehouse_2025-12-04_06-39-24" \
  evaluate=True \
  use_cuda=False
```

### Run an early checkpoint (100k steps) to compare:
```bash
python src/main.py \
  --config=qmix_warehouse_improved \
  --env-config=unity_warehouse \
  with checkpoint_path="results/models/qmix_seed185725254_unity_warehouse_2025-12-04_06-39-24" \
  load_step=100199 \
  evaluate=True \
  use_cuda=False
```

### Run more episodes for thorough evaluation:
```bash
python src/main.py \
  --config=qmix_warehouse_improved \
  --env-config=unity_warehouse \
  with checkpoint_path="results/models/qmix_seed185725254_unity_warehouse_2025-12-04_06-39-24" \
  evaluate=True \
  test_nepisode=1000 \
  use_cuda=False
```

## Available Trained Models

Based on the results folder, the following trained models are available:

| Model | Seed | Date | Checkpoints |
|-------|------|------|-------------|
| `qmix_seed185725254_unity_warehouse_2025-12-04_06-39-24` | 185725254 | Dec 4, 2025 | 199 to 1,000,199 |
| `qmix_seed462125495_unity_warehouse_2025-12-01_01-32-34` | 462125495 | Dec 1, 2025 | 199 to 1,000,199 |

## Troubleshooting

### "Checkpoint directory doesn't exist"
- Verify the `checkpoint_path` points to the correct folder
- The path should contain numbered subfolders (199, 100199, etc.)

### Unity not connecting
- Make sure Python script is running first (it waits for Unity)
- Check that no other process is using port 5004
- Ensure `timeout_wait=300` allows enough time (5 minutes)

### Agents not moving / doing nothing
- Try a later checkpoint (more training = better behavior)
- Verify the correct environment scene is loaded in Unity

## How It Works

1. **Python loads the model**: The `agent.th` and `mixer.th` files are loaded into PyTorch
2. **Unity provides the environment**: Observations, rewards, physics simulation
3. **Communication via ML-Agents**: Python and Unity communicate over port 5004
4. **Python makes decisions**: The trained model selects actions based on observations
5. **Unity executes actions**: Robots move, pick up packages, etc.

No ONNX conversion is needed because the PyTorch model runs on the Python side - Unity only handles the simulation and rendering.
