using UnityEngine;
using Unity.Robotics.PerceptionRandomizers.Shims;

/// <summary>
/// Disables perception randomizers during play mode
/// The randomizers (ShelfBoxRandomizer, FloorBoxRandomizer) are designed for
/// synthetic data generation and spawn thousands of decorative boxes.
/// During QMIX training, we only want the pickupable packages, not shelf decorations.
/// </summary>
public class DisableRandomizersInPlayMode : MonoBehaviour
{
    [Header("Disable Randomizers")]
    [Tooltip("Disable randomizers when entering play mode (recommended for training)")]
    public bool disableOnPlay = true;

    private ScenarioShim scenarioShim;
    private bool wasEnabled = false;

    void Awake()
    {
        scenarioShim = GetComponent<ScenarioShim>();

        if (scenarioShim != null && disableOnPlay && Application.isPlaying)
        {
            wasEnabled = scenarioShim.enabled;
            scenarioShim.enabled = false;
            Debug.Log("✅ Disabled ScenarioShim randomizers for training mode");
            Debug.Log("   (Prevents thousands of decorative boxes from spawning)");
        }
    }

    void OnApplicationQuit()
    {
        // Re-enable for editor
        if (scenarioShim != null && wasEnabled)
        {
            scenarioShim.enabled = true;
        }
    }
}
