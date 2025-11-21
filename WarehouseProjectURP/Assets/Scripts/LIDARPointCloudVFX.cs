using UnityEngine;
using UnityEngine.VFX;
using WarehouseRobotics;

/// <summary>
/// Proper LIDAR point cloud visualization using VFX Graph
/// GPU-accelerated, can handle thousands of points efficiently
///
/// REQUIREMENTS:
/// 1. Unity Visual Effect Graph package installed
/// 2. VFX Graph asset created (see setup instructions below)
/// 3. Project must support VFX Graph (URP/HDRP)
/// </summary>
[RequireComponent(typeof(LIDARSensor))]
public class LIDARPointCloudVFX : MonoBehaviour
{
    [Header("VFX Graph Reference")]
    [Tooltip("Assign the VFX Graph asset for point cloud rendering")]
    public VisualEffect pointCloudVFX;

    [Header("Point Cloud Settings")]
    [Tooltip("Show the point cloud")]
    public bool showPointCloud = true;

    [Tooltip("Size of each point in meters")]
    [Range(0.01f, 0.5f)]
    public float pointSize = 0.1f;

    [Tooltip("Use full 360-ray resolution or downsampled 36 rays")]
    public bool useFullResolution = false;

    [Header("Colors")]
    public Gradient distanceGradient;

    private LIDARSensor lidarSensor;
    private GraphicsBuffer pointBuffer;
    private GraphicsBuffer colorBuffer;

    // Point data structure matching what VFX Graph expects
    struct PointData
    {
        public Vector3 position;
        public Vector3 color;
    }

    void Start()
    {
        lidarSensor = GetComponent<LIDARSensor>();

        if (lidarSensor == null)
        {
            Debug.LogError("LIDARPointCloudVFX: No LIDARSensor found!");
            enabled = false;
            return;
        }

        // Initialize default gradient if not set
        if (distanceGradient == null || distanceGradient.colorKeys.Length == 0)
        {
            distanceGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(Color.red, 0.0f);    // Close
            colorKeys[1] = new GradientColorKey(Color.yellow, 0.5f); // Medium
            colorKeys[2] = new GradientColorKey(Color.green, 1.0f);  // Far

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);

            distanceGradient.SetKeys(colorKeys, alphaKeys);
        }

        InitializeVFX();
    }

    void InitializeVFX()
    {
        if (pointCloudVFX == null)
        {
            Debug.LogWarning("LIDARPointCloudVFX: No VFX Graph assigned! Please create and assign a VFX Graph asset.");
            Debug.LogWarning("See setup instructions in script comments.");
            return;
        }

        int numPoints = useFullResolution ? lidarSensor.numRays : lidarSensor.mlAgentsRays;

        // Create graphics buffer for point positions
        pointBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            numPoints,
            System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3))
        );

        // Create graphics buffer for colors
        colorBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            numPoints,
            System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3))
        );

        // Set buffers in VFX Graph
        pointCloudVFX.SetGraphicsBuffer("PointBuffer", pointBuffer);
        pointCloudVFX.SetGraphicsBuffer("ColorBuffer", colorBuffer);
        pointCloudVFX.SetInt("PointCount", numPoints);
        pointCloudVFX.SetFloat("PointSize", pointSize);

        Debug.Log($"✅ VFX Point Cloud initialized with {numPoints} points");
    }

    void LateUpdate()
    {
        if (!showPointCloud || lidarSensor == null || pointCloudVFX == null)
        {
            if (pointCloudVFX != null) pointCloudVFX.enabled = false;
            return;
        }

        pointCloudVFX.enabled = true;
        UpdatePointCloud();

        // Update VFX parameters
        pointCloudVFX.SetFloat("PointSize", pointSize);
    }

    void UpdatePointCloud()
    {
        if (pointBuffer == null || colorBuffer == null) return;

        Vector3 sensorOrigin = transform.position + Vector3.up * lidarSensor.heightOffset;
        float[] distances = useFullResolution ? lidarSensor.GetRawDistances() : lidarSensor.GetMLAgentsDistances();

        int numPoints = distances.Length;
        float angleStep = 360f / numPoints;

        Vector3[] positions = new Vector3[numPoints];
        Vector3[] colors = new Vector3[numPoints];

        for (int i = 0; i < numPoints; i++)
        {
            float angle = i * angleStep;
            float angleRad = angle * Mathf.Deg2Rad;

            // Calculate direction in sensor space
            Vector3 direction = new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad));
            direction = transform.rotation * direction;

            float distance = distances[i];
            bool isHit = distance < lidarSensor.maxRange;

            if (isHit)
            {
                // Calculate world position
                Vector3 hitPoint = sensorOrigin + direction * distance;
                positions[i] = hitPoint;

                // Calculate color based on distance
                float normalizedDist = Mathf.Clamp01((distance - lidarSensor.minRange) / (lidarSensor.maxRange - lidarSensor.minRange));
                Color color = distanceGradient.Evaluate(normalizedDist);
                colors[i] = new Vector3(color.r, color.g, color.b);
            }
            else
            {
                // No hit - place point far away (will be culled or invisible)
                positions[i] = Vector3.one * 10000f;
                colors[i] = Vector3.zero;
            }
        }

        // Upload to GPU
        pointBuffer.SetData(positions);
        colorBuffer.SetData(colors);
    }

    void OnDestroy()
    {
        // Clean up graphics buffers
        if (pointBuffer != null)
        {
            pointBuffer.Release();
            pointBuffer = null;
        }

        if (colorBuffer != null)
        {
            colorBuffer.Release();
            colorBuffer = null;
        }
    }

    void OnValidate()
    {
        // Update VFX parameters when changed in Inspector
        if (pointCloudVFX != null && Application.isPlaying)
        {
            pointCloudVFX.SetFloat("PointSize", pointSize);
        }
    }
}

/*
===========================================
VFX GRAPH SETUP INSTRUCTIONS
===========================================

This script requires a VFX Graph asset to render the point cloud.
Here's how to create one:

STEP 1: Install Visual Effect Graph Package
-------------------------------------------
1. Window → Package Manager
2. Search for "Visual Effect Graph"
3. Click Install

STEP 2: Create VFX Graph Asset
-------------------------------
1. Right-click in Project window
2. Create → Visual Effects → Visual Effect Graph
3. Name it "LIDARPointCloud"

STEP 3: Configure VFX Graph
----------------------------
1. Double-click the VFX Graph asset to open VFX Graph window
2. Delete the default nodes

3. Add these nodes:
   a) Right-click → Create Node → Context → Initialize Particle
   b) Right-click → Create Node → Context → Update Particle
   c) Right-click → Create Node → Context → Output Particle URP Lit (or HDRP)

4. In the CONTEXT PANEL (left sidebar):
   a) Under "Properties", add these exposed properties:
      - GraphicsBuffer: "PointBuffer" (Point positions)
      - GraphicsBuffer: "ColorBuffer" (Point colors)
      - Int: "PointCount" (Number of points)
      - Float: "PointSize" (Size of points, default: 0.1)

5. Configure INITIALIZE context:
   a) Set Capacity: Use "PointCount" property
   b) Add operator: "Sample Graphics Buffer" (for positions)
      - Buffer: PointBuffer
      - Index: particleId
      - Connect output to "Set Position"
   c) Add operator: "Sample Graphics Buffer" (for colors)
      - Buffer: ColorBuffer
      - Index: particleId
      - Connect output to "Set Color"

6. Configure UPDATE context:
   a) Set Age Over Lifetime → Keep particles alive

7. Configure OUTPUT context:
   a) Set primitive to "Point" or "Quad"
   b) Set size to "PointSize" property
   c) Enable "Use Color"

8. Save the VFX Graph (Ctrl+S)

STEP 4: Assign to Component
----------------------------
1. Select your agent GameObject
2. Add component: LIDARPointCloudVFX
3. Drag the VFX Graph asset into "Point Cloud VFX" field
4. Press Play!

===========================================
ALTERNATIVE: Use Unity SensorSDK
===========================================

If you have Unity SensorSDK 2.0+ installed:
1. Use PointCloudViewer component instead
2. It has built-in VFX Graph support
3. Handles everything automatically

To install SensorSDK:
1. Window → Package Manager
2. Add package from git URL:
   com.unity.sensorsdk
3. Use PointCloudViewer component directly

===========================================
TROUBLESHOOTING
===========================================

Q: VFX Graph not appearing?
A: Make sure URP/HDRP is configured correctly. VFX Graph requires URP 10+ or HDRP.

Q: Points not visible?
A: Increase PointSize or check VFX Graph Output settings.

Q: Performance issues?
A: Reduce number of points (use downsampled 36 rays instead of 360).

Q: "PointBuffer" not found error?
A: Make sure exposed property names in VFX Graph match exactly (case-sensitive).

*/
