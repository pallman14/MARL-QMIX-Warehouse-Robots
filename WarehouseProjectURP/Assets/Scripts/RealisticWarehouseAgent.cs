using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

namespace WarehouseRobotics
{
    /// <summary>
    /// Realistic warehouse robot agent with LIDAR sensor
    /// Compatible with RWARE algorithms through observation/action adaptation
    /// Uses differential drive kinematics for sim-to-real transfer
    /// </summary>
    [RequireComponent(typeof(ArticulationBody))]
    public class RealisticWarehouseAgent : Agent
    {
        [Header("Robot Components")]
        public ArticulationBody robotBase;
        public ArticulationBody leftWheel;
        public ArticulationBody rightWheel;
        public LIDARSensor lidarSensor;

        [Header("Differential Drive Parameters")]
        [Tooltip("Max linear velocity (m/s) - TurtleBot 3: 0.26")]
        public float maxLinearVelocity = 0.26f;

        [Tooltip("Max angular velocity (rad/s) - TurtleBot 3: 1.82")]
        public float maxAngularVelocity = 1.82f;

        [Tooltip("Wheel radius (m) - TurtleBot 3: 0.033")]
        public float wheelRadius = 0.033f;

        [Tooltip("Wheel separation (m) - TurtleBot 3: 0.287")]
        public float wheelSeparation = 0.287f;

        [Header("Task Configuration")]
        public Transform targetShelf;
        public Transform goalLocation;
        public List<Transform> allShelves;
        public List<Transform> allGoals;

        [Tooltip("Distance threshold for reaching target (m)")]
        public float reachThreshold = 0.5f;

        [Tooltip("Maximum episode steps")]
        public int maxSteps = 1000;

        [Header("Rewards")]
        public float deliveryReward = 1.0f;
        public float stepPenalty = -0.001f;
        public float collisionPenalty = -0.01f;
        public float distanceRewardScale = -0.001f;

        [Header("Multi-Agent")]
        public bool isMultiAgent = false;
        public int agentID = 0;

        // State tracking
        private Vector3 startPosition;
        private Quaternion startRotation;
        private int currentStep = 0;
        private bool carryingShelf = false;
        private float lastDistanceToTarget = 0f;

        public override void Initialize()
        {
            if (robotBase == null)
                robotBase = GetComponent<ArticulationBody>();

            if (lidarSensor == null)
                lidarSensor = GetComponentInChildren<LIDARSensor>();

            startPosition = transform.position;
            startRotation = transform.rotation;

            // Find shelves and goals if not assigned
            if (allShelves.Count == 0)
            {
                GameObject[] shelfObjects = GameObject.FindGameObjectsWithTag("Shelf");
                foreach (var obj in shelfObjects)
                {
                    allShelves.Add(obj.transform);
                }
            }

            if (allGoals.Count == 0)
            {
                GameObject[] goalObjects = GameObject.FindGameObjectsWithTag("Goal");
                foreach (var obj in goalObjects)
                {
                    allGoals.Add(obj.transform);
                }
            }
        }

        public override void OnEpisodeBegin()
        {
            // Reset robot position and physics
            robotBase.TeleportRoot(startPosition, startRotation);
            robotBase.linearVelocity = Vector3.zero;
            robotBase.angularVelocity = Vector3.zero;

            // Reset wheel velocities
            ResetWheelVelocities();

            // Reset task state
            currentStep = 0;
            carryingShelf = false;

            // Select random target if multiple shelves available
            if (allShelves.Count > 0)
            {
                targetShelf = allShelves[Random.Range(0, allShelves.Count)];
            }

            if (targetShelf != null)
            {
                lastDistanceToTarget = Vector3.Distance(transform.position, targetShelf.position);
            }
        }

        /// <summary>
        /// Collect observations compatible with both ML-Agents and RWARE
        /// Total: 43 observations
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            // 1. LIDAR observations (36 normalized distances)
            if (lidarSensor != null)
            {
                float[] lidarData = lidarSensor.GetNormalizedDistances();
                foreach (float distance in lidarData)
                {
                    sensor.AddObservation(distance);
                }
            }
            else
            {
                // Pad with max range if LIDAR not available
                for (int i = 0; i < 36; i++)
                {
                    sensor.AddObservation(1.0f);
                }
            }

            // 2. Robot velocity (linear and angular) - normalized
            Vector3 velocity = robotBase.linearVelocity;
            sensor.AddObservation(velocity.x / maxLinearVelocity);
            sensor.AddObservation(velocity.z / maxLinearVelocity);
            Vector3 angularVel = robotBase.angularVelocity;
            sensor.AddObservation(angularVel.y / maxAngularVelocity);

            // 3. Robot orientation (yaw) - normalized to [-1, 1]
            float yaw = transform.eulerAngles.y / 180f - 1f;
            sensor.AddObservation(yaw);

            // 4. Task-specific observations
            if (targetShelf != null)
            {
                // Relative position to target (in robot's local frame)
                Vector3 toTarget = targetShelf.position - transform.position;
                Vector3 localTarget = transform.InverseTransformDirection(toTarget);

                sensor.AddObservation(localTarget.x / 10f); // Normalize by typical warehouse size
                sensor.AddObservation(localTarget.z / 10f);
                sensor.AddObservation(toTarget.magnitude / 10f);
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }

            // 5. Carrying shelf status
            sensor.AddObservation(carryingShelf ? 1f : 0f);

            // Total: 36 (LIDAR) + 3 (velocity) + 1 (yaw) + 3 (target) + 1 (carrying) = 44 observations
        }

        /// <summary>
        /// Process continuous actions for differential drive
        /// Actions: [linear_velocity, angular_velocity] both in range [-1, 1]
        /// </summary>
        public override void OnActionReceived(ActionBuffers actions)
        {
            currentStep++;

            // Get continuous actions
            float linearAction = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            float angularAction = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

            // Convert to velocities
            float linearVel = linearAction * maxLinearVelocity;
            float angularVel = angularAction * maxAngularVelocity;

            // Apply differential drive
            ApplyDifferentialDrive(linearVel, angularVel);

            // Calculate rewards
            CalculateRewards();

            // Check episode termination
            if (currentStep >= maxSteps)
            {
                EndEpisode();
            }
        }

        /// <summary>
        /// Calculate and apply rewards
        /// </summary>
        private void CalculateRewards()
        {
            // 1. Time penalty (encourage efficiency)
            AddReward(stepPenalty);

            if (targetShelf != null)
            {
                float currentDistance = Vector3.Distance(transform.position, targetShelf.position);

                // 2. Distance-based reward (shaped reward)
                float distanceReduction = lastDistanceToTarget - currentDistance;
                AddReward(distanceReduction * distanceRewardScale * 10f);
                lastDistanceToTarget = currentDistance;

                // 3. Reached target shelf
                if (currentDistance < reachThreshold && !carryingShelf)
                {
                    carryingShelf = true;
                    AddReward(deliveryReward * 0.5f); // Partial reward for pickup

                    // Switch target to goal
                    if (allGoals.Count > 0)
                    {
                        goalLocation = allGoals[Random.Range(0, allGoals.Count)];
                        targetShelf = goalLocation;
                        lastDistanceToTarget = Vector3.Distance(transform.position, targetShelf.position);
                    }
                }

                // 4. Reached goal with shelf (delivery complete)
                if (carryingShelf && goalLocation != null)
                {
                    float distanceToGoal = Vector3.Distance(transform.position, goalLocation.position);
                    if (distanceToGoal < reachThreshold)
                    {
                        AddReward(deliveryReward); // Full reward for delivery
                        EndEpisode();
                    }
                }
            }

            // 5. Collision penalty (using LIDAR)
            if (lidarSensor != null)
            {
                float minDistance = lidarSensor.GetMinimumDistance();
                if (minDistance < 0.3f) // Very close to obstacle
                {
                    AddReward(collisionPenalty);
                }
            }

            // 6. Velocity penalty (discourage excessive speed near obstacles)
            if (lidarSensor != null && !lidarSensor.IsPathClear(30f, 0.5f))
            {
                float speed = robotBase.linearVelocity.magnitude;
                if (speed > maxLinearVelocity * 0.5f)
                {
                    AddReward(-0.005f); // Slow down near obstacles
                }
            }
        }

        /// <summary>
        /// Apply differential drive kinematics
        /// Converts linear and angular velocities to wheel velocities
        /// </summary>
        private void ApplyDifferentialDrive(float linearVel, float angularVel)
        {
            // Differential drive equations:
            // v_left = (v_linear - v_angular * wheelSeparation / 2) / wheelRadius
            // v_right = (v_linear + v_angular * wheelSeparation / 2) / wheelRadius

            float leftWheelVel = (linearVel - angularVel * wheelSeparation / 2f) / wheelRadius;
            float rightWheelVel = (linearVel + angularVel * wheelSeparation / 2f) / wheelRadius;

            // Convert rad/s to degrees/s for Unity
            leftWheelVel *= Mathf.Rad2Deg;
            rightWheelVel *= Mathf.Rad2Deg;

            // Apply velocities to ArticulationBody joints
            if (leftWheel != null)
            {
                var leftDrive = leftWheel.xDrive;
                leftDrive.targetVelocity = leftWheelVel;
                leftWheel.xDrive = leftDrive;
            }

            if (rightWheel != null)
            {
                var rightDrive = rightWheel.xDrive;
                rightDrive.targetVelocity = rightWheelVel;
                rightWheel.xDrive = rightDrive;
            }
        }

        /// <summary>
        /// Reset wheel velocities to zero
        /// </summary>
        private void ResetWheelVelocities()
        {
            if (leftWheel != null)
            {
                var leftDrive = leftWheel.xDrive;
                leftDrive.targetVelocity = 0;
                leftWheel.xDrive = leftDrive;
            }

            if (rightWheel != null)
            {
                var rightDrive = rightWheel.xDrive;
                rightDrive.targetVelocity = 0;
                rightWheel.xDrive = rightDrive;
            }
        }

        /// <summary>
        /// Manual control for testing (keyboard input)
        /// Use WASD to control the robot
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuousActions = actionsOut.ContinuousActions;

            // WASD controls
            float forward = Input.GetAxis("Vertical"); // W/S keys
            float turn = Input.GetAxis("Horizontal"); // A/D keys

            continuousActions[0] = forward; // Linear velocity
            continuousActions[1] = turn; // Angular velocity

            // Debug output
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log($"Position: {transform.position}, Carrying: {carryingShelf}");
                if (lidarSensor != null)
                {
                    Debug.Log($"Min LIDAR distance: {lidarSensor.GetMinimumDistance()}");
                }
            }
        }

        /// <summary>
        /// Convert LIDAR observations to RWARE-compatible grid format
        /// For integration with existing RWARE algorithms
        /// </summary>
        public float[] GetRWARECompatibleObservations()
        {
            if (lidarSensor == null) return new float[9];

            float[] lidarData = lidarSensor.GetNormalizedDistances();

            // Create 3x3 occupancy grid from LIDAR
            // Grid layout:
            // [front-left] [front] [front-right]
            // [left]       [robot] [right]
            // [back-left]  [back]  [back-right]

            float threshold = 0.3f; // Objects within 30% of max range

            float[] grid = new float[9];

            // Front (0°)
            grid[1] = lidarData[0] < threshold ? 1f : 0f;

            // Front-right (45°)
            int idx45 = (int)(lidarData.Length * 45f / 360f);
            grid[2] = lidarData[idx45] < threshold ? 1f : 0f;

            // Right (90°)
            int idx90 = (int)(lidarData.Length * 90f / 360f);
            grid[5] = lidarData[idx90] < threshold ? 1f : 0f;

            // Back-right (135°)
            int idx135 = (int)(lidarData.Length * 135f / 360f);
            grid[8] = lidarData[idx135] < threshold ? 1f : 0f;

            // Back (180°)
            int idx180 = (int)(lidarData.Length * 180f / 360f);
            grid[7] = lidarData[idx180] < threshold ? 1f : 0f;

            // Back-left (225°)
            int idx225 = (int)(lidarData.Length * 225f / 360f);
            grid[6] = lidarData[idx225] < threshold ? 1f : 0f;

            // Left (270°)
            int idx270 = (int)(lidarData.Length * 270f / 360f);
            grid[3] = lidarData[idx270] < threshold ? 1f : 0f;

            // Front-left (315°)
            int idx315 = (int)(lidarData.Length * 315f / 360f);
            grid[0] = lidarData[idx315] < threshold ? 1f : 0f;

            // Center (robot position)
            grid[4] = 0.5f; // Half value to indicate robot position

            return grid;
        }

        // Visualization
        void OnDrawGizmos()
        {
            // Draw target connections
            if (targetShelf != null)
            {
                Gizmos.color = carryingShelf ? Color.green : Color.yellow;
                Gizmos.DrawLine(transform.position, targetShelf.position);
            }

            // Draw reach threshold
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, reachThreshold);
        }
    }
}
