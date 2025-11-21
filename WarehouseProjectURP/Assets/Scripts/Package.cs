using UnityEngine;

/// <summary>
/// Package that can be picked up and delivered by robots
/// </summary>
public class Package : MonoBehaviour
{
    [Header("Package State")]
    public bool isPickedUp = false;
    public bool isDelivered = false;
    public int packageID;

    [Header("Home Position")]
    [Tooltip("The position this package returns to after delivery")]
    public PackageHomePosition homePosition;

    [Header("Visual Settings")]
    public Color normalColor = new Color(0.8f, 0.6f, 0.4f); // Tan/cardboard
    public Color pickedUpColor = new Color(0.6f, 0.6f, 0.6f); // Gray
    public Color deliveredColor = new Color(0.2f, 0.8f, 0.2f); // Green

    private MeshRenderer meshRenderer;
    private Transform carriedByRobot;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    void Start()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();

        // CRITICAL: Store the exact starting position and rotation
        // This is the position we'll return to after delivery
        originalPosition = transform.position;
        originalRotation = transform.rotation;

        if (meshRenderer == null)
        {
            CreateVisual();
        }

        UpdateVisual();
    }

    void CreateVisual()
    {
        // Create a box
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "PackageVisual";
        box.transform.SetParent(transform);
        box.transform.localPosition = new Vector3(0, 0.25f, 0);
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);

        meshRenderer = box.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = normalColor;
        meshRenderer.sharedMaterial = mat;

        // Remove collider from visual
        Collider col = box.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    public void PickUp(Transform robot)
    {
        isPickedUp = true;
        carriedByRobot = robot;
        transform.SetParent(robot);
        transform.localPosition = new Vector3(0, 0.5f, 0);
        UpdateVisual();
    }

    public void Drop()
    {
        isPickedUp = false;
        carriedByRobot = null;
        transform.SetParent(null);
        UpdateVisual();
    }

    public void MarkAsDelivered()
    {
        isDelivered = true;
        UpdateVisual();
    }

    /// <summary>
    /// Return package to its home position after delivery
    /// </summary>
    public void ReturnHome()
    {
        // DEBUG: Log BEFORE returning
        Debug.Log($"[BEFORE RETURN] Package {packageID} - Current pos: {transform.position}, Original pos: {originalPosition}, Active: {gameObject.activeSelf}, Parent: {transform.parent?.name ?? "null"}");

        // Reset state
        isDelivered = false;
        isPickedUp = false;
        carriedByRobot = null;

        // CRITICAL FIX: Use stored original position instead of homePosition.transform
        // This prevents packages from moving due to parent transform changes
        transform.position = originalPosition;
        transform.rotation = originalRotation;

        // Detach from any parent (important for physics)
        transform.SetParent(null);

        // CRITICAL: Stop all physics motion
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep(); // Force rigidbody to stop moving
        }

        // Make sure it's visible
        gameObject.SetActive(true);

        // Update home position reference if it exists
        if (homePosition != null)
        {
            homePosition.currentPackage = this;
        }

        UpdateVisual();

        // DEBUG: Log AFTER returning
        Debug.Log($"[AFTER RETURN] Package {packageID} - New pos: {transform.position}, Active: {gameObject.activeSelf}, Parent: {transform.parent?.name ?? "null"}, Renderer enabled: {meshRenderer?.enabled ?? false}");
    }

    void UpdateVisual()
    {
        if (meshRenderer == null) return;

        if (isDelivered)
        {
            meshRenderer.material.color = deliveredColor;
        }
        else if (isPickedUp)
        {
            meshRenderer.material.color = pickedUpColor;
        }
        else
        {
            meshRenderer.material.color = normalColor;
        }
    }

    void OnDrawGizmos()
    {
        if (!isPickedUp && !isDelivered)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
