using UnityEngine;

/// <summary>
/// Emergency fix for package spawning issues
/// Attach to QMIXWarehouseEnvironment to prevent duplicate spawning
/// </summary>
public class PackageSpawningFix : MonoBehaviour
{
    [Header("Package Control")]
    [Tooltip("Maximum packages that should exist at once")]
    public int maxPackages = 20;

    [Tooltip("Check and cleanup extra packages every N seconds")]
    public float cleanupInterval = 2f;

    private float cleanupTimer = 0f;

    void Start()
    {
        Debug.Log("🔧 PackageSpawningFix: Active - will monitor and cleanup extra packages");

        // Disable any ScenarioShim in the scene immediately
        DisableAllScenarioShims();
    }

    void Update()
    {
        cleanupTimer += Time.deltaTime;

        if (cleanupTimer >= cleanupInterval)
        {
            cleanupTimer = 0f;
            CleanupExtraPackages();
        }
    }

    void DisableAllScenarioShims()
    {
        // Use Unity 6.0+ API (FindObjectsByType instead of deprecated FindObjectsOfType)
        var shims = FindObjectsByType<Unity.Robotics.PerceptionRandomizers.Shims.ScenarioShim>(FindObjectsSortMode.None);
        foreach (var shim in shims)
        {
            if (shim.enabled)
            {
                shim.enabled = false;
                Debug.Log($"✅ Disabled ScenarioShim on {shim.gameObject.name}");
                Debug.Log("   This will prevent ShelfBoxRandomizer and FloorBoxRandomizer from spawning packages");
            }
        }

        if (shims.Length == 0)
        {
            Debug.Log("ℹ️ No ScenarioShim found - package spawning should already be controlled by QMIXWarehouseEnvironment only");
        }
    }

    void CleanupExtraPackages()
    {
        // Use Unity 6.0+ API
        Package[] allPackages = FindObjectsByType<Package>(FindObjectsSortMode.InstanceID);

        if (allPackages.Length <= maxPackages)
        {
            return; // Everything is fine
        }

        Debug.LogWarning($"⚠️ Found {allPackages.Length} packages (max: {maxPackages}). Cleaning up extras...");

        // Sort by creation time (older packages first)
        System.Array.Sort(allPackages, (a, b) =>
            a.GetInstanceID().CompareTo(b.GetInstanceID()));

        // Keep only the first maxPackages, destroy the rest
        for (int i = maxPackages; i < allPackages.Length; i++)
        {
            if (allPackages[i] != null)
            {
                Destroy(allPackages[i].gameObject);
            }
        }

        Debug.Log($"✅ Cleaned up {allPackages.Length - maxPackages} extra packages");
    }

    // Manual cleanup button
    [ContextMenu("Force Cleanup Now")]
    void ForceCleanup()
    {
        CleanupExtraPackages();
        DisableAllScenarioShims();
    }
}
