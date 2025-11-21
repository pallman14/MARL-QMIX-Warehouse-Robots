using UnityEngine;

/// <summary>
/// Third-person camera that follows the robot
/// Attach this to Main Camera
/// </summary>
public class FollowCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The robot to follow")]
    public Transform target;

    [Header("Camera Settings")]
    [Tooltip("Distance behind the robot")]
    public float distance = 5.0f;

    [Tooltip("Height above the robot")]
    public float height = 3.0f;

    [Tooltip("How smoothly camera follows")]
    public float smoothSpeed = 5.0f;

    [Tooltip("Look at offset (to look slightly ahead)")]
    public Vector3 lookAtOffset = new Vector3(0, 1, 0);

    void Start()
    {
        // Auto-find robot if not assigned
        if (target == null)
        {
            GameObject robot = GameObject.Find("turtlebot3_waffle");
            if (robot != null)
            {
                target = robot.transform;
                Debug.Log("FollowCamera: Auto-found robot target");
            }
            else
            {
                Debug.LogWarning("FollowCamera: No target assigned and couldn't find turtlebot3_waffle");
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Calculate desired position
        Vector3 desiredPosition = target.position - target.forward * distance + Vector3.up * height;

        // Smoothly move to position
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;

        // Look at target with offset
        Vector3 lookAtPosition = target.position + lookAtOffset;
        transform.LookAt(lookAtPosition);
    }

    void OnDrawGizmos()
    {
        if (target != null)
        {
            // Draw line from camera to target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}
