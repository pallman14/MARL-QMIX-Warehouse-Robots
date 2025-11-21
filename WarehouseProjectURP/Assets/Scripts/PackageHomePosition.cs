using UnityEngine;

/// <summary>
/// Marks a package's home position in the warehouse
/// Packages return here after being delivered
/// </summary>
public class PackageHomePosition : MonoBehaviour
{
    [Header("Position Info")]
    [Tooltip("Unique ID for this position")]
    public int positionID;

    [Tooltip("The package currently at this position (null if picked up)")]
    public Package currentPackage;

    [Header("Request State")]
    [Tooltip("Is this package currently requested for delivery?")]
    public bool isRequested = false;

    [Header("Visual Feedback")]
    [Tooltip("Material to use when package is requested (glowing)")]
    public Material requestedMaterial;

    [Tooltip("Particle effect when requested")]
    public GameObject requestedParticlePrefab;

    private Material originalMaterial;
    private GameObject particleEffect;
    private Renderer packageRenderer;

    void Start()
    {
        // Find the package at this position
        currentPackage = GetComponent<Package>();

        if (currentPackage != null)
        {
            // CRITICAL: Set the back-reference so package knows its home position
            currentPackage.homePosition = this;

            packageRenderer = currentPackage.GetComponent<Renderer>();
            if (packageRenderer != null)
            {
                originalMaterial = packageRenderer.material;
            }
        }

        // Register with environment (use Unity 6.0 API)
        var environment = FindAnyObjectByType<QMIXWarehouseEnvironment>();
        if (environment != null)
        {
            environment.RegisterPackagePosition(this);
        }
    }

    /// <summary>
    /// Set this package as requested (needs delivery)
    /// </summary>
    public void SetRequested(bool requested)
    {
        isRequested = requested;

        if (currentPackage == null) return;

        if (requested)
        {
            // Make package glow cyan
            EnableGlow();
        }
        else
        {
            // Return to normal appearance
            DisableGlow();
        }
    }

    void EnableGlow()
    {
        if (packageRenderer == null) return;

        // Enable emission for glowing effect
        Material glowMat = new Material(packageRenderer.material);
        glowMat.EnableKeyword("_EMISSION");
        glowMat.SetColor("_EmissionColor", Color.cyan * 1.5f);
        packageRenderer.material = glowMat;

        // Add particle effect if available
        if (requestedParticlePrefab != null && particleEffect == null)
        {
            particleEffect = Instantiate(requestedParticlePrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            particleEffect.transform.SetParent(transform);
        }
    }

    void DisableGlow()
    {
        if (packageRenderer == null) return;

        // Restore original material
        if (originalMaterial != null)
        {
            packageRenderer.material = originalMaterial;
        }
        else
        {
            // Fallback: just disable emission
            Material mat = packageRenderer.material;
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }

        // Remove particle effect
        if (particleEffect != null)
        {
            Destroy(particleEffect);
            particleEffect = null;
        }
    }

    /// <summary>
    /// Package was picked up by agent
    /// </summary>
    public void OnPackagePickedUp()
    {
        currentPackage = null;
        DisableGlow();
    }

    /// <summary>
    /// Return package to home position after delivery
    /// </summary>
    public void ReturnPackage()
    {
        if (currentPackage == null)
        {
            Debug.LogWarning($"Cannot return package to position {positionID} - no package reference!");
            return;
        }

        // Move package back to home position
        currentPackage.transform.position = transform.position;
        currentPackage.transform.rotation = transform.rotation;
        currentPackage.isPickedUp = false;
        currentPackage.isDelivered = false;

        // Make sure it's visible
        currentPackage.gameObject.SetActive(true);

        DisableGlow();
        isRequested = false;
    }

    /// <summary>
    /// Spawn a new package at this position (if needed at runtime)
    /// </summary>
    public void SpawnPackage(GameObject packagePrefab)
    {
        if (currentPackage != null)
        {
            Debug.LogWarning($"Position {positionID} already has a package!");
            return;
        }

        GameObject packageObj = Instantiate(packagePrefab, transform.position, transform.rotation);
        currentPackage = packageObj.GetComponent<Package>();

        if (currentPackage != null)
        {
            currentPackage.homePosition = this;
            currentPackage.packageID = positionID;
        }

        packageRenderer = packageObj.GetComponent<Renderer>();
        if (packageRenderer != null)
        {
            originalMaterial = packageRenderer.material;
        }
    }

    // Visualize position in Scene view
    void OnDrawGizmos()
    {
        Gizmos.color = isRequested ? Color.cyan : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        if (isRequested)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.8f);

        // Draw position ID
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, $"Position {positionID}");
        #endif
    }
}
