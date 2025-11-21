using UnityEngine;

/// <summary>
/// Simple WASD controller for TurtleBot3
/// Attach this to the turtlebot3_waffle GameObject
/// </summary>
public class SimpleTurtleBotController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Forward/backward speed (m/s)")]
    public float moveSpeed = 2.0f;

    [Tooltip("Rotation speed (degrees/s)")]
    public float rotationSpeed = 90.0f;

    [Header("Camera Settings")]
    [Tooltip("Key to toggle camera view")]
    public KeyCode toggleCameraKey = KeyCode.C;

    [Header("Package Handling")]
    [Tooltip("Key to pickup/drop packages")]
    public KeyCode pickupDropKey = KeyCode.Space;

    [Tooltip("Range to detect packages")]
    public float pickupRange = 1.5f;

    [Tooltip("Currently carried package")]
    public Package carriedPackage = null;

    private Camera robotCamera;
    private Camera mainCamera;
    private bool isRobotCameraActive = false;

    void Start()
    {
        // Find cameras
        mainCamera = Camera.main;
        robotCamera = GetComponentInChildren<Camera>();

        if (mainCamera == null)
        {
            Debug.LogError("NO MAIN CAMERA FOUND! Create one: GameObject → Camera, tag it as MainCamera");
        }
        else
        {
            Debug.Log($"Main camera found: {mainCamera.gameObject.name}");
        }

        if (robotCamera != null)
        {
            robotCamera.enabled = false;

            // Make sure robot camera has audio listener disabled
            AudioListener robotListener = robotCamera.GetComponent<AudioListener>();
            if (robotListener == null)
            {
                robotListener = robotCamera.gameObject.AddComponent<AudioListener>();
            }
            robotListener.enabled = false;

            Debug.Log("Robot camera found and disabled. Press C to toggle.");
        }
        else
        {
            Debug.LogWarning("No camera found on TurtleBot3. Add one via Tools > TurtleBot > Add Camera to Robot");
        }

        Debug.Log("TurtleBot3 Controller Active!");
        Debug.Log("Controls: W=Forward, S=Backward, A=Turn Left, D=Turn Right, C=Toggle Camera");
    }

    void Update()
    {
        HandleMovement();
        HandleCameraToggle();
        HandlePackagePickupDrop();
    }

    void HandleMovement()
    {
        float moveInput = 0f;
        float rotateInput = 0f;

        // Get input
        if (Input.GetKey(KeyCode.W))
        {
            moveInput = 1f;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            moveInput = -1f;
        }

        if (Input.GetKey(KeyCode.A))
        {
            rotateInput = 1f;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            rotateInput = -1f;
        }

        // Apply movement
        if (moveInput != 0f)
        {
            Vector3 movement = transform.forward * moveInput * moveSpeed * Time.deltaTime;
            transform.position += movement;
        }

        // Apply rotation
        if (rotateInput != 0f)
        {
            float rotation = rotateInput * rotationSpeed * Time.deltaTime;
            transform.Rotate(0, rotation, 0);
        }
    }

    void HandleCameraToggle()
    {
        if (Input.GetKeyDown(toggleCameraKey))
        {
            Debug.Log($"C key pressed! mainCamera={mainCamera != null}, robotCamera={robotCamera != null}");

            if (mainCamera == null)
            {
                Debug.LogError("Cannot toggle: Main camera is null!");
                return;
            }

            if (robotCamera == null)
            {
                Debug.LogError("Cannot toggle: Robot camera is null!");
                return;
            }

            isRobotCameraActive = !isRobotCameraActive;

            robotCamera.enabled = isRobotCameraActive;
            mainCamera.enabled = !isRobotCameraActive;

            // Toggle audio listeners
            AudioListener robotListener = robotCamera.GetComponent<AudioListener>();
            AudioListener mainListener = mainCamera.GetComponent<AudioListener>();

            if (robotListener != null && mainListener != null)
            {
                robotListener.enabled = isRobotCameraActive;
                mainListener.enabled = !isRobotCameraActive;
            }

            Debug.Log(isRobotCameraActive ? "✓ Switched to ROBOT camera view (first-person)" : "✓ Switched to MAIN camera view (third-person)");
        }
    }

    void HandlePackagePickupDrop()
    {
        if (Input.GetKeyDown(pickupDropKey))
        {
            if (carriedPackage != null)
            {
                // Drop package
                DropPackage();
            }
            else
            {
                // Try to pick up package
                TryPickupPackage();
            }
        }
    }

    void TryPickupPackage()
    {
        // Find all packages in range
        Package[] allPackages = FindObjectsByType<Package>(FindObjectsSortMode.None);
        Package closestPackage = null;
        float closestDistance = pickupRange;

        foreach (Package pkg in allPackages)
        {
            if (!pkg.isPickedUp && !pkg.isDelivered)
            {
                float distance = Vector3.Distance(transform.position, pkg.transform.position);
                if (distance < closestDistance)
                {
                    closestPackage = pkg;
                    closestDistance = distance;
                }
            }
        }

        if (closestPackage != null)
        {
            carriedPackage = closestPackage;
            carriedPackage.PickUp(transform);
            Debug.Log($"✓ Picked up package {carriedPackage.packageID}");
        }
        else
        {
            Debug.Log("No package in range to pick up");
        }
    }

    void DropPackage()
    {
        if (carriedPackage == null) return;

        // Check if we're in a delivery zone
        DeliveryZone[] zones = FindObjectsByType<DeliveryZone>(FindObjectsSortMode.None);
        DeliveryZone currentZone = null;

        foreach (DeliveryZone zone in zones)
        {
            float distance = Vector3.Distance(transform.position, zone.transform.position);
            if (distance < 2.0f)
            {
                currentZone = zone;
                break;
            }
        }

        Vector3 dropPosition = transform.position + transform.forward * 0.5f;
        carriedPackage.Drop();
        carriedPackage.transform.position = dropPosition;

        if (currentZone != null)
        {
            // Package delivered!
            carriedPackage.MarkAsDelivered();
            currentZone.OnPackageDelivered();
            Debug.Log($"✓ Package {carriedPackage.packageID} delivered to zone {currentZone.zoneID}!");
        }
        else
        {
            Debug.Log($"Dropped package {carriedPackage.packageID}");
        }

        carriedPackage = null;
    }

    void OnGUI()
    {
        // Show controls on screen
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(10, 10, 400, 30), "W/S: Move Forward/Back", style);
        GUI.Label(new Rect(10, 40, 400, 30), "A/D: Turn Left/Right", style);
        GUI.Label(new Rect(10, 70, 400, 30), "SPACE: Pickup/Drop Package", style);
        GUI.Label(new Rect(10, 100, 400, 30), "C: Toggle Camera", style);

        string cameraStatus = isRobotCameraActive ? "Robot View" : "Overview";
        GUI.Label(new Rect(10, 130, 400, 30), $"Camera: {cameraStatus}", style);

        string packageStatus = carriedPackage != null ? $"Carrying Package #{carriedPackage.packageID}" : "No Package";
        GUI.Label(new Rect(10, 160, 400, 30), packageStatus, style);
    }

    void OnDrawGizmos()
    {
        // Draw pickup range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
    }
}
