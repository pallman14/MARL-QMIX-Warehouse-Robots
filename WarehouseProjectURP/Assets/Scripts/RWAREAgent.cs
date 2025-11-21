using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

namespace RWARE
{
    /// <summary>
    /// RWARE-compatible warehouse robot agent
    /// Matches the observation and action space of the original RWARE environment
    /// </summary>
    public class RWAREAgent : Agent
    {
        [Header("RWARE Robot Configuration")]
        [Tooltip("Movement speed in units per second")]
        public float moveSpeed = 2f;

        [Tooltip("Rotation speed in degrees per second")]
        public float rotationSpeed = 180f;

        [Tooltip("Sensor range for observing other agents and shelves (grid cells)")]
        public int sensorRange = 1; // Default 3x3 grid (range of 1 around agent)

        [Tooltip("Reference to the environment manager")]
        public RWAREEnvironment environmentManager;

        [Header("Runtime State")]
        public bool isCarryingShelf = false;
        public Shelf carriedShelf = null;
        public Vector2Int gridPosition;
        public Direction currentDirection = Direction.North;

        // Grid-based movement
        private Vector3 targetWorldPosition;
        private bool isMoving = false;
        private Quaternion targetRotation;
        private bool isRotating = false;

        // For discrete grid-based actions
        public enum Direction { North = 0, East = 1, South = 2, West = 3 }

        // RWARE Action space: {TurnLeft, TurnRight, Forward, LoadUnload, Noop}
        public enum RWAREAction { TurnLeft = 0, TurnRight = 1, Forward = 2, LoadUnload = 3, Noop = 4 }

        public override void Initialize()
        {
            if (environmentManager == null)
            {
                environmentManager = GetComponentInParent<RWAREEnvironment>();
            }

            // Auto-add visual components if not present
            if (GetComponent<RobotVisualizer>() == null)
            {
                gameObject.AddComponent<RobotVisualizer>();
            }

            // Auto-add camera controller if not present
            if (GetComponent<RobotCameraController>() == null)
            {
                gameObject.AddComponent<RobotCameraController>();
            }

            // Initialize grid position based on world position
            UpdateGridPositionFromWorld();
        }

        public override void OnEpisodeBegin()
        {
            // Reset state
            isCarryingShelf = false;
            if (carriedShelf != null)
            {
                carriedShelf.DetachFromAgent();
                carriedShelf = null;
            }

            // Environment will handle repositioning
            isMoving = false;
            isRotating = false;
            UpdateGridPositionFromWorld();
        }

        /// <summary>
        /// Collect observations matching RWARE's observation space:
        /// - Self: location, carrying_shelf, direction
        /// - Others: nearby agents and shelves within sensor range
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            // Self observations (normalized)
            sensor.AddObservation(gridPosition.x / (float)environmentManager.gridWidth);
            sensor.AddObservation(gridPosition.y / (float)environmentManager.gridHeight);
            sensor.AddObservation(isCarryingShelf ? 1f : 0f);
            sensor.AddObservation((int)currentDirection / 3f); // Normalize direction (0-3)

            // Observe nearby agents (within sensor range)
            List<RWAREAgent> nearbyAgents = environmentManager.GetAgentsInRange(gridPosition, sensorRange);
            foreach (var agent in nearbyAgents)
            {
                if (agent != this)
                {
                    // Relative position
                    Vector2Int relativePos = agent.gridPosition - gridPosition;
                    sensor.AddObservation(relativePos.x / (float)(sensorRange * 2));
                    sensor.AddObservation(relativePos.y / (float)(sensorRange * 2));
                    sensor.AddObservation((int)agent.currentDirection / 3f);
                    sensor.AddObservation(agent.isCarryingShelf ? 1f : 0f);
                }
            }

            // Pad observations if fewer agents than max
            int maxAgentsObserved = environmentManager.maxAgents - 1;
            for (int i = nearbyAgents.Count - 1; i < maxAgentsObserved; i++)
            {
                sensor.AddObservation(0f); // x
                sensor.AddObservation(0f); // y
                sensor.AddObservation(0f); // direction
                sensor.AddObservation(0f); // carrying shelf
            }

            // Observe nearby shelves
            List<Shelf> nearbyShelves = environmentManager.GetShelvesInRange(gridPosition, sensorRange);
            foreach (var shelf in nearbyShelves)
            {
                Vector2Int relativePos = shelf.gridPosition - gridPosition;
                sensor.AddObservation(relativePos.x / (float)(sensorRange * 2));
                sensor.AddObservation(relativePos.y / (float)(sensorRange * 2));
                sensor.AddObservation(shelf.isRequested ? 1f : 0f);
            }

            // Pad shelf observations
            int maxShelvesObserved = environmentManager.maxShelvesInView;
            for (int i = nearbyShelves.Count; i < maxShelvesObserved; i++)
            {
                sensor.AddObservation(0f); // x
                sensor.AddObservation(0f); // y
                sensor.AddObservation(0f); // is requested
            }
        }

        /// <summary>
        /// Process discrete actions matching RWARE action space
        /// </summary>
        public override void OnActionReceived(ActionBuffers actions)
        {
            int action = actions.DiscreteActions[0];

            // Don't process new actions if currently moving/rotating
            if (isMoving || isRotating)
            {
                return;
            }

            switch ((RWAREAction)action)
            {
                case RWAREAction.TurnLeft:
                    RotateLeft();
                    break;

                case RWAREAction.TurnRight:
                    RotateRight();
                    break;

                case RWAREAction.Forward:
                    MoveForward();
                    break;

                case RWAREAction.LoadUnload:
                    LoadUnloadShelf();
                    break;

                case RWAREAction.Noop:
                    // Do nothing
                    break;
            }

            // Small time penalty to encourage efficiency (like RWARE)
            AddReward(-0.01f);
        }

        /// <summary>
        /// Manual control for testing (optional)
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discreteActions = actionsOut.DiscreteActions;

            if (Input.GetKey(KeyCode.Q))
                discreteActions[0] = (int)RWAREAction.TurnLeft;
            else if (Input.GetKey(KeyCode.E))
                discreteActions[0] = (int)RWAREAction.TurnRight;
            else if (Input.GetKey(KeyCode.W))
                discreteActions[0] = (int)RWAREAction.Forward;
            else if (Input.GetKey(KeyCode.Space))
                discreteActions[0] = (int)RWAREAction.LoadUnload;
            else
                discreteActions[0] = (int)RWAREAction.Noop;
        }

        private void RotateLeft()
        {
            currentDirection = (Direction)(((int)currentDirection + 3) % 4); // Counter-clockwise
            targetRotation = Quaternion.Euler(0, (int)currentDirection * 90, 0);
            isRotating = true;
        }

        private void RotateRight()
        {
            currentDirection = (Direction)(((int)currentDirection + 1) % 4); // Clockwise
            targetRotation = Quaternion.Euler(0, (int)currentDirection * 90, 0);
            isRotating = true;
        }

        private void MoveForward()
        {
            Vector2Int targetGridPos = gridPosition + GetDirectionVector(currentDirection);

            // Check if move is valid (collision detection handled by environment)
            if (environmentManager.IsValidMove(this, targetGridPos))
            {
                gridPosition = targetGridPos;
                targetWorldPosition = environmentManager.GridToWorld(gridPosition);
                isMoving = true;
            }
        }

        private void LoadUnloadShelf()
        {
            if (isCarryingShelf)
            {
                // Unload shelf
                if (environmentManager.CanUnloadShelf(gridPosition))
                {
                    // Check if we're at a goal location
                    if (environmentManager.IsGoalLocation(gridPosition) && carriedShelf.isRequested)
                    {
                        // Reward for successful delivery
                        AddReward(1.0f);
                        environmentManager.OnShelfDelivered(carriedShelf);
                    }

                    carriedShelf.DetachFromAgent();
                    carriedShelf.gridPosition = gridPosition;
                    carriedShelf = null;
                    isCarryingShelf = false;

                    // Update visual
                    RobotVisualizer visualizer = GetComponent<RobotVisualizer>();
                    if (visualizer != null)
                    {
                        visualizer.SetCarryingState(false);
                    }
                }
            }
            else
            {
                // Load shelf
                Shelf shelfAtPosition = environmentManager.GetShelfAtPosition(gridPosition);
                if (shelfAtPosition != null && !shelfAtPosition.isCarried)
                {
                    carriedShelf = shelfAtPosition;
                    shelfAtPosition.AttachToAgent(this);
                    isCarryingShelf = true;

                    // Update visual
                    RobotVisualizer visualizer = GetComponent<RobotVisualizer>();
                    if (visualizer != null)
                    {
                        visualizer.SetCarryingState(true);
                    }
                }
            }
        }

        void Update()
        {
            // Smooth movement between grid positions
            if (isMoving)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetWorldPosition, moveSpeed * Time.deltaTime);
                if (Vector3.Distance(transform.position, targetWorldPosition) < 0.01f)
                {
                    transform.position = targetWorldPosition;
                    isMoving = false;

                    // Update carried shelf position if any
                    if (carriedShelf != null)
                    {
                        carriedShelf.transform.position = transform.position + Vector3.up * 0.5f;
                    }
                }
            }

            // Smooth rotation
            if (isRotating)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, targetRotation) < 1f)
                {
                    transform.rotation = targetRotation;
                    isRotating = false;
                }
            }
        }

        private Vector2Int GetDirectionVector(Direction dir)
        {
            switch (dir)
            {
                case Direction.North: return Vector2Int.up;
                case Direction.East: return Vector2Int.right;
                case Direction.South: return Vector2Int.down;
                case Direction.West: return Vector2Int.left;
                default: return Vector2Int.zero;
            }
        }

        private void UpdateGridPositionFromWorld()
        {
            gridPosition = environmentManager.WorldToGrid(transform.position);
        }
    }
}
