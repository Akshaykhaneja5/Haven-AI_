using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class CylinderPlacementAgent : Agent
{
    public List<GameObject> cylinderPrefabs;   // List of cylinder prefabs for spawning
    public GameObject domeObject;              // Dome GameObject
    public GameObject planePrefab;             // Plane Prefab to prevent cylinders from falling
    public GameObject cylinderContainer;       // Container for spawned cylinders
    public int numberOfCylindersToPlace = 5;   // Number of cylinders to place

    private GameObject plane;                  // Instance of the plane
    private ParametricDome dome;               // ParametricDome script for accessing dome structure
    public float heightOffset = 0.1f;          // Height offset for each cylinder to avoid intersection

    private List<GameObject> spawnedCylinders = new List<GameObject>();
    private HashSet<int> usedVertices = new HashSet<int>();  // Using a HashSet for quick lookup and unique entries
    private int totalCylindersPlaced = 0;                    // Total number of cylinders successfully placed
    private List<int> vertexIndices = new List<int>();       // List of vertex indices

    public override void Initialize()
    {
        // Ensure the dome is static
        domeObject.isStatic = true;

        // Ensure the domeObject has the ParametricDome component
        dome = domeObject.GetComponent<ParametricDome>();
        if (dome == null)
        {
            Debug.LogError("ParametricDome component not found on the assigned domeObject.");
            return;
        }

        // Ensure the domeObject has a MeshFilter component
        MeshFilter meshFilter = domeObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogError("MeshFilter component not found on the domeObject.");
            return;
        }

        // Ensure the MeshFilter component has a mesh assigned
        Mesh mesh = meshFilter.mesh;
        if (mesh == null)
        {
            Debug.LogError("Mesh is not assigned on the MeshFilter of the domeObject.");
            return;
        }

        // Initialize vertex indices list
        vertexIndices = new List<int>();
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            vertexIndices.Add(i);
        }

        // Instantiate the plane to prevent cylinders from falling indefinitely
        plane = Instantiate(planePrefab, new Vector3(0, -1, 0), Quaternion.identity); // Position the plane slightly below the cylinders
        plane.transform.localScale = new Vector3(100, 1, 100); // Ensure the plane is large enough to catch all falling cylinders
        plane.AddComponent<BoxCollider>(); // Ensure the plane has a collider

        // Debug logging to verify plane position
        Debug.Log($"Plane position: {plane.transform.position}, scale: {plane.transform.localScale}");
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("OnEpisodeBegin called.");
        foreach (GameObject cylinder in spawnedCylinders)
        {
            Destroy(cylinder);
        }
        spawnedCylinders.Clear();
        usedVertices.Clear();
        totalCylindersPlaced = 0;

        // Seed Unity's random number generator with a unique value
        UnityEngine.Random.InitState(System.DateTime.Now.Millisecond + GetInstanceID());

        // Shuffle and sort vertex indices in a helical manner
        SortVerticesHelically();

        Debug.Log($"Episode {CompletedEpisodes + 1} started. Used vertices: {usedVertices.Count}, Remaining vertices: {GetVertexCount() - usedVertices.Count}");
        Debug.Log($"Vertex indices after shuffling: {string.Join(", ", vertexIndices.Take(10))}...");

        // Start placing cylinders
        StartCoroutine(PlaceCylinders());
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        int vertexCount = GetVertexCount();
        // Number of unused vertices
        sensor.AddObservation(vertexCount - usedVertices.Count);
        // Number of cylinders placed
        sensor.AddObservation(totalCylindersPlaced);

        // Adjust the observation space as needed
        int totalExpectedObservations = 31;
        int currentObservations = 2; // Including totalCylindersPlaced and unused vertices
        for (int i = currentObservations; i < totalExpectedObservations; i++)
        {
            sensor.AddObservation(0);
        }
    }

    private IEnumerator PlaceCylinders()
    {
        for (int i = 0; i < numberOfCylindersToPlace; i++)
        {
            bool placedSuccessfully = false;
            while (!placedSuccessfully && vertexIndices.Count > 0)
            {
                int vertexIndex = vertexIndices[0];
                vertexIndices.RemoveAt(0);

                if (usedVertices.Contains(vertexIndex))
                {
                    continue;
                }

                Vector3 worldVertex = dome.transform.TransformPoint(dome.GetComponent<MeshFilter>().mesh.vertices[vertexIndex]);
                GameObject selectedPrefab = SelectRandomPrefab(); // Automatically select a random prefab
                Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
                GameObject cylinder = Instantiate(selectedPrefab, worldVertex + Vector3.up * heightOffset, rotation, cylinderContainer.transform);

                if (!cylinder.GetComponent<Collider>())
                {
                    cylinder.AddComponent<BoxCollider>(); // Ensure the cylinder has a collider
                }

                spawnedCylinders.Add(cylinder);
                yield return new WaitForSeconds(0.2f); // Small delay to visualize the placement

                if (IsCylinderOverlapping(cylinder))
                {
                    Debug.Log($"Cylinder at vertex {vertexIndex} overlapped with another cylinder and was removed.");
                    spawnedCylinders.Remove(cylinder);
                    Destroy(cylinder);
                    AddReward(-1.0f);
                }
                else
                {
                    placedSuccessfully = true;
                    usedVertices.Add(vertexIndex); // Mark vertex as used
                    Debug.Log($"Placed cylinder at vertex {vertexIndex}, position: {cylinder.transform.position}");

                    // Reward based on proximity to other cylinders
                    float distance = CalculateMinimumDistanceToOtherCylinders(cylinder);
                    if (distance < 0.05f) // Adjusted threshold
                    {
                        AddReward(1.0f);
                        Debug.Log($"Reward: +1.0 for close placement. Distance: {distance}");
                    }
                    else
                    {
                        AddReward(-0.5f);
                        Debug.Log($"Reward: -0.5 for far placement. Distance: {distance}");
                    }
                }
            }

            yield return new WaitForSeconds(0.2f); // Small delay between placements
        }

        // Check stability of all placed cylinders
        StartCoroutine(CheckAllCylindersStability());
    }

    private bool IsCylinderOverlapping(GameObject cylinder)
    {
        Collider[] hitColliders = Physics.OverlapSphere(cylinder.transform.position, 0.05f); // Adjust the radius as needed
        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.gameObject != cylinder && spawnedCylinders.Contains(hitCollider.gameObject))
            {
                float distance = Vector3.Distance(cylinder.transform.position, hitCollider.transform.position);
                if (distance < 0.05f) // Adjust the threshold as needed
                {
                    Debug.Log($"Detected overlap with {hitCollider.gameObject.name} at distance {distance}");
                    return true;
                }
            }
        }
        return false;
    }

    private IEnumerator CheckAllCylindersStability()
    {
        // Add rigid bodies to all placed cylinders
        foreach (GameObject cylinder in spawnedCylinders)
        {
            if (cylinder != null && cylinder.activeInHierarchy)
            {
                Rigidbody rb = cylinder.AddComponent<Rigidbody>();
                rb.mass = 1.0f;  // Set the mass as appropriate for your game's physics scale
                rb.angularDrag = 0.05f;  // Adjust for realistic rotation damping
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }

        yield return new WaitForSeconds(2); // Wait for all cylinders to settle

        // Check stability of all placed cylinders
        foreach (GameObject cylinder in spawnedCylinders)
        {
            if (cylinder != null && cylinder.activeInHierarchy)
            {
                if (cylinder.transform.position.y < plane.transform.position.y)
                {
                    Debug.Log($"Cylinder fell below the plane at position {cylinder.transform.position}");
                    AddReward(-2.0f);
                    Debug.Log("Reward: -2.0 for falling below plane.");
                    Destroy(cylinder);
                    continue;
                }

                if (cylinder.transform.up.y < 0.9)
                {
                    AddReward(-2.0f);
                    Debug.Log($"Reward: -2.0 for toppling. Cylinder at position {cylinder.transform.position} toppled.");
                }
                else
                {
                    AddReward(1.0f);
                    totalCylindersPlaced++;
                    Debug.Log($"Reward: +1.0 for stable placement. Cylinder successfully placed at position {cylinder.transform.position}.");
                }
            }
        }

        if (totalCylindersPlaced == numberOfCylindersToPlace)
        {
            AddReward(10.0f);
            Debug.Log("Reward: +10.0 for placing all cylinders successfully.");
        }

        EndEpisode();
    }

    private float CalculateMinimumDistanceToOtherCylinders(GameObject cylinder)
    {
        float minDistance = float.MaxValue;
        foreach (GameObject otherCylinder in spawnedCylinders)
        {
            if (otherCylinder != cylinder)
            {
                float distance = Vector3.Distance(cylinder.transform.position, otherCylinder.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }
        }
        return minDistance;
    }

    private GameObject SelectRandomPrefab()
    {
        int randomIndex = Random.Range(0, cylinderPrefabs.Count);
        return cylinderPrefabs[randomIndex];
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (vertexIndices.Count > 0)
        {
            int vertexIndex = vertexIndices[0];
            vertexIndices.RemoveAt(0);
            discreteActionsOut[0] = vertexIndex;
            Debug.Log($"Heuristic action: selected vertex {vertexIndex}");
        }
    }

    private int GetVertexCount()
    {
        return dome.GetComponent<MeshFilter>().mesh.vertexCount;
    }

    private void SortVerticesHelically()
    {
        Mesh mesh = dome.GetComponent<MeshFilter>().mesh;
        var vertices = mesh.vertices.Select((v, index) => new { Vertex = v, Index = index }).ToList();

        // Sort vertices by their y-coordinate
        vertices.Sort((a, b) => a.Vertex.y.CompareTo(b.Vertex.y));

        vertexIndices = new List<int>();

        // Group vertices by height (y-coordinate) and create helical order within each group
        float currentHeight = vertices[0].Vertex.y;
        List<int> currentGroup = new List<int>();

        foreach (var item in vertices)
        {
            if (Mathf.Abs(item.Vertex.y - currentHeight) > heightOffset)
            {
                // Shuffle current group using Unity's random instance
                currentGroup = currentGroup.OrderBy(x => Random.value).ToList();

                // Sort the shuffled group in helical order
                currentGroup.Sort((a, b) =>
                    Mathf.Atan2(vertices[a].Vertex.z, vertices[a].Vertex.x).CompareTo(
                        Mathf.Atan2(vertices[b].Vertex.z, vertices[b].Vertex.x)));

                vertexIndices.AddRange(currentGroup);
                currentGroup.Clear();
                currentHeight = item.Vertex.y;
            }
            currentGroup.Add(item.Index);
        }

        // Shuffle and sort the last group
        currentGroup = currentGroup.OrderBy(x => Random.value).ToList();
        currentGroup.Sort((a, b) =>
            Mathf.Atan2(vertices[a].Vertex.z, vertices[a].Vertex.x).CompareTo(
                Mathf.Atan2(vertices[b].Vertex.z, vertices[b].Vertex.x)));
        vertexIndices.AddRange(currentGroup);

        // Debug log to verify randomization
        Debug.Log($"Vertex indices after shuffling: {string.Join(", ", vertexIndices.Take(10))}...");
    }

    private void OnApplicationQuit()
    {
        Debug.Log($"Application quitting. Used vertices: {usedVertices.Count}, Remaining vertices: {GetVertexCount() - usedVertices.Count}");
    }
}
