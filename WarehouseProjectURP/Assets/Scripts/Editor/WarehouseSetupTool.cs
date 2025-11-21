using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// Unity Editor tool to automatically configure warehouse settings
/// Access via: Tools → Warehouse Setup
/// </summary>
public class WarehouseSetupTool : EditorWindow
{
    private QMIXWarehouseEnvironment environment;
    private GameObject floorObject;

    [MenuItem("Tools/Warehouse Setup")]
    public static void ShowWindow()
    {
        GetWindow<WarehouseSetupTool>("Warehouse Setup Tool");
    }

    void OnGUI()
    {
        GUILayout.Label("Warehouse Configuration Tool", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Auto-find environment
        if (environment == null)
        {
            environment = FindObjectOfType<QMIXWarehouseEnvironment>();
        }

        // Display environment info
        EditorGUILayout.HelpBox(
            "This tool will automatically:\n" +
            "1. Assign realistic box prefabs\n" +
            "2. Adjust floor size to match grid\n" +
            "3. Configure package spawning",
            MessageType.Info);

        GUILayout.Space(10);

        // Environment field
        environment = (QMIXWarehouseEnvironment)EditorGUILayout.ObjectField(
            "Warehouse Environment",
            environment,
            typeof(QMIXWarehouseEnvironment),
            true);

        // Floor field
        floorObject = (GameObject)EditorGUILayout.ObjectField(
            "Floor/Ground Object",
            floorObject,
            typeof(GameObject),
            true);

        GUILayout.Space(20);

        // Button to configure packages
        GUI.enabled = environment != null;
        if (GUILayout.Button("Configure Realistic Packages", GUILayout.Height(40)))
        {
            ConfigurePackages();
        }

        GUILayout.Space(10);

        // Button to adjust floor
        GUI.enabled = environment != null;
        if (GUILayout.Button("Adjust Floor Size to Match Grid", GUILayout.Height(40)))
        {
            AdjustFloorSize();
        }

        GUILayout.Space(10);

        // Button to position agents
        GUI.enabled = environment != null;
        if (GUILayout.Button("Position Agents Inside Warehouse", GUILayout.Height(40)))
        {
            PositionAgentsInGrid();
        }

        GUILayout.Space(10);

        // Button to enable 3D LIDAR point cloud (Mesh - Recommended)
        GUI.enabled = environment != null;
        if (GUILayout.Button("📡 Enable LIDAR Point Cloud (GPU-Accelerated)", GUILayout.Height(40)))
        {
            EnableLIDARPointCloudMesh();
        }

        GUILayout.Space(10);

        // Button to enable simple point cloud (for comparison)
        GUI.enabled = environment != null;
        if (GUILayout.Button("Enable LIDAR Point Cloud (Simple - Old Method)", GUILayout.Height(30)))
        {
            EnableSimpleLIDARPointCloud();
        }

        GUILayout.Space(10);

        // Button to fix package spawning
        GUI.enabled = environment != null;
        if (GUILayout.Button("🔧 Fix Package Spawning Bug", GUILayout.Height(40)))
        {
            FixPackageSpawning();
        }

        GUILayout.Space(10);

        // Button to do everything
        GUI.enabled = environment != null;
        if (GUILayout.Button("🚀 Configure Everything", GUILayout.Height(50)))
        {
            ConfigurePackages();
            AdjustFloorSize();
            PositionAgentsInGrid();
            FixPackageSpawning();
            EnableLIDARPointCloudMesh(); // Use proper GPU-accelerated version
            ModifySpawnScript();
        }

        GUI.enabled = true;

        GUILayout.Space(20);

        // Display current settings
        if (environment != null)
        {
            EditorGUILayout.LabelField("Current Settings:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Grid Size: {environment.gridWidth} × {environment.gridHeight}");
            EditorGUILayout.LabelField($"Package Prefab: {(environment.packagePrefab != null ? environment.packagePrefab.name : "None")}");
            EditorGUILayout.LabelField($"Number of Packages: {environment.numberOfPackages}");
            EditorGUILayout.LabelField($"Number of Agents: {(environment.agents != null ? environment.agents.Length : 0)}");
        }
    }

    void ConfigurePackages()
    {
        if (environment == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a QMIXWarehouseEnvironment", "OK");
            return;
        }

        // Try to load realistic box prefab
        GameObject boxPrefab = Resources.Load<GameObject>("Prefabs/box001");

        if (boxPrefab == null)
        {
            // Try alternative path
            boxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.unity.robotics.warehouse.urp/Resources/Prefabs/box001.prefab");
        }

        if (boxPrefab != null)
        {
            Undo.RecordObject(environment, "Configure Packages");
            environment.packagePrefab = boxPrefab;
            EditorUtility.SetDirty(environment);

            Debug.Log($"✅ Assigned realistic box prefab: {boxPrefab.name}");
            EditorUtility.DisplayDialog(
                "Success",
                "Realistic box prefab (box001) has been assigned!\n\nPackages will now spawn as realistic cardboard boxes.",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Warning",
                "Could not find box001.prefab in Resources/Prefabs.\n\nMake sure the com.unity.robotics.warehouse.urp package is installed.",
                "OK");
        }
    }

    void AdjustFloorSize()
    {
        if (environment == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a QMIXWarehouseEnvironment", "OK");
            return;
        }

        // Try to find floor automatically if not assigned
        if (floorObject == null)
        {
            // Search for common floor names
            string[] floorNames = { "Floors", "Floor", "Ground", "Plane", "Floor01", "Floor01(Clone)", "GeneratedWarehouse" };
            foreach (string name in floorNames)
            {
                GameObject found = GameObject.Find(name);
                if (found != null)
                {
                    floorObject = found;
                    Debug.Log($"✅ Found floor object: {found.name}");
                    break;
                }
            }

            if (floorObject == null)
            {
                Debug.Log("⚠️ Didn't find floor by name, searching all GameObjects...");
            }

            // If still not found, search for any plane
            if (floorObject == null)
            {
                MeshFilter[] meshFilters = FindObjectsOfType<MeshFilter>();
                foreach (var mf in meshFilters)
                {
                    if (mf.sharedMesh != null && mf.sharedMesh.name.Contains("Plane"))
                    {
                        floorObject = mf.gameObject;
                        break;
                    }
                }
            }
        }

        if (floorObject == null)
        {
            // Create a new floor
            bool createNew = EditorUtility.DisplayDialog(
                "No Floor Found",
                "No floor object found. Would you like to create one?",
                "Yes, Create Floor",
                "No");

            if (createNew)
            {
                floorObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floorObject.name = "Floor";
                Undo.RegisterCreatedObjectUndo(floorObject, "Create Floor");
            }
            else
            {
                return;
            }
        }

        // Calculate floor size and position based on grid
        float gridWidth = environment.gridWidth;
        float gridHeight = environment.gridHeight;

        // Unity plane default is 10x10 units
        // Scale = gridSize / 10
        Vector3 newScale = new Vector3(gridWidth / 10f, 1f, gridHeight / 10f);
        Vector3 newPosition = new Vector3(gridWidth / 2f, -0.01f, gridHeight / 2f);

        Undo.RecordObject(floorObject.transform, "Adjust Floor Size");
        floorObject.transform.localScale = newScale;
        floorObject.transform.position = newPosition;
        EditorUtility.SetDirty(floorObject);

        Debug.Log($"✅ Adjusted floor size to match {gridWidth}×{gridHeight} grid");
        Debug.Log($"   Scale: {newScale}, Position: {newPosition}");

        EditorUtility.DisplayDialog(
            "Success",
            $"Floor adjusted to match {gridWidth}×{gridHeight} warehouse!\n\n" +
            $"Scale: {newScale.x:F1} × {newScale.z:F1}\n" +
            $"Position: ({newPosition.x:F1}, {newPosition.y:F2}, {newPosition.z:F1})",
            "OK");
    }

    void ModifySpawnScript()
    {
        if (environment == null) return;

        // Find the script file
        string scriptPath = "Assets/Scripts/QMIXWarehouseEnvironment.cs";
        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);

        if (script != null)
        {
            string scriptContent = script.text;

            // Check if already modified
            if (scriptContent.Contains("localScale = new Vector3(0.5f, 0.5f, 0.5f)"))
            {
                Debug.Log("✅ Script already contains package scaling code");
                return;
            }

            // Add scaling code
            string searchPattern = "pkgObj = Instantiate(packagePrefab, spawnPos, Quaternion.identity, transform);";
            string replacement = @"pkgObj = Instantiate(packagePrefab, spawnPos, Quaternion.identity, transform);
                // Scale down realistic boxes to pickupable size
                pkgObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);";

            if (scriptContent.Contains(searchPattern))
            {
                scriptContent = scriptContent.Replace(searchPattern, replacement);
                System.IO.File.WriteAllText(scriptPath, scriptContent);
                AssetDatabase.Refresh();

                Debug.Log("✅ Modified QMIXWarehouseEnvironment.cs to scale packages");
                EditorUtility.DisplayDialog(
                    "Script Modified",
                    "QMIXWarehouseEnvironment.cs has been updated to scale packages to 0.5x.\n\nUnity will recompile the script.",
                    "OK");
            }
            else
            {
                Debug.LogWarning("Could not find the expected code pattern in script. Script may have been modified.");
            }
        }
        else
        {
            Debug.LogWarning("Could not find QMIXWarehouseEnvironment.cs at: " + scriptPath);
        }
    }

    void PositionAgentsInGrid()
    {
        if (environment == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a QMIXWarehouseEnvironment", "OK");
            return;
        }

        if (environment.agents == null || environment.agents.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "No agents found in environment!\n\nMake sure agents are assigned in the Agents array.", "OK");
            return;
        }

        float gridWidth = environment.gridWidth;
        float gridHeight = environment.gridHeight;

        // Calculate grid bounds (warehouse is CENTERED at origin 0,0)
        // For 50x50 grid: X goes from -25 to +25, Z goes from -25 to +25
        float minX = -gridWidth / 2f + 2f; // Stay 2 units away from edge
        float maxX = gridWidth / 2f - 2f;
        float minZ = -gridHeight / 2f + 2f;
        float maxZ = gridHeight / 2f - 2f;

        int agentCount = environment.agents.Length;

        // Calculate grid layout for agents
        int cols = Mathf.CeilToInt(Mathf.Sqrt(agentCount));
        int rows = Mathf.CeilToInt((float)agentCount / cols);

        float spacingX = (maxX - minX) / (cols + 1);
        float spacingZ = (maxZ - minZ) / (rows + 1);

        Debug.Log($"Positioning {agentCount} agents in {rows}x{cols} grid");
        Debug.Log($"Warehouse bounds: X({minX} to {maxX}), Z({minZ} to {maxZ})");

        int agentIndex = 0;
        for (int row = 0; row < rows && agentIndex < agentCount; row++)
        {
            for (int col = 0; col < cols && agentIndex < agentCount; col++)
            {
                QMIXWarehouseAgent agent = environment.agents[agentIndex];
                if (agent != null)
                {
                    Vector3 newPos = new Vector3(
                        minX + spacingX * (col + 1),
                        0.5f, // Y position (height above ground)
                        minZ + spacingZ * (row + 1)
                    );

                    Undo.RecordObject(agent.transform, "Position Agent");
                    agent.transform.position = newPos;
                    EditorUtility.SetDirty(agent.gameObject);

                    Debug.Log($"Agent {agentIndex} positioned at ({newPos.x:F1}, {newPos.y:F1}, {newPos.z:F1})");
                }
                agentIndex++;
            }
        }

        EditorUtility.DisplayDialog(
            "Success",
            $"Positioned {agentCount} agents inside the {gridWidth}×{gridHeight} warehouse!\n\n" +
            $"All agents are now evenly distributed within the boundaries.",
            "OK");
    }

    void EnableLIDARPointCloudMesh()
    {
        if (environment == null || environment.agents == null || environment.agents.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "No agents found!", "OK");
            return;
        }

        // Get first agent
        QMIXWarehouseAgent firstAgent = environment.agents[0];
        if (firstAgent == null)
        {
            EditorUtility.DisplayDialog("Error", "First agent is null!", "OK");
            return;
        }

        // Check if LIDAR sensor exists
        var lidarSensor = firstAgent.GetComponent<WarehouseRobotics.LIDARSensor>();
        if (lidarSensor == null)
        {
            // Add LIDAR sensor
            lidarSensor = Undo.AddComponent<WarehouseRobotics.LIDARSensor>(firstAgent.gameObject);
            Debug.Log($"✅ Added LIDARSensor to {firstAgent.name}");
        }

        // Enable visualization
        Undo.RecordObject(lidarSensor, "Enable LIDAR Sensor");
        lidarSensor.visualizeRays = true;
        EditorUtility.SetDirty(lidarSensor);

        // Check if LIDARPointCloudMesh exists
        var pointCloud = firstAgent.GetComponent<LIDARPointCloudMesh>();
        if (pointCloud == null)
        {
            // Add GPU-accelerated mesh-based point cloud
            pointCloud = Undo.AddComponent<LIDARPointCloudMesh>(firstAgent.gameObject);
            Debug.Log($"✅ Added LIDARPointCloudMesh to {firstAgent.name}");
        }

        // Configure point cloud
        Undo.RecordObject(pointCloud, "Configure LIDAR Point Cloud");
        pointCloud.showPointCloud = true;
        pointCloud.pointSize = 0.15f; // Visible size
        pointCloud.useFullResolution = false; // Start with 36 points
        EditorUtility.SetDirty(pointCloud);

        EditorUtility.DisplayDialog(
            "Success",
            $"GPU-Accelerated LIDAR Point Cloud enabled on {firstAgent.name}!\n\n" +
            "✅ Using proper mesh-based rendering (NOT GameObjects!)\n\n" +
            "You will see:\n" +
            "• Billboard quads at each LIDAR detection point\n" +
            "• 🔴 Red = Close (<1m)\n" +
            "• 🟡 Yellow = Medium (1-2m)\n" +
            "• 🟢 Green = Far (2-3.5m)\n" +
            "• GPU-accelerated, can handle 1000+ points\n" +
            "• Quads always face camera\n" +
            "• Real-time updates\n\n" +
            "Press Play to see it in action!\n\n" +
            "This is the PROPER Unity way to do point clouds!",
            "OK");
    }

    void EnableSimpleLIDARPointCloud()
    {
        if (environment == null || environment.agents == null || environment.agents.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "No agents found!", "OK");
            return;
        }

        // Get first agent
        QMIXWarehouseAgent firstAgent = environment.agents[0];
        if (firstAgent == null)
        {
            EditorUtility.DisplayDialog("Error", "First agent is null!", "OK");
            return;
        }

        // Check if LIDAR sensor exists
        var lidarSensor = firstAgent.GetComponent<WarehouseRobotics.LIDARSensor>();
        if (lidarSensor == null)
        {
            // Add LIDAR sensor
            lidarSensor = Undo.AddComponent<WarehouseRobotics.LIDARSensor>(firstAgent.gameObject);
            Debug.Log($"✅ Added LIDARSensor to {firstAgent.name}");
        }

        // Enable visualization
        Undo.RecordObject(lidarSensor, "Enable LIDAR Sensor");
        lidarSensor.visualizeRays = true;
        EditorUtility.SetDirty(lidarSensor);

        // Check if SimpleLIDARPointCloud exists
        var pointCloud = firstAgent.GetComponent<SimpleLIDARPointCloud>();
        if (pointCloud == null)
        {
            // Add simple 3D point cloud visualization
            pointCloud = Undo.AddComponent<SimpleLIDARPointCloud>(firstAgent.gameObject);
            Debug.Log($"✅ Added SimpleLIDARPointCloud to {firstAgent.name}");
        }

        // Configure point cloud
        Undo.RecordObject(pointCloud, "Configure LIDAR Point Cloud");
        pointCloud.showPointCloud = true;
        pointCloud.pointSize = 0.15f;
        pointCloud.numberOfPoints = 36;
        EditorUtility.SetDirty(pointCloud);

        EditorUtility.DisplayDialog(
            "Warning",
            $"Simple LIDAR Point Cloud enabled on {firstAgent.name}\n\n" +
            "⚠️ This uses GameObject spheres (OLD METHOD)\n" +
            "❌ Not recommended - limited to ~50 points\n" +
            "❌ Very inefficient\n\n" +
            "Use the GPU-Accelerated option instead for proper Unity rendering!\n\n" +
            "This option is only for comparison/testing.",
            "OK");
    }

    void FixPackageSpawning()
    {
        if (environment == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a QMIXWarehouseEnvironment", "OK");
            return;
        }

        // Add PackageSpawningFix to environment
        var packageFix = environment.GetComponent<PackageSpawningFix>();
        if (packageFix == null)
        {
            packageFix = Undo.AddComponent<PackageSpawningFix>(environment.gameObject);
            Debug.Log("✅ Added PackageSpawningFix to environment");
        }

        Undo.RecordObject(packageFix, "Configure Package Fix");
        packageFix.maxPackages = environment.numberOfPackages + 5; // Some buffer
        packageFix.cleanupInterval = 2f;
        EditorUtility.SetDirty(packageFix);

        // Also try to find WarehouseManager with ScenarioShim
        var warehouseManager = FindObjectOfType<Unity.Simulation.Warehouse.WarehouseManager>();
        if (warehouseManager != null)
        {
            var scenarioShim = warehouseManager.GetComponent<Unity.Robotics.PerceptionRandomizers.Shims.ScenarioShim>();
            if (scenarioShim != null)
            {
                // Add the disabler component as backup
                var disabler = warehouseManager.GetComponent<DisableRandomizersInPlayMode>();
                if (disabler == null)
                {
                    disabler = Undo.AddComponent<DisableRandomizersInPlayMode>(warehouseManager.gameObject);
                    Debug.Log("✅ Added DisableRandomizersInPlayMode to WarehouseManager");
                }

                Undo.RecordObject(disabler, "Enable Randomizer Disabler");
                disabler.disableOnPlay = true;
                EditorUtility.SetDirty(disabler);
            }
        }

        EditorUtility.DisplayDialog(
            "Success",
            "Fixed package spawning bug!\n\n" +
            "Added PackageSpawningFix component that will:\n" +
            "• Disable all perception randomizers on play\n" +
            "• Monitor package count every 2 seconds\n" +
            "• Automatically cleanup extra packages\n\n" +
            $"Max packages set to: {packageFix.maxPackages}\n\n" +
            "Only your pickupable packages will spawn now!",
            "OK");
    }
}
