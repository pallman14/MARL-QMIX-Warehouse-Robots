using UnityEngine;

namespace WarehouseRobotics
{
    /// <summary>
    /// Simulates a LIDAR sensor (like TurtleBot 3's LDS-01)
    /// Provides both full resolution (360 rays) and downsampled (36 rays) for ML-Agents
    /// </summary>
    public class LIDARSensor : MonoBehaviour
    {
        [Header("LIDAR Configuration")]
        [Tooltip("Number of rays (TurtleBot 3 LDS-01: 360 rays)")]
        public int numRays = 360;

        [Tooltip("Max detection range in meters (TurtleBot 3: 3.5m)")]
        public float maxRange = 3.5f;

        [Tooltip("Min detection range in meters (TurtleBot 3: 0.12m)")]
        public float minRange = 0.12f;

        [Tooltip("Start angle (degrees, typically 0)")]
        public float startAngle = 0f;

        [Tooltip("End angle (degrees, typically 360)")]
        public float endAngle = 360f;

        [Tooltip("Height offset from robot base (TurtleBot 3: 0.192m)")]
        public float heightOffset = 0.192f;

        [Tooltip("Layers to detect (e.g., walls, shelves, other robots)")]
        public LayerMask detectionLayers = ~0;

        [Header("ML-Agents Configuration")]
        [Tooltip("Downsample for ML-Agents (36 rays = 10° resolution)")]
        public int mlAgentsRays = 36;

        [Tooltip("Update frequency (Hz). 0 = every FixedUpdate")]
        public float scanFrequency = 0f; // 0 = max speed

        [Header("Debug Visualization")]
        public bool visualizeRays = true;
        public bool showDebugUI = false; // Show on-screen debug text
        public Color hitRayColor = Color.red;
        public Color missRayColor = Color.gray;
        public bool onlyVisualizeMissed = false;

        // Data storage
        private float[] rawDistances;
        private float[] mlAgentsDistances;
        private float scanTimer = 0f;

        void Start()
        {
            rawDistances = new float[numRays];
            mlAgentsDistances = new float[mlAgentsRays];

            // Initialize with max range
            for (int i = 0; i < numRays; i++)
            {
                rawDistances[i] = maxRange;
            }
        }

        void FixedUpdate()
        {
            // Rate limiting
            if (scanFrequency > 0)
            {
                scanTimer += Time.fixedDeltaTime;
                if (scanTimer < 1f / scanFrequency)
                {
                    return;
                }
                scanTimer = 0f;
            }

            ScanLIDAR();
        }

        /// <summary>
        /// Perform LIDAR scan (mimics real TurtleBot 3 LDS-01 sensor)
        /// </summary>
        public void ScanLIDAR()
        {
            Vector3 origin = transform.position + Vector3.up * heightOffset;
            float angleStep = (endAngle - startAngle) / numRays;

            for (int i = 0; i < numRays; i++)
            {
                float angle = startAngle + (i * angleStep);
                float angleRad = angle * Mathf.Deg2Rad;

                // Calculate ray direction (in XZ plane)
                Vector3 direction = new Vector3(
                    Mathf.Cos(angleRad),
                    0,
                    Mathf.Sin(angleRad)
                );

                // Rotate by robot's current orientation
                direction = transform.rotation * direction;

                RaycastHit hit;
                bool didHit = Physics.Raycast(origin, direction, out hit, maxRange, detectionLayers);

                if (didHit)
                {
                    float distance = hit.distance;
                    rawDistances[i] = Mathf.Clamp(distance, minRange, maxRange);

                    if (visualizeRays && !onlyVisualizeMissed)
                    {
                        Debug.DrawRay(origin, direction * distance, hitRayColor);
                    }
                }
                else
                {
                    rawDistances[i] = maxRange;

                    if (visualizeRays)
                    {
                        Debug.DrawRay(origin, direction * maxRange, missRayColor);
                    }
                }
            }

            // Downsample for ML-Agents
            DownsampleForMLAgents();
        }

        /// <summary>
        /// Downsample full LIDAR data for ML-Agents (360 rays → 36 rays)
        /// Takes every Nth ray to reduce observation space
        /// </summary>
        private void DownsampleForMLAgents()
        {
            if (mlAgentsRays >= numRays)
            {
                // No downsampling needed
                mlAgentsDistances = rawDistances;
                return;
            }

            int step = numRays / mlAgentsRays;

            for (int i = 0; i < mlAgentsRays; i++)
            {
                int sourceIndex = i * step;
                if (sourceIndex < numRays)
                {
                    mlAgentsDistances[i] = rawDistances[sourceIndex];
                }
                else
                {
                    mlAgentsDistances[i] = maxRange;
                }
            }
        }

        /// <summary>
        /// Get normalized LIDAR distances for ML-Agents observations
        /// Returns values in range [0, 1] where 0 = minRange, 1 = maxRange
        /// </summary>
        public float[] GetNormalizedDistances()
        {
            float[] normalized = new float[mlAgentsRays];
            for (int i = 0; i < mlAgentsRays; i++)
            {
                normalized[i] = (mlAgentsDistances[i] - minRange) / (maxRange - minRange);
                normalized[i] = Mathf.Clamp01(normalized[i]);
            }
            return normalized;
        }

        /// <summary>
        /// Get raw LIDAR distances (full resolution)
        /// Useful for ROS publishing or detailed analysis
        /// </summary>
        public float[] GetRawDistances()
        {
            return rawDistances;
        }

        /// <summary>
        /// Get downsampled LIDAR distances (ML-Agents resolution)
        /// </summary>
        public float[] GetMLAgentsDistances()
        {
            return mlAgentsDistances;
        }

        /// <summary>
        /// Check if obstacle is detected within a specific angle range
        /// </summary>
        public bool IsObstacleInRange(float angleStart, float angleEnd, float maxDistance)
        {
            int startIdx = Mathf.FloorToInt((angleStart - startAngle) / ((endAngle - startAngle) / mlAgentsRays));
            int endIdx = Mathf.CeilToInt((angleEnd - startAngle) / ((endAngle - startAngle) / mlAgentsRays));

            startIdx = Mathf.Clamp(startIdx, 0, mlAgentsRays - 1);
            endIdx = Mathf.Clamp(endIdx, 0, mlAgentsRays - 1);

            for (int i = startIdx; i <= endIdx; i++)
            {
                if (mlAgentsDistances[i] < maxDistance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get minimum distance detected by LIDAR (useful for collision avoidance)
        /// </summary>
        public float GetMinimumDistance()
        {
            float min = maxRange;
            foreach (float distance in mlAgentsDistances)
            {
                if (distance < min)
                {
                    min = distance;
                }
            }
            return min;
        }

        /// <summary>
        /// Check if path ahead is clear for navigation
        /// </summary>
        public bool IsPathClear(float forwardAngleRange = 45f, float safeDistance = 0.5f)
        {
            return !IsObstacleInRange(-forwardAngleRange / 2f, forwardAngleRange / 2f, safeDistance);
        }

        // Debug GUI
        void OnGUI()
        {
            if (!showDebugUI) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("LIDAR Sensor Debug");
            GUILayout.Label($"Min Distance: {GetMinimumDistance():F2}m");
            GUILayout.Label($"Path Clear: {IsPathClear()}");

            // Show first few distances
            GUILayout.Label("Sample Distances:");
            for (int i = 0; i < Mathf.Min(5, mlAgentsRays); i++)
            {
                float angle = i * (360f / mlAgentsRays);
                GUILayout.Label($"  {angle:F0}°: {mlAgentsDistances[i]:F2}m");
            }

            GUILayout.EndArea();
        }
    }
}
