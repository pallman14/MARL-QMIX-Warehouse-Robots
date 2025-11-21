using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RWARE
{
    /// <summary>
    /// Utility to automatically create RWARE agent and shelf prefabs
    /// Run this in the Unity Editor via: Tools > RWARE > Setup Prefabs
    /// </summary>
    public class RWAREPrefabSetup : MonoBehaviour
    {
#if UNITY_EDITOR
        [MenuItem("Tools/RWARE/Create Agent Prefab")]
        static void CreateAgentPrefab()
        {
            // Create root GameObject
            GameObject agentObj = new GameObject("RWAREAgent");

            // Add RWAREAgent component
            RWAREAgent agent = agentObj.AddComponent<RWAREAgent>();

            // Add ML-Agents components
            Unity.MLAgents.DecisionRequester decisionRequester = agentObj.AddComponent<Unity.MLAgents.DecisionRequester>();
            decisionRequester.DecisionPeriod = 5;

            Unity.MLAgents.Policies.BehaviorParameters behaviorParams = agentObj.AddComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            behaviorParams.BehaviorName = "RWAREAgent";
            behaviorParams.BrainParameters.VectorObservationSize = 0; // Will be calculated automatically
            behaviorParams.BehaviorType = Unity.MLAgents.Policies.BehaviorType.HeuristicOnly; // Start in manual mode

            // Set up discrete actions: 1 branch with 5 actions
            var actionSpec = new Unity.MLAgents.Actuators.ActionSpec(0, new int[] { 5 });
            typeof(Unity.MLAgents.Policies.BehaviorParameters)
                .GetField("m_BrainParameters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(behaviorParams, new Unity.MLAgents.Policies.BrainParameters
                {
                    VectorObservationSize = 0,
                    ActionSpec = actionSpec
                });

            // Add visual components
            RobotVisualizer visualizer = agentObj.AddComponent<RobotVisualizer>();

            // Add camera controller
            RobotCameraController cameraController = agentObj.AddComponent<RobotCameraController>();

            // Add capsule collider
            CapsuleCollider collider = agentObj.AddComponent<CapsuleCollider>();
            collider.height = 1.0f;
            collider.radius = 0.4f;
            collider.center = new Vector3(0, 0.5f, 0);

            // Add rigidbody (kinematic for grid-based movement)
            Rigidbody rb = agentObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Save as prefab
            string prefabPath = "Assets/Prefabs/RWAREAgent.prefab";
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            PrefabUtility.SaveAsPrefabAsset(agentObj, prefabPath);
            Debug.Log($"Created RWARE Agent prefab at {prefabPath}");

            // Clean up scene object
            DestroyImmediate(agentObj);

            // Ping the prefab in project
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        }

        [MenuItem("Tools/RWARE/Create Shelf Prefab")]
        static void CreateShelfPrefab()
        {
            // Create root GameObject
            GameObject shelfObj = new GameObject("Shelf");

            // Add Shelf component
            Shelf shelf = shelfObj.AddComponent<Shelf>();

            // Create visual manually
            shelf.autoCreateVisual = true;
            shelf.CreateVisual();

            // Add box collider (optional, for physics interactions)
            BoxCollider collider = shelfObj.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.8f, 1.0f, 0.6f);
            collider.center = new Vector3(0, 0.5f, 0);

            // Save as prefab
            string prefabPath = "Assets/Prefabs/Shelf.prefab";
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            PrefabUtility.SaveAsPrefabAsset(shelfObj, prefabPath);
            Debug.Log($"Created Shelf prefab at {prefabPath}");

            // Clean up scene object
            DestroyImmediate(shelfObj);

            // Ping the prefab in project
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        }

        [MenuItem("Tools/RWARE/Create Goal Marker Prefab")]
        static void CreateGoalMarkerPrefab()
        {
            // Create goal marker (simple cylinder)
            GameObject goalObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goalObj.name = "GoalMarker";

            // Scale it to be flat (like a target zone)
            goalObj.transform.localScale = new Vector3(0.8f, 0.05f, 0.8f);
            goalObj.transform.localPosition = new Vector3(0, 0.05f, 0);

            // Set color to green
            MeshRenderer renderer = goalObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.0f, 1.0f, 0.0f, 0.5f);
                mat.SetFloat("_Metallic", 0.2f);
                mat.SetFloat("_Glossiness", 0.8f);
                renderer.material = mat;
            }

            // Remove collider
            Collider collider = goalObj.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            // Save as prefab
            string prefabPath = "Assets/Prefabs/GoalMarker.prefab";
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            PrefabUtility.SaveAsPrefabAsset(goalObj, prefabPath);
            Debug.Log($"Created Goal Marker prefab at {prefabPath}");

            // Clean up scene object
            DestroyImmediate(goalObj);

            // Ping the prefab in project
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        }

        [MenuItem("Tools/RWARE/Setup All Prefabs")]
        static void SetupAllPrefabs()
        {
            Debug.Log("Creating all RWARE prefabs...");
            CreateAgentPrefab();
            CreateShelfPrefab();
            CreateGoalMarkerPrefab();
            Debug.Log("All RWARE prefabs created successfully!");
        }

        [MenuItem("Tools/RWARE/Create Test Scene")]
        static void CreateTestScene()
        {
            // Create environment GameObject
            GameObject envObj = new GameObject("RWAREEnvironment");
            RWAREEnvironment env = envObj.AddComponent<RWAREEnvironment>();

            // Configure basic settings
            env.gridWidth = 10;
            env.gridHeight = 10;
            env.cellSize = 1.0f;
            env.maxAgents = 2;
            env.numRequestedShelves = 2;

            // Set up spawn positions
            env.agentSpawnPositions.Add(new Vector2Int(1, 1));
            env.agentSpawnPositions.Add(new Vector2Int(8, 1));

            // Set up shelf positions
            env.shelfPositions.Add(new Vector2Int(3, 5));
            env.shelfPositions.Add(new Vector2Int(6, 5));
            env.shelfPositions.Add(new Vector2Int(3, 7));
            env.shelfPositions.Add(new Vector2Int(6, 7));

            // Set up goal positions
            env.goalPositions.Add(new Vector2Int(1, 9));
            env.goalPositions.Add(new Vector2Int(8, 9));

            // Try to load prefabs
            GameObject agentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RWAREAgent.prefab");
            GameObject shelfPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Shelf.prefab");
            GameObject goalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GoalMarker.prefab");

            if (agentPrefab != null) env.agentPrefab = agentPrefab;
            if (shelfPrefab != null) env.shelfPrefab = shelfPrefab;
            if (goalPrefab != null) env.goalMarkerPrefab = goalPrefab;

            // Add directional light if none exists
            if (FindAnyObjectByType<Light>() == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            // Add ground plane for visual reference
            GameObject groundObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundObj.name = "Ground";
            groundObj.transform.localScale = new Vector3(env.gridWidth / 10f, 1, env.gridHeight / 10f);
            groundObj.transform.position = new Vector3(env.gridWidth / 2f, -0.1f, env.gridHeight / 2f);

            MeshRenderer groundRenderer = groundObj.GetComponent<MeshRenderer>();
            if (groundRenderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.8f, 0.8f, 0.8f);
                groundRenderer.material = mat;
            }

            Debug.Log("RWARE Test Scene created! Configure prefab references in RWAREEnvironment if needed.");
            Debug.Log("Press Play to start the environment. Use WASD to control agents.");

            Selection.activeGameObject = envObj;
        }
#endif
    }
}
