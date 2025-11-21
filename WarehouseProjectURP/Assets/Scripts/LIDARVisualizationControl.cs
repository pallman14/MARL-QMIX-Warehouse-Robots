using UnityEngine;
using WarehouseRobotics;

/// <summary>
/// Enhanced LIDAR visualization for presentations
/// Attach to any agent to enable beautiful LIDAR visualization
/// </summary>
public class LIDARVisualizationControl : MonoBehaviour
{
    [Header("Visualization Settings")]
    [Tooltip("Show LIDAR rays in Scene view")]
    public bool showRays = true;

    [Tooltip("Show LIDAR rays in Game view (rendered lines)")]
    public bool showRenderedRays = true;

    [Tooltip("Show detection info on screen")]
    public bool showDebugUI = true;

    [Header("Visual Style")]
    public Color hitColor = new Color(1f, 0.2f, 0.2f, 0.8f); // Red
    public Color missColor = new Color(0.3f, 0.3f, 0.3f, 0.3f); // Gray
    public Color clearPathColor = new Color(0.2f, 1f, 0.2f, 0.8f); // Green
    public float rayThickness = 0.02f;

    [Header("UI Settings")]
    public int fontSize = 16;
    public bool showDistanceReadout = true;
    public bool showMiniMap = false;

    private LIDARSensor lidarSensor;
    private LineRenderer[] rayRenderers;

    void Start()
    {
        lidarSensor = GetComponent<LIDARSensor>();

        if (lidarSensor == null)
        {
            Debug.LogWarning($"LIDARVisualizationControl on {gameObject.name}: No LIDARSensor component found!");
            return;
        }

        // Enable visualization on the LIDAR sensor
        lidarSensor.visualizeRays = showRays;
        lidarSensor.hitRayColor = hitColor;
        lidarSensor.missRayColor = missColor;

        // Create rendered rays for Game view
        if (showRenderedRays)
        {
            CreateRenderedRays();
        }
    }

    void CreateRenderedRays()
    {
        // Create LineRenderers for visible rays in Game view
        int numVisibleRays = lidarSensor.mlAgentsRays; // Show downsampled rays
        rayRenderers = new LineRenderer[numVisibleRays];

        for (int i = 0; i < numVisibleRays; i++)
        {
            GameObject rayObj = new GameObject($"LIDAR_Ray_{i}");
            rayObj.transform.SetParent(transform);

            LineRenderer lr = rayObj.AddComponent<LineRenderer>();
            lr.startWidth = rayThickness;
            lr.endWidth = rayThickness;
            lr.positionCount = 2;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = 1000; // Render on top

            rayRenderers[i] = lr;
        }
    }

    void Update()
    {
        if (lidarSensor == null) return;

        // Update rendered rays
        if (showRenderedRays && rayRenderers != null)
        {
            UpdateRenderedRays();
        }
    }

    void UpdateRenderedRays()
    {
        Vector3 origin = transform.position + Vector3.up * lidarSensor.heightOffset;
        float[] distances = lidarSensor.GetMLAgentsDistances();
        float angleStep = 360f / lidarSensor.mlAgentsRays;

        for (int i = 0; i < rayRenderers.Length; i++)
        {
            float angle = i * angleStep;
            float angleRad = angle * Mathf.Deg2Rad;

            Vector3 direction = new Vector3(
                Mathf.Cos(angleRad),
                0,
                Mathf.Sin(angleRad)
            );
            direction = transform.rotation * direction;

            float distance = distances[i];
            Vector3 endPoint = origin + direction * distance;

            // Color based on detection
            bool isHit = distance < lidarSensor.maxRange;
            Color rayColor = isHit ? hitColor : missColor;

            // Special color for clear forward path
            if (angle > 330 || angle < 30) // Forward cone
            {
                if (distance > 1.0f)
                {
                    rayColor = clearPathColor;
                }
            }

            rayRenderers[i].startColor = rayColor;
            rayRenderers[i].endColor = new Color(rayColor.r, rayColor.g, rayColor.b, 0f);
            rayRenderers[i].SetPosition(0, origin);
            rayRenderers[i].SetPosition(1, endPoint);
        }
    }

    void OnGUI()
    {
        if (!showDebugUI || lidarSensor == null) return;

        // Set up GUI style
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = fontSize;
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontStyle = FontStyle.Bold;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.7f));

        // Main info panel
        GUILayout.BeginArea(new Rect(10, 10, 400, 300), boxStyle);

        GUILayout.Label($"🤖 Agent: {gameObject.name}", labelStyle);
        GUILayout.Space(10);

        GUILayout.Label($"LIDAR Sensor Status", labelStyle);
        GUILayout.Label($"• Min Distance: {lidarSensor.GetMinimumDistance():F2}m", labelStyle);
        GUILayout.Label($"• Path Clear: {(lidarSensor.IsPathClear() ? "✓ YES" : "✗ NO")}", labelStyle);
        GUILayout.Label($"• Rays: {lidarSensor.mlAgentsRays} (downsampled from {lidarSensor.numRays})", labelStyle);
        GUILayout.Label($"• Range: {lidarSensor.minRange:F2}m - {lidarSensor.maxRange:F2}m", labelStyle);

        if (showDistanceReadout)
        {
            GUILayout.Space(10);
            GUILayout.Label("Sample Readings:", labelStyle);

            float[] distances = lidarSensor.GetMLAgentsDistances();

            // Show key directions
            int[] keyAngles = { 0, 90, 180, 270 }; // Front, Right, Back, Left
            string[] directions = { "Front", "Right", "Back", "Left" };

            for (int i = 0; i < keyAngles.Length; i++)
            {
                int rayIndex = (int)(keyAngles[i] / (360f / lidarSensor.mlAgentsRays));
                if (rayIndex < distances.Length)
                {
                    float dist = distances[rayIndex];
                    string status = dist < 0.5f ? "⚠️" : dist < 1.5f ? "⚡" : "✓";
                    GUILayout.Label($"  {status} {directions[i]}: {dist:F2}m", labelStyle);
                }
            }
        }

        GUILayout.EndArea();

        // Mini radar display (optional)
        if (showMiniMap)
        {
            DrawMiniRadar();
        }
    }

    void DrawMiniRadar()
    {
        float radarSize = 150f;
        float radarX = Screen.width - radarSize - 20;
        float radarY = 20;

        // Background
        GUI.Box(new Rect(radarX - 10, radarY - 10, radarSize + 20, radarSize + 20), "");

        // Draw radar circle
        DrawCircle(new Vector2(radarX + radarSize / 2, radarY + radarSize / 2), radarSize / 2);

        // Draw LIDAR readings on radar
        float[] distances = lidarSensor.GetMLAgentsDistances();
        Vector2 center = new Vector2(radarX + radarSize / 2, radarY + radarSize / 2);

        for (int i = 0; i < distances.Length; i++)
        {
            float angle = i * (360f / distances.Length);
            float normalizedDist = distances[i] / lidarSensor.maxRange;
            float radius = normalizedDist * (radarSize / 2);

            Vector2 point = center + new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );

            Color dotColor = distances[i] < lidarSensor.maxRange ? Color.red : Color.gray;
            DrawDot(point, dotColor);
        }

        // Draw agent (center)
        DrawDot(center, Color.green, 4f);
    }

    void DrawCircle(Vector2 center, float radius, int segments = 36)
    {
        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * angleStep * Mathf.Deg2Rad;
            float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

            Vector2 p1 = center + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * radius;
            Vector2 p2 = center + new Vector2(Mathf.Cos(angle2), Mathf.Sin(angle2)) * radius;

            DrawLine(p1, p2, Color.white);
        }
    }

    void DrawLine(Vector2 start, Vector2 end, Color color)
    {
        // Simple line drawing using GUI (not perfect but works for radar)
        GUI.color = color;
        Vector2 diff = end - start;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        GUI.matrix = Matrix4x4.TRS(start, Quaternion.Euler(0, 0, angle), Vector3.one);
        GUI.Box(new Rect(0, -1, diff.magnitude, 2), "");
        GUI.matrix = Matrix4x4.identity;
        GUI.color = Color.white;
    }

    void DrawDot(Vector2 position, Color color, float size = 2f)
    {
        GUI.color = color;
        GUI.Box(new Rect(position.x - size / 2, position.y - size / 2, size, size), "");
        GUI.color = Color.white;
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    // Toggle functions for runtime control
    public void ToggleRays() { showRays = !showRays; if (lidarSensor != null) lidarSensor.visualizeRays = showRays; }
    public void ToggleRenderedRays() { showRenderedRays = !showRenderedRays; }
    public void ToggleUI() { showDebugUI = !showDebugUI; }
    public void ToggleMiniMap() { showMiniMap = !showMiniMap; }
}
