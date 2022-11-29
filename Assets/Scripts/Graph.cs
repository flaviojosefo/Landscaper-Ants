using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

[Serializable]
public sealed class Graph {

    [Header("Nodes Generation Settings")]
    [SerializeField, Range(4, 100)] private int nodesAmount = 4;
    [SerializeField, Range(0, 25f)] private float width = 10f;
    [SerializeField, Range(0, 10f)] private float height = 10f;
    [SerializeField, Range(0.01f, 1f)] private float initPheromones = 0.01f;

    [Header("Nodes Display Settings")]
    [SerializeField, Range(0.05f, 1f)] private float nodeRadius = 0.3f;
    [SerializeField] private GameObject nodeSprite;
    [SerializeField] private Transform nodesSpawn;

    [Header("Limits Display Settings")]
    [SerializeField, Range(0f, 1f)] private float limitsOffset = 0.25f;
    [SerializeField] private LineRenderer limitsLine;

    private Vector3[] nodes;          // The nodes that compose the graph
    private float[,] costMatrix;      // The cost between all nodes
    private float[,] pheromoneMatrix; // The pheromones values on all edges

    private int highlighted;          // The colored node (always starts at 0)

    public Vector3[] Nodes => nodes;
    public float[,] CostMatrix => costMatrix;
    public float[,] PheromoneMatrix => pheromoneMatrix;

    // Setup all Graph variables
    public void Generate() {

        CreateNodes();
        DisplayGraph();

        CreateCostMatrix();
        CreatePheromoneMatrix();
    }

    // Creates the graph's nodes (randomly)
    private void CreateNodes() {

        nodes = new Vector3[nodesAmount];

        for (int i = 0; i < nodes.Length; i++) {

            float x = Random.Range(-width, width);
            float z = Random.Range(-height, height);

            nodes[i] = new(x, 0, z);
        }
    }

    // Calculate cost (distance) between ALL nodes (i.e. all nodes are connected)
    private void CreateCostMatrix() {

        int nAmount = nodes.Length;

        costMatrix = new float[nAmount, nAmount];

        for (int i = 0; i < nAmount; i++) {

            for (int j = i + 1; j < nAmount; j++) {

                float cost = Vector3.Distance(nodes[i], nodes[j]);

                costMatrix[i, j] = costMatrix[j, i] = cost;
            }
        }
    }

    // Create the pheromones' matrix with a specified initial value
    private void CreatePheromoneMatrix() {

        int nAmount = nodes.Length;

        pheromoneMatrix = new float[nAmount, nAmount];

        for (int i = 0; i < nAmount; i++) {

            for (int j = i + 1; j < nAmount; j++) {

                pheromoneMatrix[i, j] = pheromoneMatrix[j, i] = initPheromones;
            }
        }
    }

    // Get a node's neighbours' indices
    public int[] GetNeighbours(int node) {

        List<int> neighbours = new();

        // Find neighbours of 'node' based on paths
        for (int i = 0; i < nodes.Length; i++) {

            // node = from | i = to
            if (costMatrix[node, i] != 0) {

                neighbours.Add(i);
            }
        }

        return neighbours.ToArray();
    }

    // Updates the highlighted node (should always be the first one on the sequence)
    public void UpdateHighlightedNode(int newHighlighted) {

        nodesSpawn.GetChild(highlighted).GetComponent<SpriteRenderer>().color = Color.white;
        highlighted = newHighlighted;
        nodesSpawn.GetChild(highlighted).GetComponent<SpriteRenderer>().color = Color.blue;
    }

    // Displays both the nodes and limits of the graph
    private void DisplayGraph() {

        // ----- NODES ----- > NOT OPTIMAL BUT IT WORKS <

        // Destroy all previous nodes
        for (int i = 0; i < nodesSpawn.childCount; i++) {

            Object.Destroy(nodesSpawn.GetChild(i).gameObject);
        }

        Vector3 nodeSpriteScale = new(nodeRadius, nodeRadius, nodeRadius);

        // Instantiate the first one with a different color
        highlighted = 0;
        Vector3 firstPosOffset = new(nodes[0].x, nodes[0].y + 0.01f, nodes[0].z); // Draw on top of line
        Transform first = Object.Instantiate(nodeSprite, firstPosOffset, nodeSprite.transform.rotation, nodesSpawn).transform;
        first.localScale = nodeSpriteScale;
        first.GetComponent<SpriteRenderer>().color = Color.blue;

        // Instantiate all other nodes
        for (int i = 1; i < nodes.Length; i++) {

            Vector3 nodePosOffset = new(nodes[i].x, nodes[i].y + 0.01f, nodes[i].z);
            Object.Instantiate(nodeSprite, nodePosOffset, nodeSprite.transform.rotation, nodesSpawn).transform.localScale = nodeSpriteScale;
        }

        // ----- LIMITS -----

        limitsLine.positionCount = 4;

        // Add an offset so the limits aren't drawn on top of any nodes
        float limitWidth = width + limitsOffset;
        float limitHeight = height + limitsOffset;

        limitsLine.SetPosition(0, new Vector3(-limitWidth, 0, limitHeight));
        limitsLine.SetPosition(1, new Vector3(limitWidth, 0, limitHeight));
        limitsLine.SetPosition(2, new Vector3(limitWidth, 0, -limitHeight));
        limitsLine.SetPosition(3, new Vector3(-limitWidth, 0, -limitHeight));
    }

    public void ResetPheromones() => CreatePheromoneMatrix();
}
