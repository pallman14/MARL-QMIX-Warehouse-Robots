using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to remove extra Audio Listeners from agents
/// Tools -> Warehouse -> Remove Extra Audio Listeners
/// </summary>
public class AudioListenerFix : EditorWindow
{
    [MenuItem("Tools/Warehouse/Remove Extra Audio Listeners")]
    public static void RemoveExtraAudioListeners()
    {
        // Find all Audio Listeners in the scene
        AudioListener[] allListeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

        if (allListeners.Length <= 1)
        {
            EditorUtility.DisplayDialog("Success", $"Only {allListeners.Length} Audio Listener found. No action needed.", "OK");
            return;
        }

        int removed = 0;

        // Keep the first one (usually on the Main Camera), remove the rest
        for (int i = 1; i < allListeners.Length; i++)
        {
            GameObject obj = allListeners[i].gameObject;
            DestroyImmediate(allListeners[i]);
            Debug.Log($"Removed Audio Listener from {obj.name}");
            removed++;
        }

        // Mark scene as dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        string message = $"Removed {removed} extra Audio Listeners. Kept 1 on {allListeners[0].gameObject.name}";
        Debug.Log($"<color=green>{message}</color>");
        EditorUtility.DisplayDialog("Success", message, "OK");
    }
}
