using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace LandscaperAnts {

    public sealed class AntColonyGridTester : MonoBehaviour {

        [Header("ACO Settings")]

        [SerializeField, Range(1, 10000)] private int maxIterations = 1000; // The number of iterations on the main algorithm loop

        [SerializeField, Range(1, 10)] private int nAnts = 2;               // The amount of Ants

        [SerializeField, Range(0, 10)] private int alpha = 1;               // Pheromone influence factor (for pathfinding)
        [SerializeField, Range(1, 10)] private int beta = 1;                // Cost influence factor (for pathfinding)

        [SerializeField, Range(0f, 1f)] private float rho = 0.01f;          // Pheromone evaporation coefficient
        [SerializeField, Range(1, 5)] private int Q = 1;                    // Pheromone deposit coefficient

        [SerializeField, Range(1, 100)] private int r = 50;                 // The Moore neighbourdhood coefficient
        [SerializeField] private float heightIncr = 0.0001f;                // -> THIS SHOULD BE REPLACED BY PHEROMONE INFLUENCE

        [SerializeField, Range(0, 1)] private float maxSlope = 0.9f;        // The max slope an Ant can endure

        [SerializeField] private Terrain terrain;

        [SerializeField, Space] private Grid grid;                          // The collection of nodes and respective cost and pheromone matrices

        [Header("Display Settings")]

        [SerializeField] private TMP_Text itersUI;

        private Ant[] ants;                                                 // The Ants which will be pathtracing
        private int[] bestTrail;                                            // A collection of indices of the nodes that compose the (current) best trail

        private Coroutine aco;                                              // The coroutine for the ACO main loop (1 iteration per frame)

        // Method to generate a new graph
        public void GenerateGraph() {

            // Return if the algorithm is executing
            if (aco is not null)
                return;

            // Reset heightmap
            FlattenHeightmap();

            // Create graph and cost and pheromone matrices
            grid.Generate();
            print("----- Generated NEW Graph -----");
        }

        // Method to generate trails
        public void GenerateTrail() {

            if (aco is not null && grid.Nodes is null)
                return;

            grid.ResetMatrices();
            bestTrail = null;
            print("----- Started ACO -----");
            aco = StartCoroutine(Run());
        }

        // ACO main method
        private IEnumerator Run() {

            // Create the necessary ants
            ants = new Ant[nAnts];

            // The current number of iterations
            int iterations = 0;

            // Main algorithm loop
            while (iterations < maxIterations) {

                UpdateAnts();
                UpdateMatrices();

                iterations++;
                DisplayIterations(iterations);

                yield return null;
            }

            // Update the terrain's heightmap
            terrain.terrainData.SetHeights(0, 0, grid.Heights);

            aco = null;
        }

        // Update the Ants' status
        private void UpdateAnts() {

            // Loop through the number of Ants
            for (int i = 0; i < nAnts; i++) {

                // Dig a trail with each Ant
                DigTrail();
            }

            // Note: This is not using the Ants themselves, but SHOULD in the future
            // E.g.: Save the selected trail (not all ants should reproduce the same trail!)
            //       Save the trail of points made by the Ant (this might not be necessary, but the cost might(?)!)
            //       Ant trail cost should take into account: the number of points visited; the amount/average of height travelled; the ph levels encountered(?).
        }

        private void UpdateMatrices() {


        }

        private void DigTrail() {

            // ----- THE PLAN -----
            // Start Ant at origin
            // Get next point coordinates
            // Search neighbouring cells (Moore)
            // Give neighbours weights based on ph level and height difference
            // Also increases based on "closeness" to next point

            // Loop through all nodes (except the last one)
            for (int i = 0; i < grid.Nodes.Length - 1; i++) {

                // Get the point the Ant starts on
                Vector2Int currentPoint = grid.Nodes[i];

                // Get the point the Ant wishes to go to
                Vector2Int endPoint = grid.Nodes[i + 1];

                // Loop for a big arbitrary number of times
                // since we don't know how many points the Ant will visit
                for (int j = 0; j < grid.BaseDim * grid.BaseDim; j++) {

                    // Get the neighbours of the current point
                    Vector2Int[] neighbours = GetPointNeighbours(currentPoint);

                    // Variable to save the best distance found between a neighbour and the end point
                    float bestDistance = float.MaxValue;

                    // Loop through all found neighbours
                    for (int k = 0; k < neighbours.Length; k++) {

                        // Calculate the distance between a neighbour and the end point
                        float neighbourEndDistance = Vector2Int.Distance(neighbours[k], endPoint);

                        // Check if the distance is better than any previously found
                        if (neighbourEndDistance < bestDistance) {

                            // Update the current point and the best distance found so far
                            currentPoint = neighbours[k];
                            bestDistance = neighbourEndDistance;
                        }
                    }

                    // Decrement a manual value from the heightmap at the best neighbour
                    grid.Heights[currentPoint.y, currentPoint.x] -= heightIncr;

                    // Leave the for loop if the current point is the end point
                    if (currentPoint == endPoint)
                        break;
                }
            }
        }

        // Search for a point's neighbours using Moore's neighbourhood algorithm
        private Vector2Int[] GetPointNeighbours(Vector2Int p) {

            // The list of neighbours to find
            List<Vector2Int> neighbours = new();

            // Search for all neighbouring cells (Moore neighbourhood)
            for (int dx = -r; dx <= r; dx++) {

                for (int dy = -r; dy <= r; dy++) {

                    // Skip the cell if the coordinates match the extracted cell
                    if (dx == 0 && dy == 0)
                        continue;

                    // Get neighbour coordinates
                    int nx = p.x + dx,
                        ny = p.y + dy;

                    // Skip neighbour if coordinates are outside of the available 2D space
                    if (nx < 0 || ny < 0 || nx >= grid.BaseDim || ny >= grid.BaseDim)
                        continue;

                    // Check if Manhattan distance is within range
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > r)
                        continue;

                    // If previous conditions fail, add the point to the list
                    neighbours.Add(new Vector2Int(nx, ny));
                }
            }

            // Convert the list to an array and return it
            return neighbours.ToArray();
        }

        // Resets the heightmap
        public void FlattenHeightmap() {

            int texelSize = grid.BaseDim;

            // Apply the heightmap resolution on the terrain
            terrain.terrainData.heightmapResolution = texelSize;

            // Create the heightmap
            float[,] heights = new float[texelSize, texelSize];

            // Loop through all heightmap locations
            for (int i = 0; i < texelSize; i++) {

                for (int j = 0; j < texelSize; j++) {

                    heights[i, j] = 1.0f;
                }
            }

            // Apply the flattened heightmap on the terrain
            terrain.terrainData.SetHeights(0, 0, heights);
        }

        private void DisplayIterations(int iter) =>
            itersUI.text = $"{iter}";

        // Flatten the heightmap when quitting app
        private void OnApplicationQuit() => FlattenHeightmap();
    }
}
