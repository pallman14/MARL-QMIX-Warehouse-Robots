using UnityEngine;
using UnityEditor;

/// <summary>
/// Removes RealisticWarehouseAgent from link objects
/// </summary>
public class RemoveRealisticWarehouseAgent
{
    [MenuItem("Tools/Warehouse/Remove RealisticWarehouseAgent from Links")]
    public static void RemoveRealisticAgents()
    {
        if (!EditorUtility.DisplayDialog("Remove RealisticWarehouseAgent",
            "This will remove RealisticWarehouseAgent script from all objects named 'link'.\n\n" +
            "After this, the Behavior Parameters can be deleted too.\n\n" +
            "Continue?",
            "Yes, Remove", "Cancel"))
        {
            return;
        }

        // Find ALL GameObjects in scene
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        int removedCount = 0;

        foreach (GameObject obj in allObjects)
        {
            // Check if this is a "link" object
            if (obj.name == "link")
            {
                // Try to find RealisticWarehouseAgent component
                Component realisticAgent = obj.GetComponent("RealisticWarehouseAgent");

                if (realisticAgent != null)
                {
                    Debug.Log($"Removing RealisticWarehouseAgent from: {obj.name}");
                    Object.DestroyImmediate(realisticAgent, true);
                    removedCount++;
                }

                // Also remove Behavior Parameters now that the dependency is gone
                Unity.MLAgents.Policies.BehaviorParameters behavior = obj.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
                if (behavior != null && behavior.BehaviorName == "My Behavior")
                {
                    Debug.Log($"Removing My Behavior from: {obj.name}");
                    Object.DestroyImmediate(behavior, true);
                }
            }
        }

        // Force scene to be marked dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Removal Complete",
            $"Removed RealisticWarehouseAgent and Behavior Parameters from {removedCount} link objects.\n\n" +
            $"SAVE THE SCENE NOW with Ctrl+S!",
            "OK");

        Debug.Log($"[REMOVE REALISTIC] Removed from {removedCount} link objects");
    }
}
