using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor tool to set up packages in the warehouse
/// Tools -> Warehouse -> Setup Packages
/// </summary>
public class PackageSetupTool : EditorWindow
{
    [MenuItem("Tools/Warehouse/Setup Packages")]
    public static void ShowWindow()
    {
        GetWindow<PackageSetupTool>("Package Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("Warehouse Package Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("Configure box1-box8 for QMIX training", EditorStyles.helpBox);
        GUILayout.Space(10);

        if (GUILayout.Button("Add Package & PackageHomePosition to box1-box8", GUILayout.Height(40)))
        {
            SetupPackages();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Configure Grid Dimensions", GUILayout.Height(40)))
        {
            ConfigureGrid();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("List All Package Positions", GUILayout.Height(40)))
        {
            ListPackages();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Verify Environment Setup", GUILayout.Height(40)))
        {
            VerifySetup();
        }
    }

    void SetupPackages()
    {
        // Find all boxes (box1 through box8)
        string[] boxNames = { "box1", "box2", "box3", "box4", "box5", "box6", "box7", "box8" };
        int setupCount = 0;
        List<string> notFound = new List<string>();

        foreach (string boxName in boxNames)
        {
            GameObject box = GameObject.Find(boxName);
            if (box == null)
            {
                notFound.Add(boxName);
                continue;
            }

            // Add Package component if missing
            Package pkg = box.GetComponent<Package>();
            if (pkg == null)
            {
                pkg = box.AddComponent<Package>();
                Debug.Log($"Added Package component to {boxName}");
            }

            // Add PackageHomePosition component if missing
            PackageHomePosition homePos = box.GetComponent<PackageHomePosition>();
            if (homePos == null)
            {
                homePos = box.AddComponent<PackageHomePosition>();
                Debug.Log($"Added PackageHomePosition component to {boxName}");
            }

            // Set the package reference
            homePos.currentPackage = pkg;
            pkg.homePosition = homePos;

            // Make sure it's not marked as delivered or picked up
            pkg.isDelivered = false;
            pkg.isPickedUp = false;

            // Mark as dirty for Unity to save changes
            EditorUtility.SetDirty(box);

            setupCount++;
        }

        // Mark scene as dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        string message = $"Configured {setupCount}/8 packages with Package and PackageHomePosition components";
        if (notFound.Count > 0)
        {
            message += $"\n\nNot found: {string.Join(", ", notFound)}";
        }

        Debug.Log($"<color=green>Setup complete! {message}</color>");
        EditorUtility.DisplayDialog("Success", message, "OK");
    }

    void ConfigureGrid()
    {
        // Find the environment
        QMIXWarehouseEnvironment env = FindObjectOfType<QMIXWarehouseEnvironment>();
        if (env == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find QMIXWarehouseEnvironment in scene", "OK");
            return;
        }

        // DYNAMICALLY read package positions from scene
        string[] boxNames = { "box1", "box2", "box3", "box4", "box5", "box6", "box7", "box8" };
        List<Vector3> packagePositions = new List<Vector3>();
        List<string> notFound = new List<string>();

        foreach (string boxName in boxNames)
        {
            GameObject box = GameObject.Find(boxName);
            if (box != null)
            {
                packagePositions.Add(box.transform.position);
            }
            else
            {
                notFound.Add(boxName);
            }
        }

        if (packagePositions.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No packages found! Make sure box1-box8 exist in the scene.", "OK");
            return;
        }

        // DYNAMICALLY read agent positions from scene (try both with and without spaces)
        string[] agentNames = { "turtlebot3_waffle", "turtlebot3_waffle(4)", "turtlebot3_waffle(5)" };
        string[] agentNamesWithSpace = { "turtlebot3_waffle", "turtlebot3_waffle (4)", "turtlebot3_waffle (5)" };
        List<Vector3> agentPositions = new List<Vector3>();

        for (int i = 0; i < agentNames.Length; i++)
        {
            GameObject agent = GameObject.Find(agentNames[i]);
            if (agent == null)
            {
                agent = GameObject.Find(agentNamesWithSpace[i]);
            }

            if (agent != null)
            {
                agentPositions.Add(agent.transform.position);
            }
        }

        // Calculate bounds including both packages and agents
        List<Vector3> allPositions = new List<Vector3>();
        allPositions.AddRange(packagePositions);
        allPositions.AddRange(agentPositions);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (Vector3 pos in allPositions)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.z < minZ) minZ = pos.z;
            if (pos.z > maxZ) maxZ = pos.z;
        }

        // Add 2-unit margins on each side
        minX -= 2f;
        maxX += 2f;
        minZ -= 2f;
        maxZ += 2f;

        // Calculate grid dimensions
        float width = maxX - minX;
        float height = maxZ - minZ;

        // Set grid dimensions (round up to nearest even number for cleaner grid)
        env.gridWidth = Mathf.Ceil(width);
        env.gridHeight = Mathf.Ceil(height);

        // Update request queue size to 4 (half of 8 packages = "normal" difficulty)
        env.requestQueueSize = 4;

        // Update number of packages
        env.numberOfPackages = 8;

        // Calculate center offset
        float centerX = (minX + maxX) / 2f;
        float centerZ = (minZ + maxZ) / 2f;

        EditorUtility.SetDirty(env);

        string configMessage = $"Grid Configuration:\n\n" +
            $"Width: {env.gridWidth} units\n" +
            $"Height: {env.gridHeight} units\n" +
            $"Packages: {env.numberOfPackages}\n" +
            $"Request Queue: {env.requestQueueSize}\n\n" +
            $"Warehouse Center: ({centerX:F2}, {centerZ:F2})\n" +
            $"X range: [{minX:F2}, {maxX:F2}]\n" +
            $"Z range: [{minZ:F2}, {maxZ:F2}]\n\n" +
            $"Note: Warehouse is offset from origin.";

        Debug.Log($"<color=cyan>{configMessage}</color>");
        EditorUtility.DisplayDialog("Grid Configured", configMessage, "OK");
    }

    void ListPackages()
    {
        string[] boxNames = { "box1", "box2", "box3", "box4", "box5", "box6", "box7", "box8" };

        Debug.Log("<color=yellow>=== Package Positions ===</color>");

        foreach (string boxName in boxNames)
        {
            GameObject box = GameObject.Find(boxName);
            if (box != null)
            {
                Vector3 pos = box.transform.position;
                Package pkg = box.GetComponent<Package>();
                PackageHomePosition homePos = box.GetComponent<PackageHomePosition>();

                string status = "";
                if (pkg != null && homePos != null)
                    status = "<color=green>[READY]</color>";
                else if (pkg == null && homePos == null)
                    status = "<color=red>[MISSING COMPONENTS]</color>";
                else
                    status = "<color=orange>[PARTIAL]</color>";

                Debug.Log($"{boxName}: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}) {status}");
            }
            else
            {
                Debug.LogWarning($"{boxName}: <color=red>NOT FOUND</color>");
            }
        }

        Debug.Log("<color=yellow>=== Agent Positions ===</color>");
        string[] agentNames = { "turtlebot3_waffle", "turtlebot3_waffle(4)", "turtlebot3_waffle(5)" };
        string[] agentNamesWithSpace = { "turtlebot3_waffle", "turtlebot3_waffle (4)", "turtlebot3_waffle (5)" };

        for (int i = 0; i < agentNames.Length; i++)
        {
            GameObject agent = GameObject.Find(agentNames[i]);
            if (agent == null)
            {
                agent = GameObject.Find(agentNamesWithSpace[i]);
            }

            if (agent != null)
            {
                Vector3 pos = agent.transform.position;
                QMIXWarehouseAgent agentComp = agent.GetComponent<QMIXWarehouseAgent>();
                string status = agentComp != null ? "<color=green>[READY]</color>" : "<color=red>[MISSING AGENT COMPONENT]</color>";
                Debug.Log($"{agent.name}: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}) {status}");
            }
            else
            {
                Debug.LogWarning($"{agentNames[i]} (or with space): <color=red>NOT FOUND</color>");
            }
        }

        Debug.Log("<color=yellow>=== Delivery Zones ===</color>");
        DeliveryZone[] zones = FindObjectsOfType<DeliveryZone>();
        foreach (DeliveryZone zone in zones)
        {
            Vector3 pos = zone.transform.position;
            Debug.Log($"{zone.gameObject.name}: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}) <color=green>[READY]</color>");
        }
    }

    void VerifySetup()
    {
        List<string> issues = new List<string>();
        List<string> warnings = new List<string>();
        int okCount = 0;

        // Check environment
        QMIXWarehouseEnvironment env = FindObjectOfType<QMIXWarehouseEnvironment>();
        if (env == null)
        {
            issues.Add("QMIXWarehouseEnvironment not found in scene");
        }
        else
        {
            okCount++;
            if (env.gridWidth < 20)
                warnings.Add($"Grid width is {env.gridWidth} (might be too small)");
            if (env.requestQueueSize != 4)
                warnings.Add($"Request queue size is {env.requestQueueSize} (recommended: 4 for 8 packages)");
        }

        // Check packages
        string[] boxNames = { "box1", "box2", "box3", "box4", "box5", "box6", "box7", "box8" };
        int packageCount = 0;
        foreach (string boxName in boxNames)
        {
            GameObject box = GameObject.Find(boxName);
            if (box == null)
            {
                issues.Add($"{boxName} not found in scene");
                continue;
            }

            Package pkg = box.GetComponent<Package>();
            PackageHomePosition homePos = box.GetComponent<PackageHomePosition>();

            if (pkg == null)
                issues.Add($"{boxName} missing Package component");
            if (homePos == null)
                issues.Add($"{boxName} missing PackageHomePosition component");

            if (pkg != null && homePos != null)
                packageCount++;
        }

        if (packageCount == 8)
            okCount++;

        // Check agents (try both with and without spaces)
        string[] agentNames = { "turtlebot3_waffle", "turtlebot3_waffle(4)", "turtlebot3_waffle(5)" };
        string[] agentNamesWithSpace = { "turtlebot3_waffle", "turtlebot3_waffle (4)", "turtlebot3_waffle (5)" };
        int agentCount = 0;

        for (int i = 0; i < agentNames.Length; i++)
        {
            GameObject agent = GameObject.Find(agentNames[i]);
            if (agent == null)
            {
                // Try with space
                agent = GameObject.Find(agentNamesWithSpace[i]);
            }

            if (agent == null)
            {
                issues.Add($"{agentNames[i]} (or with space) not found in scene");
                continue;
            }

            QMIXWarehouseAgent agentComp = agent.GetComponent<QMIXWarehouseAgent>();
            if (agentComp == null)
                issues.Add($"{agent.name} missing QMIXWarehouseAgent component");
            else
                agentCount++;
        }

        if (agentCount == 3)
            okCount++;

        // Check delivery zones
        DeliveryZone[] zones = FindObjectsOfType<DeliveryZone>();
        if (zones.Length < 2)
            issues.Add($"Only {zones.Length} delivery zones found (need 2)");
        else
            okCount++;

        // Build result message
        string result = $"<b>Setup Verification Results:</b>\n\n";
        result += $"✓ Components OK: {okCount}/4\n";
        result += $"✗ Issues: {issues.Count}\n";
        result += $"⚠ Warnings: {warnings.Count}\n\n";

        if (issues.Count > 0)
        {
            result += "<b>Issues Found:</b>\n";
            foreach (string issue in issues)
                result += $"• {issue}\n";
            result += "\n";
        }

        if (warnings.Count > 0)
        {
            result += "<b>Warnings:</b>\n";
            foreach (string warning in warnings)
                result += $"• {warning}\n";
        }

        if (issues.Count == 0 && warnings.Count == 0)
        {
            result += "<color=green><b>✓ Setup looks good! Ready for training.</b></color>";
        }

        Debug.Log(result.Replace("<b>", "").Replace("</b>", "").Replace("<color=green>", "").Replace("</color>", ""));
        EditorUtility.DisplayDialog("Setup Verification", result, "OK");
    }
}
