using UnityEngine;

/// <summary>
/// Sets Unity time scale for faster training
/// Attach this to any GameObject in the scene
/// </summary>
public class TrainingTimeScale : MonoBehaviour
{
    [Header("Training Settings")]
    [Tooltip("Time scale multiplier (1 = normal speed, 20 = 20x faster)")]
    public float timeScale = 20f;

    void Start()
    {
        // Set time scale when entering Play mode
        Time.timeScale = timeScale;
        Debug.Log($"Training time scale set to: {timeScale}x");
    }

    void OnApplicationQuit()
    {
        // Reset to normal speed when exiting
        Time.timeScale = 1f;
    }
}
