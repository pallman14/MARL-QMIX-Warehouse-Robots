using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Helper to clear warnings and find errors in Console
/// </summary>
public class ConsoleCleaner : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Console/Clear All Warnings and Show Errors Only")]
    static void ShowErrorsOnly()
    {
        // This will open Console window
        EditorWindow consoleWindow = EditorWindow.GetWindow(System.Type.GetType("UnityEditor.ConsoleWindow,UnityEditor"));

        if (consoleWindow != null)
        {
            // Focus the console
            consoleWindow.Focus();
        }

        Debug.Log("=== CONSOLE FILTER ===");
        Debug.Log("In the Console window:");
        Debug.Log("1. Click the icon in the top-right that looks like three horizontal lines");
        Debug.Log("2. Uncheck 'Warning' to hide warnings");
        Debug.Log("3. Make sure 'Error' is checked");
        Debug.Log("OR: Just look at the top bar - it shows ERROR count separately from warnings");
        Debug.Log("=====================");
    }

    [MenuItem("Tools/Console/Clear Console")]
    static void ClearConsole()
    {
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.Editor));
        var type = assembly.GetType("UnityEditor.LogEntries");
        var method = type.GetMethod("Clear");
        method.Invoke(new object(), null);

        Debug.Log("Console cleared!");
    }
#endif
}
