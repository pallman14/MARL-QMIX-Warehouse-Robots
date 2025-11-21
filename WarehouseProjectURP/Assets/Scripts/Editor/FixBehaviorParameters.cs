using UnityEngine;
using UnityEditor;
using Unity.MLAgents.Policies;
using System.Reflection;

/// <summary>
/// Tool to automatically configure Behavior Parameters correctly for QMIX training
/// </summary>
public class FixBehaviorParameters
{
    [MenuItem("Tools/Warehouse/Fix All Behavior Parameters for QMIX")]
    public static void FixAllBehaviorParameters()
    {
        // Find all objects with QMIXWarehouseAgent
        QMIXWarehouseAgent[] agents = GameObject.FindObjectsOfType<QMIXWarehouseAgent>();

        if (agents.Length == 0)
        {
            EditorUtility.DisplayDialog("No Agents Found",
                "No objects with QMIXWarehouseAgent component found in the scene.",
                "OK");
            return;
        }

        int fixedCount = 0;

        foreach (var agent in agents)
        {
            GameObject obj = agent.gameObject;

            // Get or add Behavior Parameters
            BehaviorParameters behavior = obj.GetComponent<BehaviorParameters>();
            if (behavior == null)
            {
                behavior = obj.AddComponent<BehaviorParameters>();
                Debug.Log($"Added Behavior Parameters to: {obj.name}");
            }

            // Configure basic settings
            behavior.BehaviorName = "QMIXWarehouse";
            behavior.TeamId = 0;
            behavior.BehaviorType = BehaviorType.Default;
            behavior.Model = null;

            // Use SerializedObject to set action spec (works with all ML-Agents versions)
            SerializedObject serializedBehavior = new SerializedObject(behavior);

            // Set vector observation size
            SerializedProperty brainParameters = serializedBehavior.FindProperty("m_BrainParameters");
            if (brainParameters != null)
            {
                SerializedProperty vectorObservationSize = brainParameters.FindPropertyRelative("VectorObservationSize");
                if (vectorObservationSize != null)
                {
                    vectorObservationSize.intValue = 47;
                }

                SerializedProperty numStackedVectorObservations = brainParameters.FindPropertyRelative("NumStackedVectorObservations");
                if (numStackedVectorObservations != null)
                {
                    numStackedVectorObservations.intValue = 1;
                }
            }

            // Set action spec - discrete with 6 actions
            SerializedProperty actionSpec = serializedBehavior.FindProperty("m_ActionSpec");
            if (actionSpec != null)
            {
                // Set number of continuous actions to 0
                SerializedProperty numContinuousActions = actionSpec.FindPropertyRelative("m_NumContinuousActions");
                if (numContinuousActions != null)
                {
                    numContinuousActions.intValue = 0;
                }

                // Set discrete branch sizes
                SerializedProperty branchSizes = actionSpec.FindPropertyRelative("m_BranchSizes");
                if (branchSizes != null)
                {
                    branchSizes.ClearArray();
                    branchSizes.arraySize = 1;
                    branchSizes.GetArrayElementAtIndex(0).intValue = 6;
                }
            }

            serializedBehavior.ApplyModifiedProperties();

            // Mark as dirty
            EditorUtility.SetDirty(obj);
            EditorUtility.SetDirty(behavior);

            fixedCount++;
            Debug.Log($"✓ Fixed Behavior Parameters on: {obj.name}");
            Debug.Log($"  - Behavior Name: QMIXWarehouse");
            Debug.Log($"  - Vector Observation: 47");
            Debug.Log($"  - Actions: Discrete, Branch 0 Size = 6");
        }

        // Save scene
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Behavior Parameters Fixed",
            $"Fixed Behavior Parameters on {fixedCount} TurtleBot agent(s).\n\n" +
            "Configuration:\n" +
            "• Behavior Name: QMIXWarehouse\n" +
            "• Vector Observation: 47\n" +
            "• Actions: Discrete (1 branch, size 6)\n" +
            "• Behavior Type: Default\n\n" +
            "Save the scene with Ctrl+S",
            "OK");

        Debug.Log($"[FIX BEHAVIOR] Fixed {fixedCount} agents");
    }
}
