using UnityEngine;

namespace RWARE
{
    /// <summary>
    /// Represents a shelf that can be picked up and delivered by agents
    /// Matches RWARE shelf mechanics
    /// </summary>
    public class Shelf : MonoBehaviour
    {
        [Header("Shelf State")]
        public Vector2Int gridPosition;
        public bool isRequested = false;
        public bool isCarried = false;

        [Header("Visual Settings")]
        public Material normalMaterial;
        public Material requestedMaterial;
        public MeshRenderer meshRenderer;

        [Tooltip("Color when shelf is not requested")]
        public Color normalColor = new Color(0.6f, 0.4f, 0.2f); // Brown

        [Tooltip("Color when shelf is requested for delivery")]
        public Color requestedColor = new Color(1.0f, 0.8f, 0.0f); // Gold

        [Tooltip("Auto-create visual if none exists")]
        public bool autoCreateVisual = true;

        private RWAREAgent carriedByAgent;
        private Vector3 originalPosition;

        void Start()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }

            // Auto-create visual representation if none exists
            if (autoCreateVisual && meshRenderer == null)
            {
                CreateVisual();
            }

            originalPosition = transform.position;
            UpdateVisual();
        }

        /// <summary>
        /// Create a simple visual representation for the shelf
        /// </summary>
        public void CreateVisual()
        {
            // Create shelf body (cube)
            GameObject shelfBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelfBody.name = "ShelfBody";
            shelfBody.transform.SetParent(transform);
            shelfBody.transform.localPosition = new Vector3(0, 0.5f, 0);
            shelfBody.transform.localRotation = Quaternion.identity;
            shelfBody.transform.localScale = new Vector3(0.8f, 1.0f, 0.6f);

            meshRenderer = shelfBody.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.material = new Material(Shader.Find("Standard"));
                meshRenderer.material.color = normalColor;
            }

            // Remove collider from visual (shelf should have collider at root if needed)
            Collider bodyCollider = shelfBody.GetComponent<Collider>();
            if (bodyCollider != null)
            {
                Destroy(bodyCollider);
            }

            // Create shelf levels (horizontal bars)
            for (int i = 0; i < 3; i++)
            {
                GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shelf.name = $"ShelfLevel_{i}";
                shelf.transform.SetParent(shelfBody.transform);
                shelf.transform.localPosition = new Vector3(0, -0.4f + i * 0.4f, 0.35f);
                shelf.transform.localRotation = Quaternion.identity;
                shelf.transform.localScale = new Vector3(0.9f, 0.05f, 0.15f);

                MeshRenderer levelRenderer = shelf.GetComponent<MeshRenderer>();
                if (levelRenderer != null)
                {
                    levelRenderer.material = new Material(Shader.Find("Standard"));
                    levelRenderer.material.color = new Color(0.3f, 0.3f, 0.3f);
                }

                Collider levelCollider = shelf.GetComponent<Collider>();
                if (levelCollider != null)
                {
                    Destroy(levelCollider);
                }
            }
        }

        /// <summary>
        /// Mark this shelf as requested (part of active delivery tasks)
        /// </summary>
        public void SetRequested(bool requested)
        {
            isRequested = requested;
            UpdateVisual();
        }

        /// <summary>
        /// Attach shelf to an agent (picked up)
        /// </summary>
        public void AttachToAgent(RWAREAgent agent)
        {
            isCarried = true;
            carriedByAgent = agent;

            // Position shelf above agent
            transform.SetParent(agent.transform);
            transform.localPosition = Vector3.up * 0.5f;
        }

        /// <summary>
        /// Detach shelf from agent (put down)
        /// </summary>
        public void DetachFromAgent()
        {
            isCarried = false;
            carriedByAgent = null;
            transform.SetParent(null);
        }

        /// <summary>
        /// Update visual appearance based on request status
        /// </summary>
        private void UpdateVisual()
        {
            if (meshRenderer != null)
            {
                // Use materials if provided, otherwise use colors
                if (requestedMaterial != null && normalMaterial != null)
                {
                    meshRenderer.material = isRequested ? requestedMaterial : normalMaterial;
                }
                else
                {
                    // Fallback to color-based appearance
                    meshRenderer.material.color = isRequested ? requestedColor : normalColor;
                }
            }
        }

        /// <summary>
        /// Reset shelf to initial state
        /// </summary>
        public void ResetShelf()
        {
            isRequested = false;
            isCarried = false;
            carriedByAgent = null;
            transform.SetParent(null);
            transform.position = originalPosition;
            UpdateVisual();
        }
    }
}
