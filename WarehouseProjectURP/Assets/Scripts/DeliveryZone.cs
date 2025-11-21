using UnityEngine;

/// <summary>
/// Zone where packages should be delivered
/// </summary>
public class DeliveryZone : MonoBehaviour
{
    [Header("Zone Settings")]
    public int zoneID = 0;
    public Color zoneColor = Color.green;

    [Header("Stats")]
    public int packagesDelivered = 0;

    private MeshRenderer meshRenderer;

    void Start()
    {
        if (GetComponentInChildren<MeshRenderer>() == null)
        {
            CreateVisual();
        }
        else
        {
            meshRenderer = GetComponentInChildren<MeshRenderer>();
            UpdateVisual();
        }
    }

    void CreateVisual()
    {
        // Create a flat cylinder as the zone marker
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "ZoneMarker";
        marker.transform.SetParent(transform);
        marker.transform.localPosition = new Vector3(0, 0.05f, 0);
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = new Vector3(2.0f, 0.05f, 2.0f);

        meshRenderer = marker.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.5f);
        meshRenderer.sharedMaterial = mat;

        // Remove collider from visual
        Collider col = marker.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Add a box collider as trigger on the root
        BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2.0f, 1.0f, 2.0f);
        trigger.center = new Vector3(0, 0.5f, 0);
    }

    void UpdateVisual()
    {
        if (meshRenderer != null)
        {
            meshRenderer.material.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.5f);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if a robot with a package entered
        SimpleTurtleBotController robot = other.GetComponent<SimpleTurtleBotController>();
        if (robot != null)
        {
            Debug.Log($"Robot entered delivery zone {zoneID}");
        }
    }

    public void OnPackageDelivered()
    {
        packagesDelivered++;
        Debug.Log($"Package delivered to zone {zoneID}! Total: {packagesDelivered}");
    }

    void OnDrawGizmos()
    {
        Gizmos.color = zoneColor;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(2, 1, 2));
    }
}
