"""
Register Unity Warehouse environment with EPyMARL
Place this in: epymarl/src/envs/
"""

from .multiagentenv import MultiAgentEnv
from .unity_wrapper import UnityWarehouseEnv


class UnityWarehouse(MultiAgentEnv):
    """
    Unity Warehouse environment for EPyMARL
    Wraps UnityWarehouseEnv to match EPyMARL's MultiAgentEnv interface
    """

    def __init__(self, **kwargs):
        # Extract Unity-specific args
        env_path = kwargs.get("env_path", None)
        no_graphics = kwargs.get("no_graphics", False)
        time_scale = kwargs.get("time_scale", 20.0)
        worker_id = kwargs.get("worker_id", 0)

        # Create Unity environment
        self.env = UnityWarehouseEnv(
            env_path=env_path,
            no_graphics=no_graphics,
            time_scale=time_scale,
            worker_id=worker_id
        )

        # Store environment info
        self._env_info = self.env.get_env_info()
        self.n_agents = self._env_info["n_agents"]
        self.n_actions = self._env_info["n_actions"]
        self.episode_limit = self._env_info["episode_limit"]

    def step(self, actions):
        """Take a step in the environment"""
        obs, reward, terminated, truncated, info = self.env.step(actions)
        # Return 5 values to match Gymnasium API
        return obs, reward, terminated, truncated, info

    def get_obs(self):
        """Get observations for all agents"""
        return self.env.get_obs()

    def get_obs_agent(self, agent_id):
        """Get observation for a specific agent"""
        return self.env.get_obs_agent(agent_id)

    def get_obs_size(self):
        """Get observation size"""
        return self.env.get_obs_size()

    def get_state(self):
        """Get global state"""
        return self.env.get_state()

    def get_state_size(self):
        """Get global state size"""
        return self.env.get_state_size()

    def get_avail_actions(self):
        """Get available actions for all agents"""
        return self.env.get_avail_actions()

    def get_avail_agent_actions(self, agent_id):
        """Get available actions for a specific agent"""
        return self.env.get_avail_agent_actions(agent_id)

    def get_total_actions(self):
        """Get total number of actions"""
        return self.env.get_total_actions()

    def reset(self):
        """Reset the environment"""
        return self.env.reset()

    def render(self):
        """Render the environment"""
        self.env.render()

    def close(self):
        """Close the environment"""
        self.env.close()

    def seed(self, seed):
        """Set random seed"""
        self.env.seed(seed)

    def save_replay(self):
        """Save replay"""
        self.env.save_replay()

    def get_env_info(self):
        """Get environment info"""
        return self._env_info

    def get_stats(self):
        """Get environment statistics"""
        return self.env.get_stats()
