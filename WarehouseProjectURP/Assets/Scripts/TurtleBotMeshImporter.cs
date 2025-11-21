using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

/// <summary>
/// Imports TurtleBot3 mesh files and attaches them to the robot
/// </summary>
public class TurtleBotMeshImporter : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/TurtleBot/Import Meshes from Files")]
    static void ImportMeshes()
    {
        Debug.Log("=== Importing TurtleBot3 Meshes ===");

        // Find the TurtleBot3 in the scene
        GameObject robot = GameObject.Find("turtlebot3_waffle");
        if (robot == null)
        {
            Debug.LogError("TurtleBot3 'turtlebot3_waffle' not found in scene!");
            return;
        }

        string meshBasePath = "Assets/RobotModels/turtlebot3/turtlebot3_description/meshes";

        // Check if mesh directory exists
        if (!Directory.Exists(meshBasePath))
        {
            Debug.LogError($"Mesh directory not found at: {meshBasePath}");
            return;
        }

        Debug.Log($"Found mesh directory: {meshBasePath}");

        // Create mesh parts
        CreateBaseMesh(robot, meshBasePath);
        CreateWheelMeshes(robot, meshBasePath);
        CreateSensorMeshes(robot, meshBasePath);

        Debug.Log("=== Mesh import complete! ===");
        EditorUtility.SetDirty(robot);
    }

    static void CreateBaseMesh(GameObject robot, string meshBasePath)
    {
        string baseSTLPath = meshBasePath + "/bases/waffle_base.stl";

        Debug.Log($"Looking for base mesh at: {baseSTLPath}");

        // Check if STL exists
        if (!File.Exists(baseSTLPath))
        {
            Debug.LogWarning($"Base STL not found: {baseSTLPath}");
            // Create a simple cube as fallback
            CreateFallbackBase(robot);
            return;
        }

        // Load the mesh asset
        Mesh baseMesh = AssetDatabase.LoadAssetAtPath<Mesh>(baseSTLPath);

        if (baseMesh == null)
        {
            Debug.LogWarning($"Could not load mesh from {baseSTLPath}. Creating fallback.");
            CreateFallbackBase(robot);
            return;
        }

        // Find or create base link
        Transform baseLink = robot.transform.Find("base_link");
        if (baseLink == null)
        {
            GameObject baseLinkObj = new GameObject("base_link");
            baseLinkObj.transform.SetParent(robot.transform);
            baseLinkObj.transform.localPosition = Vector3.zero;
            baseLinkObj.transform.localRotation = Quaternion.identity;
            baseLink = baseLinkObj.transform;
        }

        // Add mesh components
        AddMeshToObject(baseLink.gameObject, baseMesh, new Color(0.3f, 0.3f, 0.3f));
        Debug.Log("Added base mesh");
    }

    static void CreateFallbackBase(GameObject robot)
    {
        Transform baseLink = robot.transform.Find("base_link");
        if (baseLink == null)
        {
            GameObject baseLinkObj = new GameObject("base_link");
            baseLinkObj.transform.SetParent(robot.transform);
            baseLinkObj.transform.localPosition = Vector3.zero;
            baseLink = baseLinkObj.transform;
        }

        // Create a simple box as the base
        GameObject baseMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseMesh.name = "waffle_base_visual";
        baseMesh.transform.SetParent(baseLink);
        baseMesh.transform.localPosition = new Vector3(0, 0.05f, 0);
        baseMesh.transform.localRotation = Quaternion.identity;
        baseMesh.transform.localScale = new Vector3(0.28f, 0.08f, 0.30f);

        MeshRenderer renderer = baseMesh.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.3f, 0.3f, 0.3f);
        renderer.sharedMaterial = mat;

        Debug.Log("Created fallback base (cube)");
    }

    static void CreateWheelMeshes(GameObject robot, string meshBasePath)
    {
        string wheelSTLPath = meshBasePath + "/wheels/left_tire.stl";

        // Check if wheel exists
        Mesh wheelMesh = null;
        if (File.Exists(wheelSTLPath))
        {
            wheelMesh = AssetDatabase.LoadAssetAtPath<Mesh>(wheelSTLPath);
        }

        // Wheel positions for Waffle
        CreateWheel(robot, "wheel_left_link", new Vector3(0, 0.033f, 0.144f), wheelMesh);
        CreateWheel(robot, "wheel_right_link", new Vector3(0, 0.033f, -0.144f), wheelMesh);

        Debug.Log("Created wheel meshes");
    }

    static void CreateWheel(GameObject robot, string name, Vector3 localPos, Mesh wheelMesh)
    {
        Transform wheelLink = robot.transform.Find(name);
        if (wheelLink == null)
        {
            GameObject wheelObj = new GameObject(name);
            wheelObj.transform.SetParent(robot.transform);
            wheelObj.transform.localPosition = localPos;
            wheelObj.transform.localRotation = Quaternion.Euler(0, 0, 90); // Rotate for wheel orientation
            wheelLink = wheelObj.transform;
        }

        if (wheelMesh != null)
        {
            AddMeshToObject(wheelLink.gameObject, wheelMesh, new Color(0.1f, 0.1f, 0.1f));
        }
        else
        {
            // Fallback: create cylinder
            GameObject wheelVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheelVisual.name = "wheel_visual";
            wheelVisual.transform.SetParent(wheelLink);
            wheelVisual.transform.localPosition = Vector3.zero;
            wheelVisual.transform.localRotation = Quaternion.Euler(90, 0, 0);
            wheelVisual.transform.localScale = new Vector3(0.066f, 0.018f, 0.066f);

            MeshRenderer renderer = wheelVisual.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.1f, 0.1f, 0.1f);
            renderer.sharedMaterial = mat;
        }
    }

    static void CreateSensorMeshes(GameObject robot, string meshBasePath)
    {
        // Create LiDAR (cylinder on top)
        CreateLiDAR(robot);

        Debug.Log("Created sensor meshes");
    }

    static void CreateLiDAR(GameObject robot)
    {
        Transform lidarLink = robot.transform.Find("base_scan");
        if (lidarLink == null)
        {
            GameObject lidarObj = new GameObject("base_scan");
            lidarObj.transform.SetParent(robot.transform);
            lidarObj.transform.localPosition = new Vector3(-0.064f, 0.122f, 0);
            lidarLink = lidarObj.transform;
        }

        // Create visual
        GameObject lidarVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lidarVisual.name = "lidar_visual";
        lidarVisual.transform.SetParent(lidarLink);
        lidarVisual.transform.localPosition = Vector3.zero;
        lidarVisual.transform.localRotation = Quaternion.identity;
        lidarVisual.transform.localScale = new Vector3(0.07f, 0.04f, 0.07f);

        MeshRenderer renderer = lidarVisual.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.1f, 0.1f, 0.1f);
        renderer.sharedMaterial = mat;

        // Remove collider
        Collider col = lidarVisual.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
    }

    static void AddMeshToObject(GameObject obj, Mesh mesh, Color color)
    {
        MeshFilter filter = obj.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = obj.AddComponent<MeshFilter>();
        }
        filter.sharedMesh = mesh;

        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = obj.AddComponent<MeshRenderer>();
        }

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        renderer.sharedMaterial = mat;
    }

    [MenuItem("Tools/TurtleBot/Create Simple Fallback Robot")]
    static void CreateSimpleFallbackRobot()
    {
        Debug.Log("=== Creating Simple TurtleBot3 Fallback ===");

        GameObject robot = GameObject.Find("turtlebot3_waffle");
        if (robot == null)
        {
            robot = new GameObject("turtlebot3_waffle");
            Debug.Log("Created new turtlebot3_waffle GameObject");
        }

        // Clear existing children
        while (robot.transform.childCount > 0)
        {
            DestroyImmediate(robot.transform.GetChild(0).gameObject);
        }

        // Create base
        GameObject baseMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseMesh.name = "base_link";
        baseMesh.transform.SetParent(robot.transform);
        baseMesh.transform.localPosition = new Vector3(0, 0.05f, 0);
        baseMesh.transform.localRotation = Quaternion.identity;
        baseMesh.transform.localScale = new Vector3(0.28f, 0.08f, 0.30f);

        MeshRenderer baseRenderer = baseMesh.GetComponent<MeshRenderer>();
        Material baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        baseMat.color = new Color(0.3f, 0.3f, 0.3f); // Dark gray
        baseRenderer.sharedMaterial = baseMat;

        // Create left wheel
        GameObject leftWheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leftWheel.name = "wheel_left";
        leftWheel.transform.SetParent(robot.transform);
        leftWheel.transform.localPosition = new Vector3(0, 0.033f, 0.144f);
        leftWheel.transform.localRotation = Quaternion.Euler(90, 0, 0);
        leftWheel.transform.localScale = new Vector3(0.066f, 0.018f, 0.066f);

        MeshRenderer leftRenderer = leftWheel.GetComponent<MeshRenderer>();
        Material wheelMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        wheelMat.color = new Color(0.1f, 0.1f, 0.1f); // Black
        leftRenderer.sharedMaterial = wheelMat;

        // Create right wheel
        GameObject rightWheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rightWheel.name = "wheel_right";
        rightWheel.transform.SetParent(robot.transform);
        rightWheel.transform.localPosition = new Vector3(0, 0.033f, -0.144f);
        rightWheel.transform.localRotation = Quaternion.Euler(90, 0, 0);
        rightWheel.transform.localScale = new Vector3(0.066f, 0.018f, 0.066f);

        MeshRenderer rightRenderer = rightWheel.GetComponent<MeshRenderer>();
        rightRenderer.sharedMaterial = wheelMat;

        // Create LiDAR
        GameObject lidar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lidar.name = "lidar";
        lidar.transform.SetParent(robot.transform);
        lidar.transform.localPosition = new Vector3(-0.064f, 0.122f, 0);
        lidar.transform.localRotation = Quaternion.identity;
        lidar.transform.localScale = new Vector3(0.07f, 0.04f, 0.07f);

        MeshRenderer lidarRenderer = lidar.GetComponent<MeshRenderer>();
        Material lidarMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        lidarMat.color = new Color(0.1f, 0.1f, 0.1f); // Black
        lidarRenderer.sharedMaterial = lidarMat;

        // Create direction indicator (forward marker)
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        indicator.name = "direction_indicator";
        indicator.transform.SetParent(robot.transform);
        indicator.transform.localPosition = new Vector3(0.15f, 0.05f, 0);
        indicator.transform.localRotation = Quaternion.identity;
        indicator.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

        MeshRenderer indicatorRenderer = indicator.GetComponent<MeshRenderer>();
        Material indicatorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        indicatorMat.color = Color.yellow;
        indicatorRenderer.sharedMaterial = indicatorMat;

        // Remove all colliders (we'll use a single collider on the root)
        foreach (Collider col in robot.GetComponentsInChildren<Collider>())
        {
            DestroyImmediate(col);
        }

        Debug.Log("=== Simple fallback robot created! ===");
        Debug.Log("The robot should now be visible as a simple gray box with wheels and LiDAR.");

        Selection.activeGameObject = robot;
    }
#endif
}
