using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages warehouse setup, spawning robots, packages, and delivery zones
/// </summary>
public class SimpleWarehouseManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject robotPrefab;
    public GameObject packagePrefab;
    public GameObject deliveryZonePrefab;

    [Header("Spawn Settings")]
    public int numberOfRobots = 2;
    public int numberOfPackages = 5;
    public int numberOfDeliveryZones = 2;

    [Header("Spawn Areas")]
    public Vector3 robotSpawnAreaCenter = new Vector3(0, 0, -5);
    public Vector3 robotSpawnAreaSize = new Vector3(5, 0, 2);

    public Vector3 packageSpawnAreaCenter = new Vector3(0, 0, 0);
    public Vector3 packageSpawnAreaSize = new Vector3(10, 0, 10);

    public Vector3 deliveryZoneAreaCenter = new Vector3(0, 0, 8);
    public Vector3 deliveryZoneAreaSize = new Vector3(8, 0, 2);

    [Header("Runtime")]
    public GameObject[] spawnedRobots;
    public GameObject[] spawnedPackages;
    public GameObject[] spawnedDeliveryZones;

    void Start()
    {
        if (robotPrefab == null)
        {
            Debug.LogWarning("Robot prefab not assigned! Looking for turtlebot3_waffle...");
            robotPrefab = GameObject.Find("turtlebot3_waffle");
        }
    }

    public void SpawnWarehouse()
    {
        ClearSpawned();

        SpawnRobots();
        SpawnPackages();
        SpawnDeliveryZones();

        Debug.Log($"Warehouse spawned: {numberOfRobots} robots, {numberOfPackages} packages, {numberOfDeliveryZones} zones");
    }

    void SpawnRobots()
    {
        spawnedRobots = new GameObject[numberOfRobots];

        for (int i = 0; i < numberOfRobots; i++)
        {
            Vector3 spawnPos = GetRandomPointInArea(robotSpawnAreaCenter, robotSpawnAreaSize);
            spawnPos.y = 0.1f;

            GameObject robot;
            if (i == 0 && robotPrefab != null && robotPrefab.scene.name == null)
            {
                // First robot - instantiate from prefab
                robot = Instantiate(robotPrefab, spawnPos, Quaternion.identity);
            }
            else if (i == 0 && robotPrefab != null)
            {
                // First robot - use existing robot in scene
                robot = robotPrefab;
                robot.transform.position = spawnPos;
                robot.transform.rotation = Quaternion.identity;
            }
            else
            {
                // Additional robots - duplicate the first one
                robot = Instantiate(spawnedRobots[0], spawnPos, Quaternion.identity);
            }

            robot.name = $"Robot_{i}";
            spawnedRobots[i] = robot;

            // Make sure it has the controller
            if (robot.GetComponent<SimpleTurtleBotController>() == null)
            {
                robot.AddComponent<SimpleTurtleBotController>();
            }
        }
    }

    void SpawnPackages()
    {
        spawnedPackages = new GameObject[numberOfPackages];

        for (int i = 0; i < numberOfPackages; i++)
        {
            Vector3 spawnPos = GetRandomPointInArea(packageSpawnAreaCenter, packageSpawnAreaSize);
            spawnPos.y = 0.1f;

            GameObject pkg;
            if (packagePrefab != null)
            {
                pkg = Instantiate(packagePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // Create package from scratch
                pkg = new GameObject($"Package_{i}");
                pkg.transform.position = spawnPos;
                Package packageScript = pkg.AddComponent<Package>();
                packageScript.packageID = i;
            }

            pkg.name = $"Package_{i}";
            Package pkgComponent = pkg.GetComponent<Package>();
            if (pkgComponent != null)
            {
                pkgComponent.packageID = i;
            }

            spawnedPackages[i] = pkg;
        }
    }

    void SpawnDeliveryZones()
    {
        spawnedDeliveryZones = new GameObject[numberOfDeliveryZones];

        float spacing = deliveryZoneAreaSize.x / (numberOfDeliveryZones + 1);

        for (int i = 0; i < numberOfDeliveryZones; i++)
        {
            Vector3 spawnPos = deliveryZoneAreaCenter;
            spawnPos.x = deliveryZoneAreaCenter.x - deliveryZoneAreaSize.x / 2f + spacing * (i + 1);
            spawnPos.y = 0;

            GameObject zone;
            if (deliveryZonePrefab != null)
            {
                zone = Instantiate(deliveryZonePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // Create zone from scratch
                zone = new GameObject($"DeliveryZone_{i}");
                zone.transform.position = spawnPos;
                DeliveryZone zoneScript = zone.AddComponent<DeliveryZone>();
                zoneScript.zoneID = i;

                // Set different colors for different zones
                Color[] colors = { Color.green, Color.blue, Color.magenta, Color.cyan };
                zoneScript.zoneColor = colors[i % colors.Length];
            }

            zone.name = $"DeliveryZone_{i}";
            DeliveryZone zoneComponent = zone.GetComponent<DeliveryZone>();
            if (zoneComponent != null)
            {
                zoneComponent.zoneID = i;
            }

            spawnedDeliveryZones[i] = zone;
        }
    }

    public void ClearSpawned()
    {
        if (spawnedRobots != null)
        {
            for (int i = 1; i < spawnedRobots.Length; i++) // Keep first robot
            {
                if (spawnedRobots[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(spawnedRobots[i]);
                    else
                        DestroyImmediate(spawnedRobots[i]);
                }
            }
        }

        if (spawnedPackages != null)
        {
            foreach (GameObject pkg in spawnedPackages)
            {
                if (pkg != null)
                {
                    if (Application.isPlaying)
                        Destroy(pkg);
                    else
                        DestroyImmediate(pkg);
                }
            }
        }

        if (spawnedDeliveryZones != null)
        {
            foreach (GameObject zone in spawnedDeliveryZones)
            {
                if (zone != null)
                {
                    if (Application.isPlaying)
                        Destroy(zone);
                    else
                        DestroyImmediate(zone);
                }
            }
        }
    }

    Vector3 GetRandomPointInArea(Vector3 center, Vector3 size)
    {
        return new Vector3(
            center.x + Random.Range(-size.x / 2f, size.x / 2f),
            center.y,
            center.z + Random.Range(-size.z / 2f, size.z / 2f)
        );
    }

    void OnDrawGizmos()
    {
        // Draw spawn areas
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(robotSpawnAreaCenter, robotSpawnAreaSize);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(packageSpawnAreaCenter, packageSpawnAreaSize);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(deliveryZoneAreaCenter, deliveryZoneAreaSize);
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SimpleWarehouseManager))]
    public class SimpleWarehouseManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SimpleWarehouseManager manager = (SimpleWarehouseManager)target;

            EditorGUILayout.Space();

            if (GUILayout.Button("Spawn Warehouse", GUILayout.Height(40)))
            {
                manager.SpawnWarehouse();
            }

            if (GUILayout.Button("Clear All Spawned Objects", GUILayout.Height(30)))
            {
                manager.ClearSpawned();
            }
        }
    }
#endif
}
