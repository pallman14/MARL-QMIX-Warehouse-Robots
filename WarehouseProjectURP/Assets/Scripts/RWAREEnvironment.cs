using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RWARE
{
    /// <summary>
    /// RWARE Environment Manager
    /// Handles grid-based warehouse environment, collision detection, and multi-agent coordination
    /// Matches RWARE environment rules and mechanics
    /// </summary>
    public class RWAREEnvironment : MonoBehaviour
    {
        [Header("Grid Configuration")]
        [Tooltip("Width of the warehouse grid (X axis)")]
        public int gridWidth = 10;

        [Tooltip("Height of the warehouse grid (Z axis)")]
        public int gridHeight = 20;

        [Tooltip("Size of each grid cell in world units")]
        public float cellSize = 1.0f;

        [Header("RWARE Configuration")]
        [Tooltip("Number of shelves to keep requested at any time")]
        public int numRequestedShelves = 2;

        [Tooltip("Maximum number of agents in environment")]
        public int maxAgents = 4;

        [Tooltip("Maximum shelves that can be observed (for padding observations)")]
        public int maxShelvesInView = 10;

        [Tooltip("Maximum episode steps (0 = infinite)")]
        public int maxSteps = 500;

        [Header("Prefabs")]
        public GameObject agentPrefab;
        public GameObject shelfPrefab;
        public GameObject goalMarkerPrefab;

        [Header("Spawn Locations")]
        [Tooltip("Agent spawn positions (grid coordinates)")]
        public List<Vector2Int> agentSpawnPositions = new List<Vector2Int>();

        [Tooltip("Shelf positions (grid coordinates)")]
        public List<Vector2Int> shelfPositions = new List<Vector2Int>();

        [Tooltip("Goal delivery positions (grid coordinates)")]
        public List<Vector2Int> goalPositions = new List<Vector2Int>();

        [Tooltip("Obstacle positions - cells that cannot be entered")]
        public List<Vector2Int> obstaclePositions = new List<Vector2Int>();

        [Header("Runtime State")]
        public List<RWAREAgent> agents = new List<RWAREAgent>();
        public List<Shelf> shelves = new List<Shelf>();
        private HashSet<Vector2Int> goalLocations = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> obstacles = new HashSet<Vector2Int>();

        private int currentStep = 0;
        private int shelvesDelivered = 0;

        // Grid occupancy tracking for collision detection
        private Dictionary<Vector2Int, RWAREAgent> agentGridOccupancy = new Dictionary<Vector2Int, RWAREAgent>();

        void Start()
        {
            InitializeEnvironment();
        }

        /// <summary>
        /// Initialize the warehouse environment
        /// </summary>
        public void InitializeEnvironment()
        {
            goalLocations = new HashSet<Vector2Int>(goalPositions);
            obstacles = new HashSet<Vector2Int>(obstaclePositions);

            // Spawn agents
            if (agents.Count == 0)
            {
                SpawnAgents();
            }

            // Spawn shelves
            if (shelves.Count == 0)
            {
                SpawnShelves();
            }

            // Spawn goal markers (visual indicators)
            SpawnGoalMarkers();

            // Request initial shelves
            RequestRandomShelves(numRequestedShelves);

            ResetEnvironment();
        }

        /// <summary>
        /// Reset environment for new episode
        /// </summary>
        public void ResetEnvironment()
        {
            currentStep = 0;
            shelvesDelivered = 0;
            agentGridOccupancy.Clear();

            // Reset all agents to spawn positions
            for (int i = 0; i < agents.Count; i++)
            {
                if (i < agentSpawnPositions.Count)
                {
                    agents[i].gridPosition = agentSpawnPositions[i];
                    agents[i].transform.position = GridToWorld(agentSpawnPositions[i]);
                    agents[i].transform.rotation = Quaternion.identity;
                    agents[i].currentDirection = RWAREAgent.Direction.North;
                    agentGridOccupancy[agentSpawnPositions[i]] = agents[i];
                }
            }

            // Reset all shelves
            for (int i = 0; i < shelves.Count; i++)
            {
                if (i < shelfPositions.Count)
                {
                    shelves[i].gridPosition = shelfPositions[i];
                    shelves[i].transform.position = GridToWorld(shelfPositions[i]);
                    shelves[i].ResetShelf();
                }
            }

            // Request new random shelves
            RequestRandomShelves(numRequestedShelves);
        }

        void Update()
        {
            // Track episode steps
            if (maxSteps > 0)
            {
                currentStep++;
                if (currentStep >= maxSteps)
                {
                    // End episode
                    foreach (var agent in agents)
                    {
                        agent.EndEpisode();
                    }
                    ResetEnvironment();
                }
            }
        }

        #region Agent Coordination

        /// <summary>
        /// Check if a move is valid (no collision, within bounds)
        /// </summary>
        public bool IsValidMove(RWAREAgent agent, Vector2Int targetPos)
        {
            // Check bounds
            if (targetPos.x < 0 || targetPos.x >= gridWidth || targetPos.y < 0 || targetPos.y >= gridHeight)
                return false;

            // Check obstacles
            if (obstacles.Contains(targetPos))
                return false;

            // Check if another agent is already there or trying to move there
            if (agentGridOccupancy.ContainsKey(targetPos) && agentGridOccupancy[targetPos] != agent)
                return false;

            return true;
        }

        /// <summary>
        /// Update agent position in occupancy grid
        /// </summary>
        public void UpdateAgentPosition(RWAREAgent agent, Vector2Int oldPos, Vector2Int newPos)
        {
            if (agentGridOccupancy.ContainsKey(oldPos) && agentGridOccupancy[oldPos] == agent)
            {
                agentGridOccupancy.Remove(oldPos);
            }

            agentGridOccupancy[newPos] = agent;
        }

        /// <summary>
        /// Get agents within sensor range of a position
        /// </summary>
        public List<RWAREAgent> GetAgentsInRange(Vector2Int position, int range)
        {
            List<RWAREAgent> nearbyAgents = new List<RWAREAgent>();

            foreach (var agent in agents)
            {
                int distance = Mathf.Abs(agent.gridPosition.x - position.x) +
                               Mathf.Abs(agent.gridPosition.y - position.y);
                if (distance <= range)
                {
                    nearbyAgents.Add(agent);
                }
            }

            return nearbyAgents;
        }

        /// <summary>
        /// Get shelves within sensor range of a position
        /// </summary>
        public List<Shelf> GetShelvesInRange(Vector2Int position, int range)
        {
            List<Shelf> nearbyShelves = new List<Shelf>();

            foreach (var shelf in shelves)
            {
                if (shelf.isCarried) continue; // Skip carried shelves

                int distance = Mathf.Abs(shelf.gridPosition.x - position.x) +
                               Mathf.Abs(shelf.gridPosition.y - position.y);
                if (distance <= range)
                {
                    nearbyShelves.Add(shelf);
                }
            }

            return nearbyShelves;
        }

        #endregion

        #region Shelf Management

        /// <summary>
        /// Get shelf at a specific grid position
        /// </summary>
        public Shelf GetShelfAtPosition(Vector2Int position)
        {
            return shelves.FirstOrDefault(s => s.gridPosition == position && !s.isCarried);
        }

        /// <summary>
        /// Check if agent can unload shelf at current position
        /// </summary>
        public bool CanUnloadShelf(Vector2Int position)
        {
            // Can unload at goal or any empty position
            return GetShelfAtPosition(position) == null;
        }

        /// <summary>
        /// Check if position is a goal location
        /// </summary>
        public bool IsGoalLocation(Vector2Int position)
        {
            return goalLocations.Contains(position);
        }

        /// <summary>
        /// Handle successful shelf delivery
        /// </summary>
        public void OnShelfDelivered(Shelf shelf)
        {
            shelvesDelivered++;
            shelf.SetRequested(false);

            // Request a new random shelf to maintain active requests
            RequestRandomShelves(1);

            Debug.Log($"Shelf delivered! Total: {shelvesDelivered}");
        }

        /// <summary>
        /// Request random shelves for delivery
        /// </summary>
        private void RequestRandomShelves(int count)
        {
            // Get all unrequested, uncarried shelves
            var availableShelves = shelves.Where(s => !s.isRequested && !s.isCarried).ToList();

            int toRequest = Mathf.Min(count, availableShelves.Count);
            for (int i = 0; i < toRequest; i++)
            {
                int randomIndex = Random.Range(0, availableShelves.Count);
                availableShelves[randomIndex].SetRequested(true);
                availableShelves.RemoveAt(randomIndex);
            }
        }

        #endregion

        #region Grid Utilities

        /// <summary>
        /// Convert grid coordinates to world position
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return new Vector3(
                gridPos.x * cellSize + cellSize / 2f,
                0,
                gridPos.y * cellSize + cellSize / 2f
            );
        }

        /// <summary>
        /// Convert world position to grid coordinates
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / cellSize),
                Mathf.FloorToInt(worldPos.z / cellSize)
            );
        }

        #endregion

        #region Spawning

        private void SpawnAgents()
        {
            for (int i = 0; i < Mathf.Min(maxAgents, agentSpawnPositions.Count); i++)
            {
                Vector3 spawnPos = GridToWorld(agentSpawnPositions[i]);
                GameObject agentObj = Instantiate(agentPrefab, spawnPos, Quaternion.identity, transform);
                agentObj.name = $"Agent_{i}";

                RWAREAgent agent = agentObj.GetComponent<RWAREAgent>();
                if (agent != null)
                {
                    agent.environmentManager = this;
                    agent.gridPosition = agentSpawnPositions[i];
                    agents.Add(agent);
                }
            }
        }

        private void SpawnShelves()
        {
            for (int i = 0; i < shelfPositions.Count; i++)
            {
                Vector3 spawnPos = GridToWorld(shelfPositions[i]);
                GameObject shelfObj = Instantiate(shelfPrefab, spawnPos, Quaternion.identity, transform);
                shelfObj.name = $"Shelf_{i}";

                Shelf shelf = shelfObj.GetComponent<Shelf>();
                if (shelf != null)
                {
                    shelf.gridPosition = shelfPositions[i];
                    shelves.Add(shelf);
                }
            }
        }

        private void SpawnGoalMarkers()
        {
            if (goalMarkerPrefab == null) return;

            foreach (var goalPos in goalPositions)
            {
                Vector3 worldPos = GridToWorld(goalPos);
                Instantiate(goalMarkerPrefab, worldPos, Quaternion.identity, transform);
            }
        }

        #endregion

        #region Debug Visualization

        void OnDrawGizmos()
        {
            // Draw grid
            Gizmos.color = Color.gray;
            for (int x = 0; x <= gridWidth; x++)
            {
                Vector3 start = new Vector3(x * cellSize, 0, 0);
                Vector3 end = new Vector3(x * cellSize, 0, gridHeight * cellSize);
                Gizmos.DrawLine(start, end);
            }

            for (int y = 0; y <= gridHeight; y++)
            {
                Vector3 start = new Vector3(0, 0, y * cellSize);
                Vector3 end = new Vector3(gridWidth * cellSize, 0, y * cellSize);
                Gizmos.DrawLine(start, end);
            }

            // Draw goal positions
            Gizmos.color = Color.green;
            foreach (var goal in goalPositions)
            {
                Vector3 pos = GridToWorld(goal);
                Gizmos.DrawWireCube(pos, Vector3.one * cellSize * 0.8f);
            }

            // Draw obstacles
            Gizmos.color = Color.red;
            foreach (var obstacle in obstaclePositions)
            {
                Vector3 pos = GridToWorld(obstacle);
                Gizmos.DrawCube(pos, Vector3.one * cellSize * 0.5f);
            }

            // Draw agent spawn positions
            Gizmos.color = Color.blue;
            foreach (var spawnPos in agentSpawnPositions)
            {
                Vector3 pos = GridToWorld(spawnPos);
                Gizmos.DrawWireSphere(pos, cellSize * 0.3f);
            }

            // Draw shelf positions
            Gizmos.color = Color.yellow;
            foreach (var shelfPos in shelfPositions)
            {
                Vector3 pos = GridToWorld(shelfPos);
                Gizmos.DrawWireCube(pos, Vector3.one * cellSize * 0.6f);
            }
        }

        #endregion
    }
}
