using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

/// <summary>
/// QMIX-compatible warehouse robot agent
/// Designed for multi-agent cooperative package delivery
/// </summary>
public class QMIXWarehouseAgent : Agent
{
    [Header("Agent Configuration")]
    public int agentID;
    public float moveSpeed = 2.0f;
    public float rotationSpeed = 180.0f;
    public float pickupRange = 1.5f;

    [Header("Environment Reference")]
    public QMIXWarehouseEnvironment environment;

    [Header("Current State")]
    public bool isCarryingPackage = false;
    public Package carriedPackage = null;

    [Header("Observation Settings")]
    public int maxObservableAgents = 2;      // Reduced from 4 (only 3 agents total)
    public int maxObservablePackages = 5;    // Reduced from 10 (saves memory)
    public int maxObservableZones = 3;       // Keep at 3
    public float observationRadius = 10f;

    private Vector3 startPosition;
    private Quaternion startRotation;

    // Action enumeration for QMIX
    // 0: Noop, 1: Forward, 2: Backward, 3: Turn Left, 4: Turn Right, 5: Pickup/Drop
    public enum QMIXAction { Noop = 0, Forward = 1, Backward = 2, TurnLeft = 3, TurnRight = 4, PickupDrop = 5 }

    public override void Initialize()
    {
        if (environment == null)
        {
            environment = GetComponentInParent<QMIXWarehouseEnvironment>();
        }

        startPosition = transform.position;
        startRotation = transform.rotation;

        Debug.Log($"QMIX Agent {agentID} initialized");
    }

    public override void OnEpisodeBegin()
    {
        // Reset agent state
        isCarryingPackage = false;
        if (carriedPackage != null)
        {
            carriedPackage.Drop();
            carriedPackage = null;
        }

        // Reset position
        transform.position = startPosition;
        transform.rotation = startRotation;

        // CRITICAL: Reset reward shaping distance tracker
        previousDistanceToGoal = float.MaxValue;
    }

    /// <summary>
    /// Collect observations for QMIX
    /// QMIX needs: local observations + global state (handled by environment)
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        // === SELF OBSERVATIONS ===
        // Position (normalized)
        sensor.AddObservation(transform.position.x / environment.gridWidth);
        sensor.AddObservation(transform.position.z / environment.gridHeight);

        // Rotation (normalized)
        sensor.AddObservation(transform.rotation.eulerAngles.y / 360f);

        // Velocity
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            sensor.AddObservation(rb.linearVelocity.x);
            sensor.AddObservation(rb.linearVelocity.z);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // Carrying package flag
        sensor.AddObservation(isCarryingPackage ? 1f : 0f);

        // === OTHER AGENTS (Local) ===
        QMIXWarehouseAgent[] allAgents = environment.GetAllAgents();
        List<QMIXWarehouseAgent> nearbyAgents = new List<QMIXWarehouseAgent>();

        foreach (var agent in allAgents)
        {
            if (agent != this && agent != null)
            {
                float distance = Vector3.Distance(transform.position, agent.transform.position);
                if (distance <= observationRadius)
                {
                    nearbyAgents.Add(agent);
                }
            }
        }

        // Add nearby agents (padded to maxObservableAgents)
        int agentCount = 0;
        foreach (var agent in nearbyAgents)
        {
            if (agentCount >= maxObservableAgents) break;

            Vector3 relativePos = agent.transform.position - transform.position;
            sensor.AddObservation(relativePos.x / observationRadius);
            sensor.AddObservation(relativePos.z / observationRadius);
            sensor.AddObservation(agent.isCarryingPackage ? 1f : 0f);
            agentCount++;
        }

        // Pad remaining agent observations
        for (int i = agentCount; i < maxObservableAgents; i++)
        {
            sensor.AddObservation(0f); // relative x
            sensor.AddObservation(0f); // relative z
            sensor.AddObservation(0f); // carrying flag
        }

        // === PACKAGES (Local) ===
        Package[] allPackages = FindObjectsByType<Package>(FindObjectsSortMode.None);
        List<Package> nearbyPackages = new List<Package>();

        foreach (var pkg in allPackages)
        {
            if (!pkg.isPickedUp && !pkg.isDelivered)
            {
                float distance = Vector3.Distance(transform.position, pkg.transform.position);
                if (distance <= observationRadius)
                {
                    nearbyPackages.Add(pkg);
                }
            }
        }

        // Sort by distance (closest first)
        nearbyPackages.Sort((a, b) =>
        {
            float distA = Vector3.Distance(transform.position, a.transform.position);
            float distB = Vector3.Distance(transform.position, b.transform.position);
            return distA.CompareTo(distB);
        });

        // Add nearby packages (padded to maxObservablePackages)
        int packageCount = 0;
        foreach (var pkg in nearbyPackages)
        {
            if (packageCount >= maxObservablePackages) break;

            Vector3 relativePos = pkg.transform.position - transform.position;
            sensor.AddObservation(relativePos.x / observationRadius);
            sensor.AddObservation(relativePos.z / observationRadius);

            // CRITICAL: Observe if package is requested (in queue)
            bool isRequested = pkg.homePosition != null && pkg.homePosition.isRequested;
            sensor.AddObservation(isRequested ? 1f : 0f);

            packageCount++;
        }

        // Pad remaining package observations
        for (int i = packageCount; i < maxObservablePackages; i++)
        {
            sensor.AddObservation(0f); // relative x
            sensor.AddObservation(0f); // relative z
            sensor.AddObservation(0f); // is requested
        }

        // === DELIVERY ZONES ===
        DeliveryZone[] allZones = FindObjectsByType<DeliveryZone>(FindObjectsSortMode.None);
        int zoneCount = 0;

        foreach (var zone in allZones)
        {
            if (zoneCount >= maxObservableZones) break;

            Vector3 relativePos = zone.transform.position - transform.position;
            sensor.AddObservation(relativePos.x / observationRadius);
            sensor.AddObservation(relativePos.z / observationRadius);
            sensor.AddObservation(zone.packagesDelivered / 10f); // Normalized count
            zoneCount++;
        }

        // Pad remaining zone observations
        for (int i = zoneCount; i < maxObservableZones; i++)
        {
            sensor.AddObservation(0f); // relative x
            sensor.AddObservation(0f); // relative z
            sensor.AddObservation(0f); // delivered count
        }
    }

    /// <summary>
    /// Execute actions from QMIX
    /// </summary>
    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];

        // Execute action
        switch ((QMIXAction)action)
        {
            case QMIXAction.Noop:
                // Do nothing
                break;

            case QMIXAction.Forward:
                MoveForward();
                break;

            case QMIXAction.Backward:
                MoveBackward();
                break;

            case QMIXAction.TurnLeft:
                TurnLeft();
                break;

            case QMIXAction.TurnRight:
                TurnRight();
                break;

            case QMIXAction.PickupDrop:
                HandlePickupDrop();
                break;
        }

        // NO time penalty - let reward shaping guide behavior
        // Reward shaping: Guide agents toward goal
        GiveShapingReward();
    }

    private float previousDistanceToGoal = float.MaxValue;

    void GiveShapingReward()
    {
        float distanceToGoal = float.MaxValue;

        if (!isCarryingPackage)
        {
            // Find nearest requested package
            Package nearestPackage = environment.GetNearestRequestedPackage(transform.position);
            if (nearestPackage != null)
            {
                distanceToGoal = Vector3.Distance(transform.position, nearestPackage.transform.position);
            }
        }
        else
        {
            // Find nearest delivery zone
            DeliveryZone nearestZone = environment.GetNearestDeliveryZone(transform.position);
            if (nearestZone != null)
            {
                distanceToGoal = Vector3.Distance(transform.position, nearestZone.transform.position);
            }
        }

        // ONLY reward for getting closer (no penalty for moving away)
        if (distanceToGoal < float.MaxValue && previousDistanceToGoal < float.MaxValue)
        {
            float improvement = previousDistanceToGoal - distanceToGoal;

            // OPTION 4: Defensive checks + cap to prevent reward explosions
            // Safety: Reject impossible improvements (agent can't teleport >50 units in one frame)
            if (improvement > 0 && improvement < 50f)
            {
                float reward = Mathf.Min(improvement * 0.01f, 0.05f); // Cap at 0.05 per step
                AddReward(reward);
                if (reward > 0.04f) // Log near-max rewards
                {
                    Debug.Log($"[PROGRESS] Agent {agentID} moving toward goal | Distance: {distanceToGoal:F2} | Reward: +{reward:F4}");
                }
            }
            else if (improvement >= 50f)
            {
                Debug.LogWarning($"[BUG DETECTED] Agent {agentID} impossible distance change: {improvement:F2} units! Skipping reward to prevent explosion.");
            }
        }

        previousDistanceToGoal = distanceToGoal;
    }

    /// <summary>
    /// Manual control for testing (optional)
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        if (Input.GetKey(KeyCode.W))
            discreteActions[0] = (int)QMIXAction.Forward;
        else if (Input.GetKey(KeyCode.S))
            discreteActions[0] = (int)QMIXAction.Backward;
        else if (Input.GetKey(KeyCode.A))
            discreteActions[0] = (int)QMIXAction.TurnLeft;
        else if (Input.GetKey(KeyCode.D))
            discreteActions[0] = (int)QMIXAction.TurnRight;
        else if (Input.GetKey(KeyCode.Space))
            discreteActions[0] = (int)QMIXAction.PickupDrop;
        else
            discreteActions[0] = (int)QMIXAction.Noop;
    }

    void MoveForward()
    {
        Vector3 newPosition = transform.position + transform.forward * moveSpeed * Time.fixedDeltaTime;
        transform.position = ClampToBounds(newPosition);
    }

    void MoveBackward()
    {
        Vector3 newPosition = transform.position - transform.forward * moveSpeed * Time.fixedDeltaTime;
        transform.position = ClampToBounds(newPosition);
    }

    Vector3 ClampToBounds(Vector3 position)
    {
        // Your warehouse is offset from origin, so we need to calculate the actual bounds
        // Based on your package positions: X: -10 to -2, Z: -13 to -6
        // Center is approximately at X: -6, Z: -9.5
        float centerX = -6f;
        float centerZ = -9.5f;

        float halfWidth = environment.gridWidth / 2f;
        float halfHeight = environment.gridHeight / 2f;

        position.x = Mathf.Clamp(position.x, centerX - halfWidth, centerX + halfWidth);
        position.z = Mathf.Clamp(position.z, centerZ - halfHeight, centerZ + halfHeight);

        return position;
    }

    void TurnLeft()
    {
        transform.Rotate(0, -rotationSpeed * Time.fixedDeltaTime, 0);
    }

    void TurnRight()
    {
        transform.Rotate(0, rotationSpeed * Time.fixedDeltaTime, 0);
    }

    void HandlePickupDrop()
    {
        if (isCarryingPackage)
        {
            DropPackage();
        }
        else
        {
            TryPickupPackage();
        }
    }

    void TryPickupPackage()
    {
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
            isCarryingPackage = true;

            // Reward for picking up (increased to make it more noticeable)
            AddReward(0.5f);
            Debug.Log($"[PICKUP] Agent {agentID} picked up package {carriedPackage.packageID} | Reward: +0.5");

            environment.OnPackagePickedUp(this, carriedPackage);
        }
    }

    void DropPackage()
    {
        if (carriedPackage == null) return;

        // Check if in delivery zone
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

        if (currentZone != null)
        {
            // SUCCESSFUL DELIVERY - Big reward!
            // Note: Don't call MarkAsDelivered() here - environment will handle package reset
            currentZone.OnPackageDelivered();

            AddReward(1.0f); // Individual reward

            // CRITICAL FIX: Store package reference before clearing
            Package deliveredPackage = carriedPackage;

            // CRITICAL FIX: Clear agent state BEFORE calling environment handler
            // This prevents the agent from picking up the same package again immediately
            carriedPackage = null;
            isCarryingPackage = false;

            // Call environment delivery handler (handles both RWARE queue and team rewards)
            // This will call ReturnHome() which teleports the package back to its shelf
            environment.OnPackageDelivered(this, deliveredPackage, currentZone);

            Debug.Log($"[DELIVERY] Agent {agentID} delivered package {deliveredPackage.packageID} to zone {currentZone.zoneID}");
        }
        else
        {
            // Dropped outside zone - small penalty
            Vector3 dropPosition = transform.position + transform.forward * 0.5f;
            carriedPackage.Drop();
            carriedPackage.transform.position = dropPosition;
            AddReward(-0.05f);

            carriedPackage = null;
            isCarryingPackage = false;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Penalty for collisions with other agents
        if (collision.gameObject.GetComponent<QMIXWarehouseAgent>() != null)
        {
            AddReward(-0.1f);
        }
    }

    void OnDrawGizmos()
    {
        // Draw observation radius
        Gizmos.color = new Color(0, 1, 1, 0.2f);
        Gizmos.DrawWireSphere(transform.position, observationRadius);

        // Draw pickup range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
    }
}
