import json
import matplotlib.pyplot as plt
from datetime import datetime, timezone
import numpy as np
import os

# --- Configuration ---
METRICS_FILE = 'metrics.json'
INFO_FILE = 'info.json'
# Easily change this variable to update plot titles, the output directory name, and all file prefixes.
EPISODE_COUNT = 5001 

def load_json(file_path):
    """Loads JSON data from a file."""
    if not os.path.exists(file_path):
        return None
    try:
        with open(file_path, 'r') as f:
            return json.load(f)
    except Exception:
        return None

def extract_numpy_values(data_list):
    """
    Extracts the float 'value' from a list of nested NumPy objects 
    (which appear in info.json) or returns the value directly.
    """
    values = []
    for item in data_list:
        if isinstance(item, dict) and 'value' in item and 'py/object' in item:
            values.append(item['value'])
        elif isinstance(item, (float, int)):
             values.append(item)
        else:
            try:
                values.append(float(item))
            except (ValueError, TypeError, json.JSONDecodeError):
                values.append(np.nan)
    return values

def plot_returns_vs_steps(info_data, output_file, episode_count):
    """Generates a plot for return mean (train/test) vs. total steps."""
    
    try:
        train_returns = extract_numpy_values(info_data['return_mean'])
        train_steps = info_data['return_mean_T']
    except KeyError:
        return 

    try:
        test_returns = extract_numpy_values(info_data['test_return_mean'])
        test_steps = info_data['test_return_mean_T']
    except KeyError:
        test_returns = None
        test_steps = None

    plt.figure(figsize=(12, 6))
    
    if train_returns and train_steps and len(train_returns) == len(train_steps):
        plt.plot(train_steps, train_returns, label='Training Return Mean', color='blue', alpha=0.7)
    
    if test_returns and test_steps and len(test_returns) == len(test_steps):
        plt.plot(test_steps, test_returns, label='Test Return Mean', color='red', linewidth=2)
    
    plt.title(f'QMIX Learning Progress ({episode_count} Episodes): Return Mean vs. Total Steps')
    plt.xlabel('Total Steps (T)')
    plt.ylabel('Mean Return')
    plt.grid(True, linestyle='--', alpha=0.6)
    plt.legend()
    plt.tight_layout()
    plt.savefig(output_file)
    plt.close()
    return f"Plot saved to {output_file}"

def plot_single_metric_vs_steps(info_data, metric_key, step_key, output_file, title, y_label, color, episode_count):
    """Generates a plot for a single metric vs. total steps using info.json."""
    
    try:
        metric_values = extract_numpy_values(info_data[metric_key])
        metric_steps = info_data[step_key]
    except KeyError:
        return

    plt.figure(figsize=(12, 6))
    
    if metric_values and metric_steps and len(metric_values) == len(metric_steps):
        plt.plot(metric_steps, metric_values, label=title, color=color, alpha=0.7)
    
    plt.title(f'QMIX Training Progress ({episode_count} Episodes): {title} vs. Total Steps')
    plt.xlabel('Total Steps (T)')
    plt.ylabel(y_label)
    plt.grid(True, linestyle='--', alpha=0.6)
    plt.legend()
    plt.tight_layout()
    plt.savefig(output_file)
    plt.close()
    return f"Plot saved to {output_file}"


def plot_return_vs_time(metrics_data, output_file, episode_count):
    """Generates a plot for return mean vs. elapsed time using metrics.json."""
    
    try:
        data = metrics_data['return_mean']
        timestamps_str = data['timestamps']
        return_values = data['values']
    except KeyError:
        return 
    
    try:
        timestamps = []
        for ts_str in timestamps_str:
            ts_str = ts_str.replace('Z', '+00:00')
            try:
                ts = datetime.fromisoformat(ts_str)
            except ValueError:
                ts = datetime.strptime(ts_str.split('.')[0], "%Y-%m-%dT%H:%M:%S").replace(tzinfo=timezone.utc)
            
            if ts.tzinfo is None:
                ts = ts.replace(tzinfo=timezone.utc)
            timestamps.append(ts)
        
        if not timestamps:
            return

        start_time = timestamps[0]
        elapsed_seconds = [(ts - start_time).total_seconds() for ts in timestamps]
        elapsed_hours = [sec / 3600 for sec in elapsed_seconds]
    except Exception:
        return

    plt.figure(figsize=(12, 6))
    
    if elapsed_hours and return_values and len(elapsed_hours) == len(return_values):
        plt.plot(elapsed_hours, return_values, label='Training Return Mean', color='green')
    
    plt.title(f'QMIX Training Return Mean ({episode_count} Episodes) vs. Elapsed Time')
    plt.xlabel('Elapsed Time (Hours)')
    plt.ylabel('Mean Return')
    plt.grid(True, linestyle='--', alpha=0.6)
    plt.legend()
    plt.tight_layout()
    plt.savefig(output_file)
    plt.close()
    return f"Plot saved to {output_file}"


# --- Main Execution ---
if __name__ == "__main__":
    metrics_data = load_json(METRICS_FILE)
    info_data = load_json(INFO_FILE)

    # 1. Define and create the output directory based only on EPISODE_COUNT
    OUTPUT_DIR = str(EPISODE_COUNT)
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    results = []

    # 2. Define output file names using EPISODE_COUNT as a prefix, and create full paths
    # Note: Using EPISODE_COUNT variable as prefix and directory name, as requested.
    step_plot_name = f"{EPISODE_COUNT}_return_mean_vs_steps.png"
    time_plot_name = f"{EPISODE_COUNT}_return_mean_vs_time.png"
    loss_plot_name = f"{EPISODE_COUNT}_loss_vs_steps.png"
    epsilon_plot_name = f"{EPISODE_COUNT}_epsilon_vs_steps.png"
    td_error_plot_name = f"{EPISODE_COUNT}_td_error_abs_vs_steps.png"
    q_mean_plot_name = f"{EPISODE_COUNT}_q_taken_mean_vs_steps.png"
    return_std_plot_name = f"{EPISODE_COUNT}_return_std_vs_steps.png"
    # NEW METRIC: Episode Length Mean
    ep_length_plot_name = f"{EPISODE_COUNT}_ep_length_mean_vs_steps.png" 

    step_plot_path = os.path.join(OUTPUT_DIR, step_plot_name)
    time_plot_path = os.path.join(OUTPUT_DIR, time_plot_name)
    loss_plot_path = os.path.join(OUTPUT_DIR, loss_plot_name)
    epsilon_plot_path = os.path.join(OUTPUT_DIR, epsilon_plot_name)
    td_error_plot_path = os.path.join(OUTPUT_DIR, td_error_plot_name)
    q_mean_plot_path = os.path.join(OUTPUT_DIR, q_mean_plot_name)
    return_std_plot_path = os.path.join(OUTPUT_DIR, return_std_plot_name)
    ep_length_plot_path = os.path.join(OUTPUT_DIR, ep_length_plot_name)


    if info_data:
        # 1. Performance
        results.append(plot_returns_vs_steps(info_data, step_plot_path, EPISODE_COUNT))
        
        # 2. Learning Dynamics
        results.append(plot_single_metric_vs_steps(info_data, 'loss', 'loss_T', loss_plot_path, 'Loss', 'Loss', color='purple', episode_count=EPISODE_COUNT))
        results.append(plot_single_metric_vs_steps(info_data, 'td_error_abs', 'td_error_abs_T', td_error_plot_path, 'TD Error Absolute', 'Absolute TD Error', color='brown', episode_count=EPISODE_COUNT))
        results.append(plot_single_metric_vs_steps(info_data, 'q_taken_mean', 'q_taken_mean_T', q_mean_plot_path, 'Q Taken Mean', 'Mean Q Value', color='teal', episode_count=EPISODE_COUNT))
        
        # 3. Stability and Exploration
        results.append(plot_single_metric_vs_steps(info_data, 'return_std', 'return_std_T', return_std_plot_path, 'Return Standard Deviation', 'Std Dev of Return', color='gray', episode_count=EPISODE_COUNT))
        results.append(plot_single_metric_vs_steps(info_data, 'epsilon', 'epsilon_T', epsilon_plot_path, r'Epsilon ($\epsilon$)', 'Epsilon Value', color='orange', episode_count=EPISODE_COUNT))
        
        # 4. NEW METRIC: Episode Length
        results.append(plot_single_metric_vs_steps(info_data, 'ep_length_mean', 'ep_length_mean_T', ep_length_plot_path, 'Episode Length Mean', 'Mean Episode Length', color='blue', episode_count=EPISODE_COUNT))
    
    if metrics_data:
        # 5. Time-based Performance
        results.append(plot_return_vs_time(metrics_data, time_plot_path, EPISODE_COUNT))

    print(f"\nAll plots saved to directory: {OUTPUT_DIR}")