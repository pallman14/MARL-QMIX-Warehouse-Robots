# AI Assistance Disclosure

This project utilized Large Language Model (LLM) assistance throughout development. All experimental decisions, algorithm selection, and research conclusions were made by team members. Below is a detailed breakdown of AI tool usage by team member and project component.

## Team Member AI Usage

### Price Allman (Claude)

- **Unity Environment**: Debugging C# scripts for QMIXWarehouseAgent, LIDAR sensor implementation, ML-Agents communication setup
- **EPyMARL Integration**: Unity wrapper development (`unity_wrapper.py`), troubleshooting Python-Unity connection issues
- **IPPO Implementation**: Hyperparameter tuning guidance, understanding macro-actions and behavioral cloning concepts
- **Documentation**: README structure, running-trained-models guide, research paper editing and conciseness improvements
- **Code Debugging**: Resolving CUDA tensor errors, Sacred syntax issues, checkpoint loading problems

### Dre Simmons (ChatGPT)

- **QMIX on RWARE**: Understanding QMIX architecture, debugging training failures with default hyperparameters
- **Hyperparameter Analysis**: Guidance on epsilon annealing schedules, buffer sizing, batch size tuning
- **Quarto Deliverables**: Formatting weekly reports, explaining training metrics (return mean, TD error, Q-values)
- **Code Explanation**: Understanding EPyMARL codebase structure, RNN agent architecture

### Lian Thang (ChatGPT)

- **MASAC Implementation**: Multi-Agent Soft Actor-Critic algorithm understanding, centralized critic setup
- **MPE Environment**: PettingZoo Simple Spread configuration, debugging agent coordination issues
- **Performance Visualization**: Creating comparison plots across MPE, RWARE, and Unity environments
- **Quarto Deliverables**: Formatting training curves, interpreting learning progression

### Salmon Riaz (ChatGPT)

- **Research Paper**: Literature review assistance, methodology section writing, results compilation
- **Windows Server Deployment**: Environment setup troubleshooting, CUDA configuration on Windows Server 2022
- **Team Coordination**: Compiling weekly reports from team members, presentation preparation
- **Scaling Analysis**: Understanding joint action space complexity, interpreting performance degradation patterns

## Project Component AI Usage Summary

| Component | AI Assistance Areas |
|-----------|---------------------|
| **Unity Environment** | C# debugging, ML-Agents setup, sensor implementation |
| **EPyMARL/QMIX** | Wrapper development, hyperparameter tuning, training debugging |
| **RWARE Environment** | Configuration, reward interpretation, episode structure |
| **MPE Environment** | Simple Spread setup, MASAC implementation guidance |
| **Research Paper** | Writing assistance, LaTeX formatting, citation verification |
| **Quarto Book** | Markdown formatting, plot generation, deliverable structure |
| **Documentation** | README writing, code comments, setup instructions |

## What AI Was NOT Used For

- Algorithm selection decisions (team chose QMIX based on experimental results)
- Experimental design and hypothesis formation
- Interpretation of results and research conclusions
- Running actual training experiments
- Data collection and metric logging
- Final research decisions and paper conclusions
