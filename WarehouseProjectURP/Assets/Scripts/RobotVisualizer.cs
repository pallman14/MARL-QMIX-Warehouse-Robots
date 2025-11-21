using UnityEngine;

namespace RWARE
{
    /// <summary>
    /// Adds visual representation to robot agents
    /// Attaches this to robot GameObject to make it visible
    /// </summary>
    public class RobotVisualizer : MonoBehaviour
    {
        [Header("Visual Settings")]
        [Tooltip("Color of the robot body")]
        public Color robotColor = new Color(0.2f, 0.6f, 1.0f); // Blue

        [Tooltip("Color of the direction indicator")]
        public Color indicatorColor = Color.yellow;

        [Tooltip("Height of the robot body")]
        public float robotHeight = 1.0f;

        [Tooltip("Radius of the robot body")]
        public float robotRadius = 0.4f;

        private GameObject bodyVisual;
        private GameObject directionIndicator;
        private Renderer bodyRenderer;
        private Renderer indicatorRenderer;

        void Start()
        {
            CreateVisuals();
        }

        /// <summary>
        /// Create visual representation for the robot
        /// </summary>
        void CreateVisuals()
        {
            // Create main body (capsule)
            bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyVisual.name = "RobotBody";
            bodyVisual.transform.SetParent(transform);
            bodyVisual.transform.localPosition = new Vector3(0, robotHeight / 2, 0);
            bodyVisual.transform.localRotation = Quaternion.identity;
            bodyVisual.transform.localScale = new Vector3(robotRadius * 2, robotHeight / 2, robotRadius * 2);

            // Set body color
            bodyRenderer = bodyVisual.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.material = new Material(Shader.Find("Standard"));
                bodyRenderer.material.color = robotColor;
            }

            // Remove collider from visual (agent should have its own collider at root)
            Collider bodyCollider = bodyVisual.GetComponent<Collider>();
            if (bodyCollider != null)
            {
                Destroy(bodyCollider);
            }

            // Create direction indicator (arrow-like cube)
            directionIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            directionIndicator.name = "DirectionIndicator";
            directionIndicator.transform.SetParent(transform);
            directionIndicator.transform.localPosition = new Vector3(0, robotHeight / 2, robotRadius * 0.8f);
            directionIndicator.transform.localRotation = Quaternion.identity;
            directionIndicator.transform.localScale = new Vector3(0.2f, robotHeight * 0.3f, 0.4f);

            // Set indicator color
            indicatorRenderer = directionIndicator.GetComponent<Renderer>();
            if (indicatorRenderer != null)
            {
                indicatorRenderer.material = new Material(Shader.Find("Standard"));
                indicatorRenderer.material.color = indicatorColor;
                indicatorRenderer.material.SetFloat("_Metallic", 0.5f);
            }

            // Remove collider from indicator
            Collider indicatorCollider = directionIndicator.GetComponent<Collider>();
            if (indicatorCollider != null)
            {
                Destroy(indicatorCollider);
            }
        }

        /// <summary>
        /// Update robot color (useful for team identification or state changes)
        /// </summary>
        public void SetRobotColor(Color color)
        {
            robotColor = color;
            if (bodyRenderer != null)
            {
                bodyRenderer.material.color = robotColor;
            }
        }

        /// <summary>
        /// Highlight robot when carrying a shelf
        /// </summary>
        public void SetCarryingState(bool isCarrying)
        {
            if (indicatorRenderer != null)
            {
                indicatorRenderer.material.color = isCarrying ? Color.green : indicatorColor;
            }
        }
    }
}
