"""
Unity ML-Agents to EPyMARL Environment Wrapper
Adapts Unity warehouse environment to EPyMARL's interface
"""

import numpy as np
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.base_env import ActionTuple


class UnityWarehouseEnv:
    """
    Wrapper to make Unity warehouse compatible with EPyMARL
    Follows SMAC-like interface
    """

    def __init__(self, env_path=None, no_graphics=False, time_scale=20.0, worker_id=0):
        """
        Args:
            env_path: Path to Unity executable (None for Editor)
            no_graphics: Run headless
            time_scale: Unity time scale (higher = faster)
            worker_id: For parallel environments
        """
        self.engine_channel = EngineConfigurationChannel()

        if env_path:
            self.unity_env = UnityEnvironment(
                file_name=env_path,
                worker_id=worker_id,
                side_channels=[self.engine_channel],
                no_graphics=no_graphics
            )
        else:
            # Connect to running Unity Editor
            self.unity_env = UnityEnvironment(
                worker_id=worker_id,
                side_channels=[self.engine_channel],
                no_graphics=no_graphics,
                timeout_wait=300  # Wait up to 5 minutes for connection
            )

        self.engine_channel.set_configuration_parameters(time_scale=time_scale)
        self.unity_env.reset()

        # Get behavior name
        self.behavior_name = list(self.unity_env.behavior_specs)[0]
        self.spec = self.unity_env.behavior_specs[self.behavior_name]

        # Step once to get agents to request decisions
        self.unity_env.step()

        # Get environment info from the actual agents in the scene
        decision_steps, _ = self.unity_env.get_steps(self.behavior_name)
        self.n_agents = len(decision_steps)  # Actual number of agents

        if self.n_agents == 0:
            # Fallback: assume 4 agents for warehouse
            print("Warning: No agents found in first step, using default n_agents=4")
            self.n_agents = 4

        self.n_actions = self.spec.action_spec.discrete_branches[0]  # Should be 6
        self.obs_shape = self.spec.observation_specs[0].shape[0] if len(self.spec.observation_specs) > 0 else 47

        self.episode_limit = 1000  # Max steps per episode
        self._episode_count = 0
        self._episode_steps = 0
        self._total_steps = 0

        print(f"Unity Warehouse Environment initialized:")
        print(f"  Agents: {self.n_agents}")
        print(f"  Actions: {self.n_actions}")
        print(f"  Observation shape: {self.obs_shape}")

    def reset(self):
        """Reset the environment"""
        self.unity_env.reset()
        self._episode_steps = 0
        self._episode_count += 1

        decision_steps, terminal_steps = self.unity_env.get_steps(self.behavior_name)

        # Return initial observations
        return self.get_obs(), self.get_state()

    def step(self, actions):
        """
        Execute actions for all agents
        Args:
            actions: List of action indices, one per agent
        Returns:
            reward: Team reward
            terminated: Episode ended
            info: Additional info dict
        """
        # Convert actions to Unity format
        action_tuple = ActionTuple()
        action_tuple.add_discrete(np.array(actions).reshape(-1, 1))

        # Set actions for all agents
        decision_steps, terminal_steps = self.unity_env.get_steps(self.behavior_name)
        self.unity_env.set_actions(self.behavior_name, action_tuple)

        # Step environment
        self.unity_env.step()

        # Get results
        decision_steps, terminal_steps = self.unity_env.get_steps(self.behavior_name)

        # Calculate team reward (sum of individual rewards)
        reward = 0.0
        if len(decision_steps) > 0:
            reward = np.sum(decision_steps.reward)
        if len(terminal_steps) > 0:
            reward += np.sum(terminal_steps.reward)

        # Check if episode is done
        self._episode_steps += 1

        # Terminated = episode ended naturally (e.g., all packages delivered)
        # Truncated = episode cut short by time limit
        terminated = len(terminal_steps) > 0
        truncated = self._episode_steps >= self.episode_limit and not terminated

        self._total_steps += 1

        # Get new observations
        obs = self.get_obs()

        info = {
            "episode_steps": self._episode_steps,
            "total_steps": self._total_steps
        }

        return obs, reward, terminated, truncated, info

    def get_obs(self):
        """
        Get observations for all agents
        Returns:
            List of observations, one per agent
        """
        decision_steps, terminal_steps = self.unity_env.get_steps(self.behavior_name)

        if len(decision_steps) > 0:
            # Get observations from decision steps
            obs = decision_steps.obs[0]  # Assuming vector observations
            return [obs[i] for i in range(len(obs))]
        else:
            # Return zeros if no agents (shouldn't happen normally)
            return [np.zeros(self.obs_shape) for _ in range(self.n_agents)]

    def get_obs_agent(self, agent_id):
        """Get observation for specific agent"""
        obs_list = self.get_obs()
        if agent_id < len(obs_list):
            return obs_list[agent_id]
        return np.zeros(self.obs_shape)

    def get_obs_size(self):
        """Return observation size"""
        return self.obs_shape

    def get_state(self):
        """
        Get global state for QMIX
        This should match QMIXWarehouseEnvironment.GetGlobalState()
        """
        # For now, concatenate all agent observations
        # In full implementation, get true global state from Unity
        obs = self.get_obs()
        state = np.concatenate(obs)
        return state

    def get_state_size(self):
        """Return global state size"""
        return self.obs_shape * self.n_agents

    def get_avail_actions(self):
        """
        Get available actions for all agents
        Returns list of available actions (1 = available, 0 = not available)
        For warehouse, all actions always available
        """
        return [[1] * self.n_actions for _ in range(self.n_agents)]

    def get_avail_agent_actions(self, agent_id):
        """Get available actions for specific agent"""
        return [1] * self.n_actions

    def get_total_actions(self):
        """Return number of actions"""
        return self.n_actions

    def get_stats(self):
        """Return environment statistics"""
        return {}

    def get_env_info(self):
        """Return environment information (required by EPyMARL)"""
        env_info = {
            "state_shape": self.get_state_size(),
            "obs_shape": self.get_obs_size(),
            "n_actions": self.get_total_actions(),
            "n_agents": self.n_agents,
            "episode_limit": self.episode_limit
        }
        return env_info

    def close(self):
        """Close the environment"""
        self.unity_env.close()

    def seed(self, seed):
        """Set random seed"""
        np.random.seed(seed)

    def render(self):
        """Render (no-op for Unity)"""
        pass

    def save_replay(self):
        """Save replay (not implemented)"""
        pass


# EPyMARL expects a function that returns the environment
def make_unity_warehouse_env(**kwargs):
    """Factory function for creating Unity warehouse environment"""
    return UnityWarehouseEnv(**kwargs)


if __name__ == "__main__":
    # Test the wrapper
    print("Testing Unity Warehouse Environment Wrapper...")

    env = UnityWarehouseEnv(no_graphics=False)
    env_info = env.get_env_info()

    print("\nEnvironment Info:")
    for key, value in env_info.items():
        print(f"  {key}: {value}")

    print("\nRunning test episode...")
    env.reset()

    for step in range(10):
        # Random actions
        actions = [np.random.randint(0, env.n_actions) for _ in range(env.n_agents)]
        reward, terminated, info = env.step(actions)

        print(f"Step {step}: Reward={reward:.3f}, Terminated={terminated}")

        if terminated:
            print("Episode ended")
            break

    env.close()
    print("\nTest complete!")
