using UnityEngine;
using UnityEditor;
using Unity.MLAgents.Policies;
using System.Collections.Generic;

/// <summary>
/// Tool to find and clean up unwanted Behavior Parameters components
/// </summary>
public class CleanupBehaviorParameters : EditorWindow
{
    private Vector2 scrollPosition;
    private List<GameObject> objectsWithBehavior = new List<GameObject>();

    [MenuItem("Tools/Warehouse/Cleanup Behavior Parameters")]
    public static void ShowWindow()
    {
        GetWindow<CleanupBehaviorParameters>("Cleanup Behavior Parameters");
    }

    void OnEnable()
    {
        RefreshList();
    }

    void OnGUI()
    {
        GUILayout.Label("Behavior Parameters Cleanup Tool", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Refresh List", GUILayout.Height(30)))
        {
            RefreshList();
        }

        GUILayout.Space(10);
        GUILayout.Label($"Found {objectsWithBehavior.Count} objects with Behavior Parameters:", EditorStyles.boldLabel);

        if (objectsWithBehavior.Count == 0)
        {
            GUILayout.Label("No objects found with Behavior Parameters.");
            return;
        }

        GUILayout.Space(10);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        foreach (GameObject obj in objectsWithBehavior)
        {
            if (obj == null) continue;

            GUILayout.BeginHorizontal("box");

            // Object name
            GUILayout.Label(obj.name, GUILayout.Width(200));

            // Select button
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                Selection.activeGameObject = obj;
                EditorGUIUtility.PingObject(obj);
            }

            // Check if it's a TurtleBot
            bool isTurtleBot = obj.name.ToLower().Contains("turtle") ||
                               obj.GetComponent<QMIXWarehouseAgent>() != null;

            if (isTurtleBot)
            {
                GUILayout.Label("✓ TurtleBot", EditorStyles.helpBox, GUILayout.Width(100));
            }
            else
            {
                GUILayout.Label("❌ Not TurtleBot", EditorStyles.helpBox, GUILayout.Width(100));

                if (GUILayout.Button("Remove Behavior Parameters", GUILayout.Width(180)))
                {
                    RemoveBehaviorParameters(obj);
                    RefreshList();
                }
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        GUILayout.Space(20);

        if (GUILayout.Button("Remove Behavior Parameters from ALL Non-TurtleBots", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Confirm Cleanup",
                "This will remove Behavior Parameters from all objects that don't have QMIXWarehouseAgent.\n\n" +
                "Your 4 TurtleBots will keep their Behavior Parameters.\n\n" +
                "Continue?",
                "Yes, Clean Up", "Cancel"))
            {
                CleanupNonTurtleBots();
            }
        }
    }

    void RefreshList()
    {
        objectsWithBehavior.Clear();
        BehaviorParameters[] allBehaviors = FindObjectsOfType<BehaviorParameters>();

        foreach (var behavior in allBehaviors)
        {
            objectsWithBehavior.Add(behavior.gameObject);
        }

        Debug.Log($"Found {objectsWithBehavior.Count} objects with Behavior Parameters");
    }

    void RemoveBehaviorParameters(GameObject obj)
    {
        BehaviorParameters behavior = obj.GetComponent<BehaviorParameters>();
        if (behavior != null)
        {
            DestroyImmediate(behavior);
            EditorUtility.SetDirty(obj);
            Debug.Log($"Removed Behavior Parameters from: {obj.name}");
        }
    }

    void CleanupNonTurtleBots()
    {
        int removedCount = 0;

        BehaviorParameters[] allBehaviors = FindObjectsOfType<BehaviorParameters>();

        foreach (var behavior in allBehaviors)
        {
            GameObject obj = behavior.gameObject;

            // Keep ONLY if this exact object has QMIXWarehouseAgent (actual TurtleBot root)
            if (obj.GetComponent<QMIXWarehouseAgent>() != null)
            {
                Debug.Log($"Keeping Behavior Parameters on TurtleBot root: {obj.name}");
                continue;
            }

            // Remove from everything else (including child links)
            DestroyImmediate(behavior);
            EditorUtility.SetDirty(obj);
            removedCount++;
            Debug.Log($"Removed Behavior Parameters from: {obj.name}");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Cleanup Complete",
            $"Removed Behavior Parameters from {removedCount} objects.\n\n" +
            "Only objects with QMIXWarehouseAgent component kept their Behavior Parameters.",
            "OK");

        RefreshList();
    }
}
