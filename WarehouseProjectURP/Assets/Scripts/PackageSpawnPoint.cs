using UnityEngine;

/// <summary>
/// Marks a position where packages can spawn
/// Place empty GameObjects at ground positions where you want packages to appear
/// </summary>
public class PackageSpawnPoint : MonoBehaviour
{
    [Header("Visualization")]
    [Tooltip("Show gizmo in Scene view")]
    public bool showGizmo = true;

    [Tooltip("Gizmo color")]
    public Color gizmoColor = new Color(0f, 1f, 0f, 0.5f); // Green

    [Tooltip("Gizmo size")]
    public float gizmoSize = 0.5f;

    void OnDrawGizmos()
    {
        if (!showGizmo) return;

        // Draw a wire cube to show spawn point location
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(transform.position, Vector3.one * gizmoSize);

        // Draw a small sphere at the exact spawn point
        Gizmos.DrawSphere(transform.position, gizmoSize * 0.2f);

        // Draw an upward arrow
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * gizmoSize);
    }

    void OnDrawGizmosSelected()
    {
        // When selected, draw a more prominent gizmo
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, Vector3.one * gizmoSize * 1.5f);
    }
}
