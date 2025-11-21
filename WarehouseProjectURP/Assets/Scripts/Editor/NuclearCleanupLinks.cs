using UnityEngine;
using UnityEditor;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

/// <summary>
/// Nuclear option - removes ALL ML-Agents components from link objects
/// </summary>
public class NuclearCleanupLinks
{
    [MenuItem("Tools/Warehouse/NUCLEAR - Clean All ML-Agents from Links")]
    public static void NuclearClean()
    {
        if (!EditorUtility.DisplayDialog("NUCLEAR Cleanup",
            "This will remove ALL ML-Agents components from objects named 'link':\n" +
            "- DecisionRequester\n" +
            "- RealisticWarehouseAgent\n" +
            "- BehaviorParameters\n\n" +
            "This is irreversible (except with Ctrl+Z).\n\n" +
            "Continue?",
            "Yes, NUKE IT", "Cancel"))
        {
            return;
        }

        // Find ALL GameObjects in scene
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        int cleanedCount = 0;

        foreach (GameObject obj in allObjects)
        {
            // Check if this is a "link" object
            if (obj.name == "link")
            {
                Debug.Log($"Cleaning link object: {obj.name} (parent: {obj.transform.parent?.name})");

                // Remove DecisionRequester first (top of dependency chain)
                DecisionRequester decisionRequester = obj.GetComponent<DecisionRequester>();
                if (decisionRequester != null)
                {
                    Debug.Log($"  - Removing DecisionRequester");
                    Object.DestroyImmediate(decisionRequester, true);
                }

                // Remove RealisticWarehouseAgent second
                Component realisticAgent = obj.GetComponent("RealisticWarehouseAgent");
                if (realisticAgent != null)
                {
                    Debug.Log($"  - Removing RealisticWarehouseAgent");
                    Object.DestroyImmediate(realisticAgent, true);
                }

                // Remove BehaviorParameters last (bottom of dependency chain)
                BehaviorParameters behavior = obj.GetComponent<BehaviorParameters>();
                if (behavior != null)
                {
                    Debug.Log($"  - Removing BehaviorParameters (was: {behavior.BehaviorName})");
                    Object.DestroyImmediate(behavior, true);
                }

                cleanedCount++;
            }
        }

        // Force scene to be marked dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        // Count remaining Behavior Parameters
        BehaviorParameters[] remaining = Object.FindObjectsByType<BehaviorParameters>(FindObjectsSortMode.None);
        int qmixCount = 0;
        int otherCount = 0;

        foreach (var bp in remaining)
        {
            if (bp.BehaviorName == "QMIXWarehouse")
                qmixCount++;
            else
                otherCount++;
        }

        EditorUtility.DisplayDialog("Nuclear Cleanup Complete",
            $"Cleaned {cleanedCount} link objects.\n\n" +
            $"Remaining Behavior Parameters:\n" +
            $"- QMIXWarehouse: {qmixCount} (should be 4)\n" +
            $"- Other: {otherCount} (should be 0)\n\n" +
            $"SAVE THE SCENE NOW with Ctrl+S!",
            "OK");

        Debug.Log($"[NUCLEAR CLEANUP] Cleaned {cleanedCount} link objects. Remaining: {qmixCount} QMIXWarehouse, {otherCount} other");
    }
}
