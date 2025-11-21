using UnityEngine;
using WarehouseRobotics;

/// <summary>
/// Simple, easy-to-see LIDAR point cloud visualization
/// Attach to any agent with a LIDARSensor
/// </summary>
public class SimpleLIDARPointCloud : MonoBehaviour
{
    [Header("Visibility")]
    [Tooltip("Show the point cloud")]
    public bool showPointCloud = true;

    [Tooltip("Size of each point sphere")]
    [Range(0.05f, 0.5f)]
    public float pointSize = 0.15f;

    [Header("Colors")]
    public Color closeColor = Color.red;      // < 1m
    public Color mediumColor = Color.yellow;  // 1-2m
    public Color farColor = Color.green;      // 2-3.5m

    [Header("Settings")]
    [Tooltip("Number of points to show (fewer = clearer)")]
    public int numberOfPoints = 36;

    private LIDARSensor lidarSensor;
    private GameObject[] spheres;
    private MeshRenderer[] renderers;

    void Start()
    {
        lidarSensor = GetComponent<LIDARSensor>();

        if (lidarSensor == null)
        {
            Debug.LogError("SimpleLIDARPointCloud: No LIDARSensor found! Add LIDARSensor component first.");
            enabled = false;
            return;
        }

        CreatePointCloud();
        Debug.Log($"✅ LIDAR Point Cloud created with {numberOfPoints} points");
        Debug.Log($"   Point size: {pointSize}m");
        Debug.Log($"   Look for colored spheres around the robot!");
    }

    void CreatePointCloud()
    {
        spheres = new GameObject[numberOfPoints];
        renderers = new MeshRenderer[numberOfPoints];

        for (int i = 0; i < numberOfPoints; i++)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"LIDARPoint_{i}";
            sphere.transform.SetParent(transform);
            sphere.transform.localScale = Vector3.one * pointSize;

            // Remove collider
            Destroy(sphere.GetComponent<Collider>());

            // Setup material
            MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = closeColor;
            renderer.material = mat;

            spheres[i] = sphere;
            renderers[i] = renderer;
        }
    }

    void LateUpdate()
    {
        if (!showPointCloud || lidarSensor == null || spheres == null)
        {
            if (spheres != null)
            {
                foreach (var sphere in spheres)
                {
                    if (sphere != null) sphere.SetActive(false);
                }
            }
            return;
        }

        UpdatePointCloud();
    }

    void UpdatePointCloud()
    {
        Vector3 sensorOrigin = transform.position + Vector3.up * lidarSensor.heightOffset;
        float[] distances = lidarSensor.GetMLAgentsDistances();

        int step = distances.Length / numberOfPoints;
        if (step < 1) step = 1;

        for (int i = 0; i < numberOfPoints && i * step < distances.Length; i++)
        {
            int distanceIndex = i * step;
            float distance = distances[distanceIndex];

            // Calculate angle
            float angle = (distanceIndex * 360f / distances.Length) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            direction = transform.rotation * direction;

            // Check if hit
            bool isHit = distance < lidarSensor.maxRange;

            if (isHit)
            {
                // Position sphere at hit point
                Vector3 hitPoint = sensorOrigin + direction * distance;
                spheres[i].transform.position = hitPoint;
                spheres[i].SetActive(true);

                // Color based on distance
                Color color = GetColorForDistance(distance);
                renderers[i].material.color = color;
            }
            else
            {
                spheres[i].SetActive(false);
            }
        }
    }

    Color GetColorForDistance(float distance)
    {
        if (distance < 1f)
        {
            return closeColor;
        }
        else if (distance < 2f)
        {
            float t = (distance - 1f) / 1f;
            return Color.Lerp(closeColor, mediumColor, t);
        }
        else
        {
            float t = (distance - 2f) / 1.5f;
            return Color.Lerp(mediumColor, farColor, t);
        }
    }

    void OnGUI()
    {
        if (!showPointCloud) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleLeft;
        style.padding = new RectOffset(10, 10, 10, 10);

        int activePoints = 0;
        foreach (var sphere in spheres)
        {
            if (sphere != null && sphere.activeSelf) activePoints++;
        }

        string info = $"LIDAR Point Cloud\n" +
                     $"Active Points: {activePoints}/{numberOfPoints}\n" +
                     $"Red = Close, Yellow = Medium, Green = Far";

        GUI.Box(new Rect(10, Screen.height - 100, 350, 90), info, style);
    }

    void OnDestroy()
    {
        if (spheres != null)
        {
            foreach (var sphere in spheres)
            {
                if (sphere != null) Destroy(sphere);
            }
        }
    }
}
