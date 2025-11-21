using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to quickly create delivery zones
/// </summary>
public class DeliveryZoneSetupTool : EditorWindow
{
    private int numberOfZones = 2;
    private float zoneRadius = 2.0f;
    private Color zoneColor = new Color(0.2f, 0.8f, 0.2f, 0.5f); // Green

    [MenuItem("Tools/Warehouse/Setup Delivery Zones")]
    public static void ShowWindow()
    {
        GetWindow<DeliveryZoneSetupTool>("Delivery Zone Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("Delivery Zone Setup Tool", EditorStyles.boldLabel);
        GUILayout.Space(10);

        numberOfZones = EditorGUILayout.IntSlider("Number of Zones:", numberOfZones, 1, 5);
        zoneRadius = EditorGUILayout.Slider("Zone Radius:", zoneRadius, 1.0f, 5.0f);
        zoneColor = EditorGUILayout.ColorField("Zone Color:", zoneColor);

        GUILayout.Space(20);

        if (GUILayout.Button("Create Delivery Zones", GUILayout.Height(40)))
        {
            CreateDeliveryZones();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Remove All Delivery Zones", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Confirm Removal",
                "Are you sure you want to remove all delivery zones?",
                "Yes", "Cancel"))
            {
                RemoveDeliveryZones();
            }
        }

        GUILayout.Space(20);
        GUILayout.Label("Current Scene Status:", EditorStyles.boldLabel);

        int currentZones = FindObjectsOfType<DeliveryZone>().Length;
        GUILayout.Label($"Delivery Zones in Scene: {currentZones}");

        GUILayout.Space(10);
        GUILayout.Label("Suggested Positions:", EditorStyles.helpBox);
        GUILayout.Label("Zone 1: (10, 0, 10) - Top Right");
        GUILayout.Label("Zone 2: (-10, 0, 10) - Top Left");
        GUILayout.Label("Zone 3: (10, 0, -10) - Bottom Right");
        GUILayout.Label("Zone 4: (-10, 0, -10) - Bottom Left");
    }

    void CreateDeliveryZones()
    {
        // Remove existing zones first
        DeliveryZone[] existing = FindObjectsOfType<DeliveryZone>();
        foreach (var zone in existing)
        {
            DestroyImmediate(zone.gameObject);
        }

        // Positions for zones (corners of warehouse)
        Vector3[] positions = new Vector3[]
        {
            new Vector3(10, 0.05f, 10),   // Top right
            new Vector3(-10, 0.05f, 10),  // Top left
            new Vector3(10, 0.05f, -10),  // Bottom right
            new Vector3(-10, 0.05f, -10), // Bottom left
            new Vector3(0, 0.05f, 12)     // Center top
        };

        // Create zones
        for (int i = 0; i < numberOfZones && i < positions.Length; i++)
        {
            GameObject zoneObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            zoneObj.name = $"DeliveryZone_{i}";
            zoneObj.transform.position = positions[i];
            zoneObj.transform.localScale = new Vector3(zoneRadius, 0.1f, zoneRadius);

            // Add DeliveryZone component
            DeliveryZone zone = zoneObj.AddComponent<DeliveryZone>();
            zone.zoneID = i;

            // Create material with color
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = zoneColor;
            mat.SetFloat("_Metallic", 0.2f);
            mat.SetFloat("_Glossiness", 0.3f);
            zoneObj.GetComponent<Renderer>().material = mat;

            // Make it a trigger
            Collider collider = zoneObj.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            Debug.Log($"Created DeliveryZone_{i} at {positions[i]}");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Delivery Zones Created",
            $"Created {numberOfZones} delivery zones at warehouse corners.\n\n" +
            "You can move them to your preferred locations in the Scene view.",
            "OK");
    }

    void RemoveDeliveryZones()
    {
        DeliveryZone[] zones = FindObjectsOfType<DeliveryZone>();
        int count = 0;

        foreach (var zone in zones)
        {
            DestroyImmediate(zone.gameObject);
            count++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Zones Removed",
            $"Removed {count} delivery zones.",
            "OK");

        Debug.Log($"Removed {count} delivery zones");
    }
}
