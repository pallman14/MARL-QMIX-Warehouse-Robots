using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Environment manager for QMIX multi-agent training
/// Handles centralized rewards and episode management
/// </summary>
public class QMIXWarehouseEnvironment : MonoBehaviour
{
    [Header("Grid Configuration")]
    public float gridWidth = 10f;
    public float gridHeight = 10f;

    [Header("Episode Settings")]
    public int maxEpisodeSteps = 1000;
    public int currentStep = 0;

    [Header("Agents")]
    public QMIXWarehouseAgent[] agents;

    [Header("Spawning")]
    public GameObject agentPrefab;
    public GameObject packagePrefab;
    public GameObject deliveryZonePrefab;

    public int numberOfPackages = 5;
    public int numberOfDeliveryZones = 2;

    [Header("Package Spawn Positions")]
    [Tooltip("Leave empty to spawn randomly. Add positions for specific spawn points.")]
    public Transform[] packageSpawnPoints;
    [Tooltip("If true, use spawn points. If false, use random positions.")]
    public bool useSpawnPoints = false;

    [Header("Request Queue System (RWARE-style)")]
    [Tooltip("Number of packages requested at once")]
    public int requestQueueSize = 5;

    [Tooltip("Auto-detect existing packages in scene")]
    public bool useExistingPackages = true;

    [Header("Episode Stats")]
    public int totalPackagesDelivered = 0;
    public float teamReward = 0f;

    private List<Package> activePackages = new List<Package>();
    private List<PackageHomePosition> packagePositions = new List<PackageHomePosition>();
    private List<Package> requestQueue = new List<Package>();
    private List<DeliveryZone> deliveryZones = new List<DeliveryZone>();

    void Start()
    {
        InitializeEnvironment();
    }

    public void InitializeEnvironment()
    {
        // Find or create agents
        if (agents == null || agents.Length == 0)
        {
            agents = GetComponentsInChildren<QMIXWarehouseAgent>();
        }

        // Assign agent IDs
        for (int i = 0; i < agents.Length; i++)
        {
            agents[i].agentID = i;
            agents[i].environment = this;
        }

        if (useExistingPackages)
        {
            // Wait for PackageHomePosition components to register themselves
            // Then initialize request queue
            Invoke("InitializeRequestQueue", 0.1f);
            Debug.Log($"QMIX Environment: Using existing packages in scene. Will initialize request queue...");
        }
        else
        {
            // Spawn packages dynamically
            SpawnPackages();
        }

        // Spawn delivery zones
        SpawnDeliveryZones();

        Debug.Log($"QMIX Environment initialized: {agents.Length} agents, {numberOfDeliveryZones} zones");
    }

    void SpawnPackages()
    {
        // Clear existing packages
        foreach (var pkg in activePackages)
        {
            if (pkg != null) Destroy(pkg.gameObject);
        }
        activePackages.Clear();

        // Spawn new packages
        for (int i = 0; i < numberOfPackages; i++)
        {
            Vector3 spawnPos = GetRandomSpawnPosition();
            GameObject pkgObj;

            if (packagePrefab != null)
            {
                pkgObj = Instantiate(packagePrefab, spawnPos, Quaternion.identity, transform);
                // Scale down realistic boxes to pickupable size
                pkgObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }
            else
            {
                pkgObj = new GameObject($"Package_{i}");
                pkgObj.transform.position = spawnPos;
                pkgObj.transform.SetParent(transform);
                Package pkg = pkgObj.AddComponent<Package>();
                pkg.packageID = i;
            }

            Package packageComponent = pkgObj.GetComponent<Package>();
            if (packageComponent != null)
            {
                packageComponent.packageID = i;
                activePackages.Add(packageComponent);
            }
        }
    }

    void SpawnDeliveryZones()
    {
        // Clear existing zones
        foreach (var zone in deliveryZones)
        {
            if (zone != null) Destroy(zone.gameObject);
        }
        deliveryZones.Clear();

        // Spawn new zones at edges
        for (int i = 0; i < numberOfDeliveryZones; i++)
        {
            Vector3 spawnPos = new Vector3(
                gridWidth / 2f - 2f,
                0,
                -gridHeight / 2f + (i + 1) * (gridHeight / (numberOfDeliveryZones + 1))
            );

            GameObject zoneObj;
            if (deliveryZonePrefab != null)
            {
                zoneObj = Instantiate(deliveryZonePrefab, spawnPos, Quaternion.identity, transform);
            }
            else
            {
                zoneObj = new GameObject($"DeliveryZone_{i}");
                zoneObj.transform.position = spawnPos;
                zoneObj.transform.SetParent(transform);
                DeliveryZone zone = zoneObj.AddComponent<DeliveryZone>();
                zone.zoneID = i;
            }

            DeliveryZone zoneComponent = zoneObj.GetComponent<DeliveryZone>();
            if (zoneComponent != null)
            {
                zoneComponent.zoneID = i;
                deliveryZones.Add(zoneComponent);
            }
        }
    }

    Vector3 GetRandomSpawnPosition()
    {
        // Use spawn points if configured
        if (useSpawnPoints && packageSpawnPoints != null && packageSpawnPoints.Length > 0)
        {
            // Randomly select from configured spawn points
            int randomIndex = Random.Range(0, packageSpawnPoints.Length);
            Transform spawnPoint = packageSpawnPoints[randomIndex];

            if (spawnPoint != null)
            {
                return spawnPoint.position;
            }
            else
            {
                Debug.LogWarning($"Spawn point {randomIndex} is null, using random position instead");
            }
        }

        // Fallback: Random position in warehouse
        return new Vector3(
            Random.Range(-gridWidth / 2f + 2f, gridWidth / 2f - 2f),
            0.1f,
            Random.Range(-gridHeight / 2f + 2f, gridHeight / 2f - 2f)
        );
    }

    /// <summary>
    /// Register a package home position (called by PackageHomePosition.Start())
    /// </summary>
    public void RegisterPackagePosition(PackageHomePosition position)
    {
        if (!packagePositions.Contains(position))
        {
            position.positionID = packagePositions.Count;
            packagePositions.Add(position);

            // Also register the package itself
            if (position.currentPackage != null && !activePackages.Contains(position.currentPackage))
            {
                position.currentPackage.packageID = position.positionID;
                position.currentPackage.homePosition = position;
                activePackages.Add(position.currentPackage);
            }

            Debug.Log($"Registered package position {position.positionID} at {position.transform.position}");
        }
    }

    /// <summary>
    /// Initialize request queue with random packages
    /// </summary>
    public void InitializeRequestQueue()
    {
        requestQueue.Clear();

        if (activePackages.Count == 0)
        {
            Debug.LogWarning("No packages available to request!");
            return;
        }

        // Select random packages for request queue
        int requestCount = Mathf.Min(requestQueueSize, activePackages.Count);
        List<Package> availablePackages = new List<Package>(activePackages);

        for (int i = 0; i < requestCount; i++)
        {
            int randomIndex = Random.Range(0, availablePackages.Count);
            Package package = availablePackages[randomIndex];

            requestQueue.Add(package);
            availablePackages.RemoveAt(randomIndex);

            // Mark package as requested
            if (package.homePosition != null)
            {
                package.homePosition.SetRequested(true);
            }
        }

        Debug.Log($"Request queue initialized with {requestQueue.Count} packages");
    }

    /// <summary>
    /// Handle package delivery (RWARE-style)
    /// </summary>
    public void OnPackageDelivered(Package package, QMIXWarehouseAgent agent)
    {
        if (!requestQueue.Contains(package))
        {
            Debug.LogWarning($"Package {package.packageID} was not in request queue!");
            return;
        }

        // Give reward
        agent.AddReward(1.0f);
        totalPackagesDelivered++;

        Debug.Log($"Package {package.packageID} delivered by Agent {agent.agentID}! Total delivered: {totalPackagesDelivered}");

        // Remove from request queue
        requestQueue.Remove(package);

        // Disable glow for delivered package
        if (package.homePosition != null)
        {
            package.homePosition.SetRequested(false);
        }

        // Mark as delivered
        package.MarkAsDelivered();

        // Return package to home position
        package.ReturnHome();

        // Immediately request a new package (RWARE-style)
        RequestNewPackage();
    }

    /// <summary>
    /// Add a new package to the request queue
    /// </summary>
    void RequestNewPackage()
    {
        // Find packages not currently requested, not picked up, and not delivered
        List<Package> availablePackages = new List<Package>();
        foreach (var pkg in activePackages)
        {
            if (pkg != null && !requestQueue.Contains(pkg) && !pkg.isPickedUp && !pkg.isDelivered)
            {
                availablePackages.Add(pkg);
            }
        }

        if (availablePackages.Count == 0)
        {
            Debug.Log("No more packages available to request (all are either requested, being carried, or delivered)");
            return;
        }

        // Randomly select one
        int randomIndex = Random.Range(0, availablePackages.Count);
        Package newRequest = availablePackages[randomIndex];

        requestQueue.Add(newRequest);

        // Mark as requested
        if (newRequest.homePosition != null)
        {
            newRequest.homePosition.SetRequested(true);
        }

        Debug.Log($"New package requested: {newRequest.packageID}. Request queue size: {requestQueue.Count}");
    }

    void FixedUpdate()
    {
        currentStep++;

        // Check if episode should end
        if (currentStep >= maxEpisodeSteps || AllPackagesDelivered())
        {
            EndEpisode();
        }
    }

    bool AllPackagesDelivered()
    {
        foreach (var pkg in activePackages)
        {
            if (pkg != null && !pkg.isDelivered)
            {
                return false;
            }
        }
        return true;
    }

    void EndEpisode()
    {
        // Give bonus if all packages delivered
        if (AllPackagesDelivered())
        {
            float bonus = 10f;
            foreach (var agent in agents)
            {
                agent.AddReward(bonus);
            }
            Debug.Log($"Episode complete! All packages delivered. Bonus: {bonus}");
        }

        Debug.Log($"Episode ended. Steps: {currentStep}, Packages delivered: {totalPackagesDelivered}/{numberOfPackages}");

        // Reset episode
        currentStep = 0;
        totalPackagesDelivered = 0;
        teamReward = 0f;

        // Reset all agents
        foreach (var agent in agents)
        {
            agent.EndEpisode();
        }

        // Reset packages based on mode
        if (useExistingPackages)
        {
            // Return all existing packages to their home positions
            foreach (var pkg in activePackages)
            {
                if (pkg != null)
                {
                    pkg.ReturnHome();
                }
            }
            // Re-initialize request queue
            InitializeRequestQueue();
        }
        else
        {
            // Respawn packages dynamically
            SpawnPackages();
        }

        // Reset delivery zones
        foreach (var zone in deliveryZones)
        {
            zone.packagesDelivered = 0;
        }
    }

    /// <summary>
    /// Called when an agent picks up a package
    /// </summary>
    public void OnPackagePickedUp(QMIXWarehouseAgent agent, Package package)
    {
        // Could add team coordination rewards here
        Debug.Log($"Agent {agent.agentID} picked up package {package.packageID}");
    }

    /// <summary>
    /// Called when an agent delivers a package
    /// This is where QMIX's centralized reward comes in
    /// </summary>
    public void OnPackageDelivered(QMIXWarehouseAgent agent, Package package, DeliveryZone zone)
    {
        // CRITICAL: Handle RWARE queue management first
        if (requestQueue.Contains(package))
        {
            // Remove from request queue
            requestQueue.Remove(package);

            // Disable glow for delivered package
            if (package.homePosition != null)
            {
                package.homePosition.SetRequested(false);
            }

            // Return package to home position
            package.ReturnHome();

            // Immediately request a new package (RWARE-style)
            RequestNewPackage();

            Debug.Log($"Package {package.packageID} returned to home. Request queue size: {requestQueue.Count}");
        }
        else
        {
            Debug.LogWarning($"Package {package.packageID} was not in request queue!");
        }

        totalPackagesDelivered++;

        // Team reward - all agents benefit from any delivery
        float teamRewardValue = 0.5f;
        teamReward += teamRewardValue;

        foreach (var a in agents)
        {
            if (a != agent) // Exclude the delivering agent (they already got individual reward)
            {
                a.AddReward(teamRewardValue);
            }
        }

        Debug.Log($"Package {package.packageID} delivered! Total: {totalPackagesDelivered}. Team reward: {teamRewardValue}");
    }

    /// <summary>
    /// Get all agents in the environment
    /// </summary>
    public QMIXWarehouseAgent[] GetAllAgents()
    {
        return agents;
    }

    /// <summary>
    /// Get global state for QMIX mixer network
    /// This should encode the full state of the environment
    /// </summary>
    public float[] GetGlobalState()
    {
        List<float> state = new List<float>();

        // Episode progress
        state.Add(currentStep / (float)maxEpisodeSteps);

        // Total packages delivered
        state.Add(totalPackagesDelivered / (float)numberOfPackages);

        // All agent positions and states
        foreach (var agent in agents)
        {
            state.Add(agent.transform.position.x / gridWidth);
            state.Add(agent.transform.position.z / gridHeight);
            state.Add(agent.isCarryingPackage ? 1f : 0f);
        }

        // All package positions and states
        foreach (var pkg in activePackages)
        {
            if (pkg != null)
            {
                state.Add(pkg.transform.position.x / gridWidth);
                state.Add(pkg.transform.position.z / gridHeight);
                state.Add(pkg.isPickedUp ? 1f : 0f);
                state.Add(pkg.isDelivered ? 1f : 0f);
            }
        }

        // Delivery zone positions and counts
        foreach (var zone in deliveryZones)
        {
            state.Add(zone.transform.position.x / gridWidth);
            state.Add(zone.transform.position.z / gridHeight);
            state.Add(zone.packagesDelivered / (float)numberOfPackages);
        }

        return state.ToArray();
    }

    /// <summary>
    /// Helper: Find nearest requested package to given position
    /// </summary>
    public Package GetNearestRequestedPackage(Vector3 position)
    {
        Package nearest = null;
        float minDistance = float.MaxValue;

        foreach (var pkg in requestQueue)
        {
            if (pkg != null && !pkg.isPickedUp)
            {
                float dist = Vector3.Distance(position, pkg.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest = pkg;
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// Helper: Find nearest delivery zone to given position
    /// </summary>
    public DeliveryZone GetNearestDeliveryZone(Vector3 position)
    {
        DeliveryZone nearest = null;
        float minDistance = float.MaxValue;

        foreach (var zone in deliveryZones)
        {
            if (zone != null)
            {
                float dist = Vector3.Distance(position, zone.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest = zone;
                }
            }
        }

        return nearest;
    }

    void OnDrawGizmos()
    {
        // Draw environment bounds
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(gridWidth, 0.1f, gridHeight));
    }
}
