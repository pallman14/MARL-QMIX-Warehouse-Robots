using UnityEngine;
using UnityEditor;
using Unity.MLAgents.Policies;

/// <summary>
/// Nuclear option - removes ALL Behavior Parameters except from TurtleBot roots
/// </summary>
public class ForceBehaviorCleanup
{
    [MenuItem("Tools/Warehouse/FORCE Remove Invalid Behavior Parameters")]
    public static void ForceCleanup()
    {
        if (!EditorUtility.DisplayDialog("Force Cleanup",
            "This will forcefully remove Behavior Parameters from ALL objects except the 4 TurtleBot root objects.\n\n" +
            "This action cannot be undone (except with Ctrl+Z).\n\n" +
            "Continue?",
            "Yes, Force Clean", "Cancel"))
        {
            return;
        }

        // Get ALL objects in scene
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();

        int removedCount = 0;
        int keptCount = 0;

        foreach (GameObject obj in allObjects)
        {
            BehaviorParameters behavior = obj.GetComponent<BehaviorParameters>();
            if (behavior == null) continue;

            QMIXWarehouseAgent agent = obj.GetComponent<QMIXWarehouseAgent>();

            // Keep ONLY if this object has QMIXWarehouseAgent
            if (agent != null)
            {
                Debug.Log($"✓ KEEPING: {obj.name} (has QMIXWarehouseAgent)");
                keptCount++;
            }
            else
            {
                // Remove it
                Debug.Log($"✗ REMOVING: {obj.name}");
                Undo.DestroyObjectImmediate(behavior);
                removedCount++;
            }
        }

        // Force scene to be marked dirty and save
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        // Force asset database refresh
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Force Cleanup Complete",
            $"Removed Behavior Parameters from {removedCount} objects.\n" +
            $"Kept on {keptCount} TurtleBot root objects.\n\n" +
            $"Scene has been marked dirty - save with Ctrl+S.",
            "OK");

        Debug.Log($"[FORCE CLEANUP] Removed: {removedCount}, Kept: {keptCount}");
    }

    [MenuItem("Tools/Warehouse/Count Behavior Parameters")]
    public static void CountBehaviors()
    {
        BehaviorParameters[] allBehaviors = UnityEngine.Object.FindObjectsOfType<BehaviorParameters>();

        int withAgent = 0;
        int withoutAgent = 0;

        Debug.Log("=== BEHAVIOR PARAMETERS COUNT ===");

        foreach (var behavior in allBehaviors)
        {
            QMIXWarehouseAgent agent = behavior.GetComponent<QMIXWarehouseAgent>();
            if (agent != null)
            {
                Debug.Log($"✓ {behavior.gameObject.name} - HAS QMIXWarehouseAgent");
                withAgent++;
            }
            else
            {
                Debug.Log($"✗ {behavior.gameObject.name} - NO QMIXWarehouseAgent (should be removed)");
                withoutAgent++;
            }
        }

        EditorUtility.DisplayDialog("Behavior Parameters Count",
            $"Total: {allBehaviors.Length}\n\n" +
            $"With QMIXWarehouseAgent (correct): {withAgent}\n" +
            $"Without QMIXWarehouseAgent (wrong): {withoutAgent}\n\n" +
            "You should have exactly 4 with QMIXWarehouseAgent.",
            "OK");

        Debug.Log($"=== TOTAL: {allBehaviors.Length} (Should be 4) ===");
    }
}
