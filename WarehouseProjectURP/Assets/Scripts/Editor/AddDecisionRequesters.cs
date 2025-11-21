using UnityEngine;
using UnityEditor;
using Unity.MLAgents;

/// <summary>
/// Adds DecisionRequester to TurtleBots that have QMIXWarehouseAgent
/// </summary>
public class AddDecisionRequesters
{
    [MenuItem("Tools/Warehouse/Add DecisionRequesters to Agents")]
    public static void AddDecisionRequestersToAgents()
    {
        // Find all QMIXWarehouseAgents
        QMIXWarehouseAgent[] agents = Object.FindObjectsByType<QMIXWarehouseAgent>(FindObjectsSortMode.None);

        if (agents.Length == 0)
        {
            EditorUtility.DisplayDialog("No Agents Found",
                "No objects with QMIXWarehouseAgent found in the scene.",
                "OK");
            return;
        }

        int addedCount = 0;

        foreach (var agent in agents)
        {
            GameObject obj = agent.gameObject;

            // Check if it already has DecisionRequester
            DecisionRequester existingRequester = obj.GetComponent<DecisionRequester>();

            if (existingRequester == null)
            {
                // Add DecisionRequester
                DecisionRequester requester = obj.AddComponent<DecisionRequester>();
                requester.DecisionPeriod = 5;  // Request decision every 5 steps
                requester.TakeActionsBetweenDecisions = true;  // Keep taking same action

                Debug.Log($"Added DecisionRequester to: {obj.name}");
                addedCount++;
                EditorUtility.SetDirty(obj);
            }
            else
            {
                Debug.Log($"DecisionRequester already exists on: {obj.name}");
            }
        }

        // Mark scene as dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("DecisionRequesters Added",
            $"Added DecisionRequester to {addedCount} agents.\n" +
            $"Total agents with DecisionRequester: {agents.Length}\n\n" +
            $"SAVE THE SCENE with Ctrl+S!",
            "OK");

        Debug.Log($"[ADD DECISION REQUESTERS] Added to {addedCount} agents");
    }
}
