using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Comprehensive robot visibility and setup checker
/// </summary>
public class RobotVisibilityChecker : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/TurtleBot/FULL DIAGNOSTIC - Check Everything")]
    static void FullDiagnostic()
    {
        Debug.Log("========================================");
        Debug.Log("=== FULL TURTLEBOT3 DIAGNOSTIC ===");
        Debug.Log("========================================\n");

        GameObject robot = GameObject.Find("turtlebot3_waffle");

        if (robot == null)
        {
            Debug.LogError("❌ ROBOT NOT FOUND IN SCENE!");
            Debug.LogError("The turtlebot3_waffle GameObject doesn't exist.");
            Debug.LogError("You need to import the robot first.");
            return;
        }

        Debug.Log("✓ Robot GameObject found: " + robot.name);
        Debug.Log($"  Position: {robot.transform.position}");
        Debug.Log($"  Active: {robot.activeInHierarchy}");
        Debug.Log($"  Layer: {LayerMask.LayerToName(robot.layer)}\n");

        // Check visibility
        CheckVisibility(robot);

        // Check scripts
        CheckScripts(robot);

        // Check camera
        CheckCamera();

        // Check main camera position
        CheckMainCameraPosition(robot);

        Debug.Log("\n========================================");
        Debug.Log("=== DIAGNOSTIC COMPLETE ===");
        Debug.Log("========================================");
    }

    static void CheckVisibility(GameObject robot)
    {
        Debug.Log("--- CHECKING VISIBILITY ---");

        MeshRenderer[] renderers = robot.GetComponentsInChildren<MeshRenderer>(true);
        MeshFilter[] filters = robot.GetComponentsInChildren<MeshFilter>(true);

        Debug.Log($"MeshRenderers found: {renderers.Length}");
        Debug.Log($"MeshFilters found: {filters.Length}");

        if (renderers.Length == 0 && filters.Length == 0)
        {
            Debug.LogError("❌ NO MESHES! Robot is invisible!");
            Debug.LogError("FIX: Run 'Tools → TurtleBot → Import Meshes from Files' OR 'Create Simple Fallback Robot'");
            return;
        }

        // Check each renderer
        int visibleCount = 0;
        int invisibleCount = 0;

        foreach (MeshRenderer renderer in renderers)
        {
            bool isVisible = renderer.enabled &&
                           renderer.gameObject.activeInHierarchy &&
                           renderer.sharedMaterial != null;

            if (isVisible)
            {
                visibleCount++;
                Debug.Log($"  ✓ {renderer.gameObject.name} - VISIBLE (Material: {renderer.sharedMaterial.name})");
            }
            else
            {
                invisibleCount++;
                string reason = "";
                if (!renderer.enabled) reason = "Renderer disabled";
                else if (!renderer.gameObject.activeInHierarchy) reason = "GameObject inactive";
                else if (renderer.sharedMaterial == null) reason = "No material";

                Debug.LogWarning($"  ❌ {renderer.gameObject.name} - INVISIBLE ({reason})");
            }
        }

        if (visibleCount == 0)
        {
            Debug.LogError("❌ ALL PARTS ARE INVISIBLE!");
            Debug.LogError("FIX: Run 'Tools → TurtleBot → Fix Missing Materials' and 'Enable All Mesh Renderers'");
        }
        else if (invisibleCount > 0)
        {
            Debug.LogWarning($"⚠️ {invisibleCount} parts are invisible, {visibleCount} are visible");
            Debug.LogWarning("FIX: Run 'Tools → TurtleBot → Enable All Mesh Renderers'");
        }
        else
        {
            Debug.Log($"✓ ALL {visibleCount} parts are VISIBLE!");
        }

        Debug.Log("");
    }

    static void CheckScripts(GameObject robot)
    {
        Debug.Log("--- CHECKING SCRIPTS ---");

        MonoBehaviour[] scripts = robot.GetComponents<MonoBehaviour>();
        Debug.Log($"Scripts attached: {scripts.Length}");

        if (scripts.Length == 0)
        {
            Debug.LogError("❌ NO CONTROLLER SCRIPT! WASD won't work!");
            Debug.LogError("FIX: Run 'Tools → TurtleBot → Add Simple Controller'");
        }
        else
        {
            bool hasController = false;
            foreach (MonoBehaviour script in scripts)
            {
                if (script != null)
                {
                    Debug.Log($"  ✓ {script.GetType().Name}");
                    if (script.GetType().Name.Contains("Controller"))
                    {
                        hasController = true;
                    }
                }
                else
                {
                    Debug.LogWarning("  ❌ Missing Script (broken reference)");
                }
            }

            if (!hasController)
            {
                Debug.LogWarning("⚠️ No controller script found. WASD won't work!");
                Debug.LogWarning("FIX: Run 'Tools → TurtleBot → Add Simple Controller'");
            }
            else
            {
                Debug.Log("✓ Controller script found!");
            }
        }

        Debug.Log("");
    }

    static void CheckCamera()
    {
        Debug.Log("--- CHECKING CAMERAS ---");

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("❌ NO MAIN CAMERA in scene!");
        }
        else
        {
            Debug.Log($"✓ Main Camera found at: {mainCam.transform.position}");
        }

        GameObject robot = GameObject.Find("turtlebot3_waffle");
        Camera robotCam = robot.GetComponentInChildren<Camera>();

        if (robotCam == null)
        {
            Debug.LogWarning("⚠️ No robot camera found");
            Debug.LogWarning("FIX: Run 'Tools → TurtleBot → Add Camera to Robot'");
        }
        else
        {
            Debug.Log($"✓ Robot camera found: {robotCam.gameObject.name}");
            Debug.Log($"  Position: {robotCam.transform.position}");
            Debug.Log($"  Enabled: {robotCam.enabled}");
        }

        Debug.Log("");
    }

    static void CheckMainCameraPosition(GameObject robot)
    {
        Debug.Log("--- CHECKING CAMERA VIEW ---");

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("No main camera to check");
            return;
        }

        Vector3 robotPos = robot.transform.position;
        Vector3 camPos = mainCam.transform.position;

        float distance = Vector3.Distance(robotPos, camPos);
        Debug.Log($"Distance from camera to robot: {distance:F2}m");

        // Check if camera is looking at robot
        Vector3 dirToRobot = (robotPos - camPos).normalized;
        float dot = Vector3.Dot(mainCam.transform.forward, dirToRobot);

        if (dot < 0.5f)
        {
            Debug.LogWarning("⚠️ Camera is NOT looking at the robot!");
            Debug.LogWarning("FIX: In Scene view, navigate to see robot, then press Ctrl+Shift+F");
        }
        else
        {
            Debug.Log("✓ Camera is pointing toward the robot");
        }

        // Check if robot is in front of camera
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCam);
        Bounds robotBounds = new Bounds(robotPos, Vector3.one * 2); // Assume 2m cube around robot

        if (GeometryUtility.TestPlanesAABB(planes, robotBounds))
        {
            Debug.Log("✓ Robot is in camera frustum (should be visible in Game view)");
        }
        else
        {
            Debug.LogWarning("⚠️ Robot is OUTSIDE camera view!");
            Debug.LogWarning("FIX: Select Main Camera, look at robot in Scene view, press Ctrl+Shift+F");
        }

        Debug.Log("");
    }

    [MenuItem("Tools/TurtleBot/FIX ALL ISSUES")]
    static void FixAllIssues()
    {
        Debug.Log("========================================");
        Debug.Log("=== FIXING ALL ISSUES ===");
        Debug.Log("========================================\n");

        GameObject robot = GameObject.Find("turtlebot3_waffle");

        if (robot == null)
        {
            Debug.LogError("❌ Robot not found! Cannot fix.");
            return;
        }

        // Step 1: Check if meshes exist
        MeshRenderer[] renderers = robot.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning("No meshes found. Creating simple fallback robot...");
            // Call the fallback creator
            var method = typeof(TurtleBotMeshImporter).GetMethod("CreateSimpleFallbackRobot",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(null, null);
            }
            else
            {
                Debug.LogError("Could not find CreateSimpleFallbackRobot method");
            }

            // Re-find robot after recreation
            robot = GameObject.Find("turtlebot3_waffle");
            renderers = robot.GetComponentsInChildren<MeshRenderer>(true);
        }

        // Step 2: Fix materials
        Debug.Log("Fixing materials...");
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name.Contains("Error"))
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.3f, 0.3f, 0.3f);
                renderer.sharedMaterial = mat;
                Debug.Log($"  Fixed material for {renderer.gameObject.name}");
            }

            if (!renderer.enabled)
            {
                renderer.enabled = true;
                Debug.Log($"  Enabled renderer for {renderer.gameObject.name}");
            }
        }

        // Step 3: Add controller if missing
        SimpleTurtleBotController controller = robot.GetComponent<SimpleTurtleBotController>();
        if (controller == null)
        {
            Debug.Log("Adding controller...");
            controller = robot.AddComponent<SimpleTurtleBotController>();
            controller.moveSpeed = 2.0f;
            controller.rotationSpeed = 90.0f;
            Debug.Log("  ✓ Controller added");
        }
        else
        {
            Debug.Log("  ✓ Controller already exists");
        }

        // Step 4: Add camera if missing
        Camera robotCam = robot.GetComponentInChildren<Camera>();
        if (robotCam == null)
        {
            Debug.Log("Adding robot camera...");
            GameObject camObj = new GameObject("RobotCamera");
            camObj.transform.SetParent(robot.transform);
            camObj.transform.localPosition = new Vector3(0, 0.3f, 0.1f);
            camObj.transform.localRotation = Quaternion.identity;

            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 80f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.enabled = false;

            Debug.Log("  ✓ Camera added");
        }
        else
        {
            Debug.Log("  ✓ Camera already exists");
        }

        // Step 5: Position main camera to see robot
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 robotPos = robot.transform.position;
            mainCam.transform.position = robotPos + new Vector3(-5, 3, -5);
            mainCam.transform.LookAt(robotPos);
            Debug.Log("  ✓ Main camera positioned to view robot");
        }

        Debug.Log("\n========================================");
        Debug.Log("=== ALL FIXES APPLIED ===");
        Debug.Log("Press PLAY and try WASD to move!");
        Debug.Log("Press C to toggle camera view");
        Debug.Log("========================================");

        Selection.activeGameObject = robot;
    }

    [MenuItem("Tools/TurtleBot/Show Robot in Scene View")]
    static void ShowRobotInScene()
    {
        GameObject robot = GameObject.Find("turtlebot3_waffle");
        if (robot == null)
        {
            Debug.LogError("Robot not found!");
            return;
        }

        Selection.activeGameObject = robot;
        SceneView.FrameLastActiveSceneView();

        Debug.Log("Robot selected and framed in Scene view.");
        Debug.Log("If you can see it in Scene view but not Game view:");
        Debug.Log("  1. Keep looking at robot in Scene view");
        Debug.Log("  2. Select Main Camera in Hierarchy");
        Debug.Log("  3. Press Ctrl+Shift+F (or Cmd+Shift+F on Mac)");
    }
#endif
}
