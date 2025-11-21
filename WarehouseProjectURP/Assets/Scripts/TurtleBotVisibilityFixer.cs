using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Diagnostic and fix tool for TurtleBot3 visibility issues
/// </summary>
public class TurtleBotVisibilityFixer : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/TurtleBot/Diagnose Visibility Issues")]
    static void DiagnoseVisibility()
    {
        Debug.Log("=== TurtleBot3 Visibility Diagnostic ===");

        // Find all GameObjects with "turtlebot" or "waffle" in name
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        bool foundRobot = false;

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.ToLower().Contains("turtlebot") ||
                obj.name.ToLower().Contains("waffle") ||
                obj.name.ToLower().Contains("burger"))
            {
                foundRobot = true;
                Debug.Log($"\n--- Found Robot: {obj.name} ---");
                Debug.Log($"Position: {obj.transform.position}");
                Debug.Log($"Active: {obj.activeInHierarchy}");
                Debug.Log($"Layer: {LayerMask.LayerToName(obj.layer)}");

                // Check for mesh renderers
                MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>(true);
                Debug.Log($"MeshRenderers found: {renderers.Length}");

                int enabledRenderers = 0;
                int renderersWithMaterial = 0;
                int renderersWithNullMaterial = 0;

                foreach (MeshRenderer renderer in renderers)
                {
                    if (renderer.enabled) enabledRenderers++;

                    if (renderer.sharedMaterial != null)
                    {
                        renderersWithMaterial++;
                        Debug.Log($"  - {renderer.gameObject.name}: Material={renderer.sharedMaterial.name}, Shader={renderer.sharedMaterial.shader.name}");
                    }
                    else
                    {
                        renderersWithNullMaterial++;
                        Debug.LogWarning($"  - {renderer.gameObject.name}: NO MATERIAL!");
                    }
                }

                Debug.Log($"Enabled Renderers: {enabledRenderers}/{renderers.Length}");
                Debug.Log($"Renderers with Material: {renderersWithMaterial}");
                Debug.Log($"Renderers with NULL Material: {renderersWithNullMaterial}");

                // Check mesh filters
                MeshFilter[] filters = obj.GetComponentsInChildren<MeshFilter>(true);
                Debug.Log($"MeshFilters found: {filters.Length}");

                int filtersWithMesh = 0;
                foreach (MeshFilter filter in filters)
                {
                    if (filter.sharedMesh != null)
                    {
                        filtersWithMesh++;
                        Debug.Log($"  - {filter.gameObject.name}: Mesh={filter.sharedMesh.name}, Vertices={filter.sharedMesh.vertexCount}");
                    }
                    else
                    {
                        Debug.LogWarning($"  - {filter.gameObject.name}: NO MESH!");
                    }
                }

                Debug.Log($"MeshFilters with Mesh: {filtersWithMesh}/{filters.Length}");
            }
        }

        if (!foundRobot)
        {
            Debug.LogWarning("No TurtleBot3 found in scene! Make sure the robot is instantiated.");
        }

        Debug.Log("\n=== Diagnostic Complete ===");
    }

    [MenuItem("Tools/TurtleBot/Fix Missing Materials")]
    static void FixMissingMaterials()
    {
        Debug.Log("=== Fixing TurtleBot3 Materials ===");

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int fixedCount = 0;

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.ToLower().Contains("turtlebot") ||
                obj.name.ToLower().Contains("waffle") ||
                obj.name.ToLower().Contains("burger"))
            {
                MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>(true);

                foreach (MeshRenderer renderer in renderers)
                {
                    if (renderer.sharedMaterial == null ||
                        renderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader")
                    {
                        // Create a default URP material
                        Material newMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

                        // Set a default color based on the part name
                        string partName = renderer.gameObject.name.ToLower();
                        if (partName.Contains("base"))
                        {
                            newMat.color = new Color(0.3f, 0.3f, 0.3f); // Dark gray
                        }
                        else if (partName.Contains("wheel") || partName.Contains("tire"))
                        {
                            newMat.color = new Color(0.1f, 0.1f, 0.1f); // Black
                        }
                        else if (partName.Contains("sensor") || partName.Contains("lidar") || partName.Contains("camera"))
                        {
                            newMat.color = new Color(0.2f, 0.2f, 0.2f); // Dark gray
                        }
                        else
                        {
                            newMat.color = new Color(0.5f, 0.5f, 0.5f); // Gray
                        }

                        renderer.sharedMaterial = newMat;
                        fixedCount++;

                        Debug.Log($"Fixed material for: {renderer.gameObject.name}");
                    }
                }
            }
        }

        Debug.Log($"=== Fixed {fixedCount} materials ===");
    }

    [MenuItem("Tools/TurtleBot/Enable All Mesh Renderers")]
    static void EnableAllRenderers()
    {
        Debug.Log("=== Enabling All TurtleBot3 Renderers ===");

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int enabledCount = 0;

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.ToLower().Contains("turtlebot") ||
                obj.name.ToLower().Contains("waffle") ||
                obj.name.ToLower().Contains("burger"))
            {
                MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>(true);

                foreach (MeshRenderer renderer in renderers)
                {
                    if (!renderer.enabled)
                    {
                        renderer.enabled = true;
                        enabledCount++;
                        Debug.Log($"Enabled renderer for: {renderer.gameObject.name}");
                    }
                }

                // Also activate the GameObject if it's inactive
                if (!obj.activeInHierarchy)
                {
                    obj.SetActive(true);
                    Debug.Log($"Activated GameObject: {obj.name}");
                }
            }
        }

        Debug.Log($"=== Enabled {enabledCount} renderers ===");
    }

    [MenuItem("Tools/TurtleBot/Add Camera to Robot")]
    static void AddCameraToRobot()
    {
        Debug.Log("=== Adding Camera to TurtleBot3 ===");

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.ToLower().Contains("turtlebot") ||
                obj.name.ToLower().Contains("waffle") ||
                obj.name.ToLower().Contains("burger"))
            {
                // Check if camera already exists
                Camera existingCamera = obj.GetComponentInChildren<Camera>();
                if (existingCamera != null)
                {
                    Debug.Log($"Camera already exists on {obj.name}: {existingCamera.gameObject.name}");
                    continue;
                }

                // Create camera GameObject
                GameObject cameraObj = new GameObject("RobotCamera");
                cameraObj.transform.SetParent(obj.transform);
                cameraObj.transform.localPosition = new Vector3(0, 0.3f, 0.1f); // Adjust height
                cameraObj.transform.localRotation = Quaternion.identity;

                // Add camera component
                Camera cam = cameraObj.AddComponent<Camera>();
                cam.fieldOfView = 80f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 50f;
                cam.enabled = false; // Start disabled

                Debug.Log($"Added camera to {obj.name}");
                Debug.Log("Press C in Play mode to toggle camera (you'll need to add a script for this)");
            }
        }

        Debug.Log("=== Camera addition complete ===");
    }

    [MenuItem("Tools/TurtleBot/Check What Scripts Are Attached")]
    static void CheckScripts()
    {
        Debug.Log("=== Checking TurtleBot3 Scripts ===");

        GameObject robot = GameObject.Find("turtlebot3_waffle");
        if (robot == null)
        {
            Debug.LogError("TurtleBot3 not found!");
            return;
        }

        MonoBehaviour[] scripts = robot.GetComponents<MonoBehaviour>();
        Debug.Log($"Found {scripts.Length} scripts on turtlebot3_waffle:");

        if (scripts.Length == 0)
        {
            Debug.LogWarning("NO SCRIPTS ATTACHED! This is why WASD doesn't work.");
            Debug.LogWarning("You need to add a controller script!");
            Debug.LogWarning("Use: Tools → TurtleBot → Add Simple Controller");
        }
        else
        {
            foreach (MonoBehaviour script in scripts)
            {
                if (script != null)
                {
                    Debug.Log($"  - {script.GetType().Name}");
                }
                else
                {
                    Debug.LogWarning("  - (Missing Script) <-- THIS IS A PROBLEM!");
                }
            }
        }

        Debug.Log("=== Script check complete ===");
    }

    [MenuItem("Tools/TurtleBot/Add Simple Controller")]
    static void AddSimpleController()
    {
        Debug.Log("=== Adding Simple Controller ===");

        GameObject robot = GameObject.Find("turtlebot3_waffle");
        if (robot == null)
        {
            Debug.LogError("TurtleBot3 not found!");
            return;
        }

        // Check if controller already exists
        SimpleTurtleBotController existing = robot.GetComponent<SimpleTurtleBotController>();
        if (existing != null)
        {
            Debug.LogWarning("SimpleTurtleBotController already exists!");
            return;
        }

        // Add the controller
        SimpleTurtleBotController controller = robot.AddComponent<SimpleTurtleBotController>();
        controller.moveSpeed = 2.0f;
        controller.rotationSpeed = 90.0f;

        Debug.Log("=== Simple controller added! ===");
        Debug.Log("Press Play and use WASD to move, C to toggle camera");

        Selection.activeGameObject = robot;
    }
#endif
}
