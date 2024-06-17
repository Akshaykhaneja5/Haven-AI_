using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CylinderDomeSpawner : MonoBehaviour
{
    public List<GameObject> cylinderPrefabs;  // List of cylinder prefabs for spawning
    public float spawnDelay = 0.1f;           // Delay in seconds between spawns
    public int numberOfCylinders = 50;        // Total number of cylinders to spawn
    public float heightOffset = 0f;           // Height offset for each cylinder to avoid intersection
    public float mass = 1.0f;                 // Mass of each Rigidbody

    private ParametricDome dome;              // Reference to the ParametricDome script

    void Start()
    {
        dome = GetComponent<ParametricDome>();  // Get the ParametricDome component
        if (dome != null)
        {
            StartCoroutine(SpawnCylindersAroundDome());
        }
        else
        {
            Debug.LogError("ParametricDome component not found.");
        }
    }

    IEnumerator SpawnCylindersAroundDome()
    {
        if (cylinderPrefabs.Count == 0)
        {
            Debug.LogError("No cylinder prefabs assigned.");
            yield break;  // Stop the coroutine if no prefabs are assigned
        }

        Mesh domeMesh = dome.GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = domeMesh.vertices;

        if (vertices.Length == 0)
        {
            Debug.LogError("Dome mesh has no vertices.");
            yield break;
        }

        Debug.Log("Starting to spawn cylinders. Total vertices: " + vertices.Length);

        float cylinderRadius = cylinderPrefabs[0].transform.localScale.x / 2f; // Assuming uniform scale and cylindrical shape
        float cylinderHeight = cylinderPrefabs[0].transform.localScale.y;

        int spawnedCount = 0;
        List<Vector3> spawnPositions = new List<Vector3>();

        for (int i = 0; i < vertices.Length; i++)
        {
            if (spawnedCount >= numberOfCylinders) break;

            Vector3 worldVertex = dome.transform.TransformPoint(vertices[i]);
            Vector3 spawnPosition = worldVertex + Vector3.up * (cylinderHeight / 2 + heightOffset);

            bool canSpawn = true;
            foreach (Vector3 pos in spawnPositions)
            {
                if (Vector3.Distance(pos, spawnPosition) < 2 * cylinderRadius)
                {
                    canSpawn = false;
                    break;
                }
            }

            if (canSpawn)
            {
                GameObject selectedPrefab = cylinderPrefabs[Random.Range(0, cylinderPrefabs.Count)];
                GameObject cylinder = Instantiate(selectedPrefab, spawnPosition, Quaternion.Euler(90f, 0f, 0f));

                Rigidbody existingRB = cylinder.GetComponent<Rigidbody>();
                if (existingRB != null)
                    Destroy(existingRB);

                cylinder.tag = "RigidbodyObject";
                spawnPositions.Add(spawnPosition);
                spawnedCount++;

                yield return new WaitForSeconds(spawnDelay);
            }
        }

        Debug.Log("Finished spawning cylinders.");
    }
}
