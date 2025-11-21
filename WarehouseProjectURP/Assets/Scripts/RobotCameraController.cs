using UnityEngine;

namespace RWARE
{
    /// <summary>
    /// Manages first-person and third-person camera views for the robot
    /// Attach to robot GameObject alongside RWAREAgent
    /// </summary>
    public class RobotCameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        [Tooltip("The first-person camera (robot's view)")]
        public Camera firstPersonCamera;

        [Tooltip("Height offset for first-person camera")]
        public float fpCameraHeight = 0.8f;

        [Tooltip("Forward offset for first-person camera")]
        public float fpCameraForward = 0.2f;

        [Header("Input Settings")]
        [Tooltip("Key to toggle between FP and main camera")]
        public KeyCode toggleCameraKey = KeyCode.C;

        private bool isFirstPersonActive = false;
        private Camera mainCamera;

        void Start()
        {
            SetupFirstPersonCamera();
            mainCamera = Camera.main;

            // Start with first-person camera disabled
            if (firstPersonCamera != null)
            {
                firstPersonCamera.enabled = false;
                AudioListener fpListener = firstPersonCamera.GetComponent<AudioListener>();
                if (fpListener != null)
                {
                    fpListener.enabled = false;
                }
            }
        }

        void Update()
        {
            // Toggle camera view
            if (Input.GetKeyDown(toggleCameraKey))
            {
                ToggleCameraView();
            }
        }

        /// <summary>
        /// Create and configure the first-person camera
        /// </summary>
        void SetupFirstPersonCamera()
        {
            // Check if camera already exists
            if (firstPersonCamera == null)
            {
                // Create camera GameObject
                GameObject fpCameraObj = new GameObject("RobotFPCamera");
                fpCameraObj.transform.SetParent(transform);
                fpCameraObj.transform.localPosition = new Vector3(0, fpCameraHeight, fpCameraForward);
                fpCameraObj.transform.localRotation = Quaternion.identity;

                // Add camera component
                firstPersonCamera = fpCameraObj.AddComponent<Camera>();
                firstPersonCamera.fieldOfView = 80f; // Wider FOV for better awareness
                firstPersonCamera.nearClipPlane = 0.1f;
                firstPersonCamera.farClipPlane = 50f;

                // Don't add audio listener if main camera has one
                // (Only one audio listener should be active at a time)
            }
            else
            {
                // Position existing camera
                firstPersonCamera.transform.localPosition = new Vector3(0, fpCameraHeight, fpCameraForward);
            }
        }

        /// <summary>
        /// Toggle between first-person and main camera view
        /// </summary>
        public void ToggleCameraView()
        {
            isFirstPersonActive = !isFirstPersonActive;

            if (firstPersonCamera != null && mainCamera != null)
            {
                firstPersonCamera.enabled = isFirstPersonActive;
                mainCamera.enabled = !isFirstPersonActive;

                // Toggle audio listeners
                AudioListener fpListener = firstPersonCamera.GetComponent<AudioListener>();
                AudioListener mainListener = mainCamera.GetComponent<AudioListener>();

                if (fpListener != null && mainListener != null)
                {
                    fpListener.enabled = isFirstPersonActive;
                    mainListener.enabled = !isFirstPersonActive;
                }

                Debug.Log($"Camera view: {(isFirstPersonActive ? "First-Person (Robot View)" : "Third-Person (Overview)")}");
            }
        }

        /// <summary>
        /// Switch to first-person view
        /// </summary>
        public void EnableFirstPersonView()
        {
            if (!isFirstPersonActive)
            {
                ToggleCameraView();
            }
        }

        /// <summary>
        /// Switch to main camera view
        /// </summary>
        public void EnableMainCameraView()
        {
            if (isFirstPersonActive)
            {
                ToggleCameraView();
            }
        }
    }
}
