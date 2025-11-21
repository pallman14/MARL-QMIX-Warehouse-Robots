using UnityEngine;
using WarehouseRobotics;

/// <summary>
/// Creates a 3D point cloud visualization of LIDAR sensor data
/// Shows the actual 3D space that the LIDAR detects
/// </summary>
[RequireComponent(typeof(LIDARSensor))]
public class LIDARPointCloud : MonoBehaviour
{
    [Header("Point Cloud Settings")]
    [Tooltip("Show 3D point cloud mesh")]
    public bool showPointCloud = true;

    [Tooltip("Size of each detection point")]
    [Range(0.01f, 0.5f)]
    public float pointSize = 0.05f;

    [Tooltip("Color for detected surfaces")]
    public Color pointColor = new Color(1f, 0.3f, 0.3f, 0.9f); // Red-orange

    [Tooltip("Show points as spheres (more visual) or cubes (faster)")]
    public bool useSpheres = true;

    [Tooltip("Update frequency (0 = every frame, higher = slower updates)")]
    [Range(0, 10)]
    public int updateEveryNFrames = 1;

    [Header("Point Cloud Style")]
    [Tooltip("Fade out points over time")]
    public bool fadePoints = false;

    [Tooltip("Lifetime of each point before fading (seconds)")]
    public float pointLifetime = 0.5f;

    [Tooltip("Show only downsampled LIDAR points (36) or full resolution (360)")]
    public bool useFullResolution = false;

    [Header("Material")]
    public Material pointMaterial;

    private LIDARSensor lidarSensor;
    private GameObject pointCloudContainer;
    private GameObject[] pointObjects;
    private MeshRenderer[] pointRenderers;
    private float[] pointAges;
    private int frameCounter = 0;

    void Start()
    {
        lidarSensor = GetComponent<LIDARSensor>();

        if (lidarSensor == null)
        {
            Debug.LogError("LIDARPointCloud requires a LIDARSensor component!");
            enabled = false;
            return;
        }

        // Create container for point cloud
        pointCloudContainer = new GameObject("LIDAR_PointCloud");
        pointCloudContainer.transform.SetParent(transform);
        pointCloudContainer.transform.localPosition = Vector3.zero;

        // Create material if not assigned
        if (pointMaterial == null)
        {
            pointMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            pointMaterial.color = pointColor;
        }

        // Initialize point objects
        int numPoints = useFullResolution ? lidarSensor.numRays : lidarSensor.mlAgentsRays;
        CreatePointObjects(numPoints);

        Debug.Log($"✅ LIDAR Point Cloud initialized with {numPoints} points");
    }

    void CreatePointObjects(int numPoints)
    {
        pointObjects = new GameObject[numPoints];
        pointRenderers = new MeshRenderer[numPoints];
        pointAges = new float[numPoints];

        for (int i = 0; i < numPoints; i++)
        {
            // Create point object
            GameObject point;
            if (useSpheres)
            {
                point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            }
            else
            {
                point = GameObject.CreatePrimitive(PrimitiveType.Cube);
            }

            point.name = $"Point_{i}";
            point.transform.SetParent(pointCloudContainer.transform);
            point.transform.localScale = Vector3.one * pointSize;

            // Remove collider (we don't need physics on visualization)
            Collider collider = point.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // Apply material
            MeshRenderer renderer = point.GetComponent<MeshRenderer>();
            renderer.material = new Material(pointMaterial);
            pointRenderers[i] = renderer;

            pointObjects[i] = point;
            pointAges[i] = 0f;

            // Start invisible
            point.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (!showPointCloud || lidarSensor == null)
        {
            if (pointCloudContainer != null)
            {
                pointCloudContainer.SetActive(false);
            }
            return;
        }

        pointCloudContainer.SetActive(true);

        // Update frequency throttling
        frameCounter++;
        if (frameCounter < updateEveryNFrames)
        {
            if (fadePoints)
            {
                UpdatePointFading();
            }
            return;
        }
        frameCounter = 0;

        UpdatePointCloud();

        if (fadePoints)
        {
            UpdatePointFading();
        }
    }

    void UpdatePointCloud()
    {
        Vector3 sensorOrigin = transform.position + Vector3.up * lidarSensor.heightOffset;
        float[] distances = useFullResolution ? lidarSensor.GetRawDistances() : lidarSensor.GetMLAgentsDistances();
        int numPoints = distances.Length;
        float angleStep = 360f / numPoints;

        for (int i = 0; i < pointObjects.Length && i < numPoints; i++)
        {
            float angle = i * angleStep;
            float angleRad = angle * Mathf.Deg2Rad;

            // Calculate direction
            Vector3 direction = new Vector3(
                Mathf.Cos(angleRad),
                0,
                Mathf.Sin(angleRad)
            );
            direction = transform.rotation * direction;

            float distance = distances[i];
            bool isHit = distance < lidarSensor.maxRange;

            if (isHit)
            {
                // Position point at detection location
                Vector3 hitPoint = sensorOrigin + direction * distance;
                pointObjects[i].transform.position = hitPoint;
                pointObjects[i].SetActive(true);

                // Reset age for fading
                pointAges[i] = 0f;

                // Color based on distance (optional)
                float normalizedDist = distance / lidarSensor.maxRange;
                Color distanceColor = Color.Lerp(Color.red, Color.yellow, normalizedDist);
                pointRenderers[i].material.color = distanceColor;
            }
            else
            {
                // No hit, hide point (or show at max range)
                if (fadePoints)
                {
                    // Let it fade out
                    pointAges[i] += Time.deltaTime;
                }
                else
                {
                    pointObjects[i].SetActive(false);
                }
            }
        }
    }

    void UpdatePointFading()
    {
        for (int i = 0; i < pointObjects.Length; i++)
        {
            if (pointAges[i] > 0f)
            {
                pointAges[i] += Time.deltaTime;

                if (pointAges[i] >= pointLifetime)
                {
                    pointObjects[i].SetActive(false);
                    pointAges[i] = 0f;
                }
                else
                {
                    // Fade out alpha
                    float alpha = 1f - (pointAges[i] / pointLifetime);
                    Color color = pointRenderers[i].material.color;
                    color.a = alpha;
                    pointRenderers[i].material.color = color;
                }
            }
        }
    }

    void OnDestroy()
    {
        // Clean up
        if (pointCloudContainer != null)
        {
            Destroy(pointCloudContainer);
        }
    }

    // GUI for quick info
    void OnGUI()
    {
        if (!showPointCloud) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.cyan;
        style.fontStyle = FontStyle.Bold;

        int activePoints = 0;
        foreach (var point in pointObjects)
        {
            if (point != null && point.activeSelf)
            {
                activePoints++;
            }
        }

        GUI.Label(new Rect(10, Screen.height - 80, 300, 30),
            $"LIDAR Point Cloud: {activePoints}/{pointObjects.Length} points", style);
        GUI.Label(new Rect(10, Screen.height - 60, 300, 30),
            $"Resolution: {(useFullResolution ? "Full (360°)" : "Downsampled (36)")}", style);
        GUI.Label(new Rect(10, Screen.height - 40, 300, 30),
            $"Range: {lidarSensor.minRange:F2}m - {lidarSensor.maxRange:F2}m", style);
    }

    // Public methods for runtime control
    public void TogglePointCloud() { showPointCloud = !showPointCloud; }
    public void SetPointSize(float size) { pointSize = size; UpdatePointSizes(); }
    public void ToggleFading() { fadePoints = !fadePoints; }
    public void ToggleResolution() { useFullResolution = !useFullResolution; RecreatePointCloud(); }

    void UpdatePointSizes()
    {
        foreach (var point in pointObjects)
        {
            if (point != null)
            {
                point.transform.localScale = Vector3.one * pointSize;
            }
        }
    }

    void RecreatePointCloud()
    {
        if (pointCloudContainer != null)
        {
            Destroy(pointCloudContainer);
        }

        pointCloudContainer = new GameObject("LIDAR_PointCloud");
        pointCloudContainer.transform.SetParent(transform);
        pointCloudContainer.transform.localPosition = Vector3.zero;

        int numPoints = useFullResolution ? lidarSensor.numRays : lidarSensor.mlAgentsRays;
        CreatePointObjects(numPoints);
    }
}
