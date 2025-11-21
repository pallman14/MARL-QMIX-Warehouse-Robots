using UnityEngine;
using UnityEditor;
using Unity.MLAgents.Policies;

/// <summary>
/// Nuclear option - forcefully destroys all "My Behavior" Behavior Parameters
/// </summary>
public class ForceDeleteMyBehavior
{
    [MenuItem("Tools/Warehouse/FORCE DELETE My Behavior Parameters")]
    public static void ForceDelete()
    {
        if (!EditorUtility.DisplayDialog("Force Delete My Behavior",
            "This will forcefully DELETE all Behavior Parameters named 'My Behavior'.\n\n" +
            "This includes disabled ones on child link objects.\n\n" +
            "Continue?",
            "Yes, DELETE", "Cancel"))
        {
            return;
        }

        // Find ALL Behavior Parameters in the scene
        BehaviorParameters[] allBehaviors = Object.FindObjectsOfType<BehaviorParameters>(true); // true = include inactive

        int deletedCount = 0;
        int keptCount = 0;

        foreach (var behavior in allBehaviors)
        {
            // Check the behavior name
            if (behavior.BehaviorName == "My Behavior")
            {
                Debug.Log($"DELETING: {behavior.gameObject.name} - Behavior: {behavior.BehaviorName}");

                // Use DestroyImmediate to forcefully remove it
                Object.DestroyImmediate(behavior, true);
                deletedCount++;
            }
            else
            {
                Debug.Log($"KEEPING: {behavior.gameObject.name} - Behavior: {behavior.BehaviorName}");
                keptCount++;
            }
        }

        // Force scene to be marked dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Force Delete Complete",
            $"Deleted {deletedCount} 'My Behavior' components.\n" +
            $"Kept {keptCount} other Behavior Parameters.\n\n" +
            $"SAVE THE SCENE NOW with Ctrl+S!",
            "OK");

        Debug.Log($"[FORCE DELETE] Deleted: {deletedCount}, Kept: {keptCount}");
    }
}
