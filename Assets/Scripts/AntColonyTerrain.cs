using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public sealed class AntColonyTerrain : AntColonyOptimization {

    [Header("Display Settings")]
    [SerializeField] private int texelSize = 513;
    [SerializeField] private float tileSize = 10f;
    [SerializeField] private Terrain terrain;
    [SerializeField] private TMP_Text itersUI;
    [SerializeField] private LineRenderer trailLine; // For testing, remove when unnecessary
    [SerializeField] private RectTransform phMatrix; // For testing, remove when unnecessary

    [SerializeField, Range(1, 100)] private int r = 50;               // The Moore neighbourdhood coefficient
    [SerializeField] private float heightIncr = 0.0001f;              // -> THIS SHOULD BE REPLACED BY PHEROMONE INFLUENCE
    [SerializeField, Range(.1f, 10f)] private float sigmaBlur = 1.0f; // The amount of blur to be applied on the heightmap

    [SerializeField] private bool oneAnt = false; // Dig with 1 vs all ants

    private float[,] heights;  // The heightmap to be applied on the terrain

    // Called whenever a new graph is created
    public override void GenerateGraph() {

        // Return if the algorithm is executing
        if (aco is not null)
            return;

        trailLine.positionCount = 0;

        FlattenHeightmap();

        base.GenerateGraph();
    }

    public override void GenerateTrail() {

        if (aco is not null && graph.Nodes is null)
            return;

        trailLine.positionCount = 0;

        base.GenerateTrail();
    }

    protected override void GetBestTrail() {

        // Best trail -> Less climbing, more pheromones (in that order)

        base.GetBestTrail();

        // Update Height Map
        UpdateHeightMap();
    }

    protected override void DisplayBestTrail() {

        // Define the number of points the line will have
        trailLine.positionCount = bestTrail.Length;
        string best = "NEW Best Trail: ";

        // Add each node's position to each available position on the line
        for (int i = 0; i < bestTrail.Length; i++) {

            best += $"{bestTrail[i]}" + (i + 1 == bestTrail.Length ? "" : "->");
            trailLine.SetPosition(i, graph.Nodes[bestTrail[i]]);
        }

        // Update the highlighted nodes
        graph.UpdateStartHighlight(bestTrail[0]);
        graph.UpdateEndHighlight(bestTrail[^1]);

        // Print a message on the console displaying the best trail and its cost
        best += $" | Cost: {GetTrailCost(bestTrail)}";
        print(best);
    }

    protected override void DisplayIterations(int iter) {

        // Update the iteration number on the display
        itersUI.text = $"{iter}";
    }

    // Update heightmap (futurely based on diffusion and evaporation)
    public void UpdateHeightMap() {

        // Check wether or not to use only the best performing Ant
        if (oneAnt) {

            // Select an ant to dig a path
            Ant ant = SelectAnt();

            // Get the nodes locations in texel coordinates
            Vector3[] nodes = FindTexelNodeLocations(ant.Trail);

            ProcessTexels(nodes);

        } else {

            // Loop through all ants
            for (int i = 0; i < ants.Length; i++) {

                // Get the nodes locations in texel coordinates
                Vector3[] nodes = FindTexelNodeLocations(ants[i].Trail);

                ProcessTexels(nodes);
            }
        }

        // Apply the new heightmap on the terrain
        terrain.terrainData.SetHeights(0, 0, heights);
    }

    // Choose an ant based on the inverse of the trail cost
    // Smaller trails = Bigger probabilities
    private Ant SelectAnt() {

        // Calculate denominator sum
        float sum = 0;

        for (int i = 0; i < ants.Length; i++) {

            sum += 1 / ants[i].TrailCost;
        }

        // Create a tuple containing an ant's index and probability of being chosen
        (int antIndex, float percentage)[] probs = new (int, float)[ants.Length];

        // Calculate each of the path's percentage
        for (int i = 0; i < ants.Length; i++) {

            probs[i] = (i, (1 / ants[i].TrailCost) / sum);
        }

        // Reorganize the tuple based on ascending percentage
        probs = probs.OrderBy(x => x.percentage).ToArray();

        // ##### ROULETTE WHEEL #####

        // Calculate cumulative sum
        float[] cuSum = new float[probs.Length + 1];

        for (int i = 0; i < probs.Length; i++) {

            cuSum[i + 1] = cuSum[i] + probs[i].percentage;
        }

        // Get random value between 0 and 1 (both inclusive)
        float rnd = Random.value;

        for (int i = 0; i < cuSum.Length - 1; i++) {

            if ((rnd >= cuSum[i]) && (rnd < cuSum[i + 1])) {

                return ants[probs[i].antIndex];
            }
        }

        // If the cumulative sum goes above 1 (due to float errors)
        // return the best performing ant
        return ants.OrderBy(a => a.TrailCost).First();
    }

    private void ProcessTexels(Vector3[] nodes) {

        // Structure to place all cells found between all node paths
        Queue<Vector2Int> cells = new();

        // 1D array to keep track of already encountered cells
        // This prevents "main" cells from being processed as neighbours
        // It also keeps track of similar neighbours of different "main" cells
        bool[] processedCells = new bool[texelSize * texelSize];

        // Loop through the nodes' locations
        for (int j = 0; j < nodes.Length - 1; j++) {

            // Get a line of cells in the heightmap between 2 nodes
            BresenhamLine(nodes[j], nodes[j + 1], (x, y) => {

                // THIS CODE PROCESSES EACH "MAIN" CELL 
                // FOUND BETWEEN THE 2 NODES (INCLUSIVE)

                // Place the found cell into the queue
                cells.Enqueue(new(x, y));

                // Decrease the cell's height
                heights[y, x] -= heightIncr;

                // Mark the cell as processed
                processedCells[y + x * texelSize] = true;
            });
        }

        // Loop through all found cells
        while (cells.Count > 0) {

            // Extract the cell from the queue
            Vector2Int cell = cells.Dequeue();

            // Search for all neighbouring cells (Moore neighbourhood)
            for (int dx = -r; dx <= r; dx++) {

                for (int dy = -r; dy <= r; dy++) {

                    // Skip the cell if the coordinates match the extracted cell
                    if (dx == 0 && dy == 0)
                        continue;

                    // Get neighbour coordinates
                    int nx = cell.x + dx,
                        ny = cell.y + dy;

                    // Skip neighbour if coordinates are outside of the available 2D space
                    if (nx < 0 || ny < 0 || nx >= texelSize || ny >= texelSize)
                        continue;

                    // Skip if neighbour was already processed
                    if (processedCells[ny + nx * texelSize])
                        continue;

                    // Check if Manhattan distance is within range
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > r)
                        continue;

                    // Decrease the neighbouring cell's height
                    heights[ny, nx] -= heightIncr;

                    // Mark the neighbour as processed
                    processedCells[ny + nx * texelSize] = true;
                }
            }
        }
    }

    // Resets the heightmap
    public void FlattenHeightmap() {

        // Apply the heightmap resolution on the terrain
        terrain.terrainData.heightmapResolution = texelSize;

        // Create the heightmap
        heights = new float[texelSize, texelSize];

        // Loop through all heightmap locations
        for (int i = 0; i < texelSize; i++) {

            for (int j = 0; j < texelSize; j++) {

                heights[i, j] = 1.0f;
            }
        }

        // Apply the flattened heightmap on the terrain
        terrain.terrainData.SetHeights(0, 0, heights);
    }

    // Applies a gaussian blur to the heightmap
    public void BlurHeightmap() {

        // Return if ACO is running
        if (aco is not null)
            return;

        // Create a blurred version of the regular heightmap
        float[,] blurHeights = GaussianBlur(heights, sigmaBlur);

        // Apply the blurred heightmap on the terrain
        terrain.terrainData.SetHeights(0, 0, blurHeights);
    }

    // Resets the heightmap to original values
    public void DeblurHeightmap() {

        // Return if ACO is running
        if (aco is not null)
            return;

        // Apply the regular heightmap on the terrain
        terrain.terrainData.SetHeights(0, 0, heights);
    }

    // Applies a Gaussian Blur to a 2D grid
    private float[,] GaussianBlur(float[,] input, float sigma) {

        // Create the output map
        float[,] output = new float[texelSize, texelSize];

        // Create the kernel for the blur
        int kernelSize = (int)(sigma * 3);
        float[,] kernel = new float[kernelSize, kernelSize];
        float kernelSum = 0;

        // Loop through the kernel
        for (int x = 0; x < kernelSize; x++) {

            for (int y = 0; y < kernelSize; y++) {

                // Calculate the kernel weight based on
                // the standard deviation of the blur
                float xDistance = x - kernelSize / 2;
                float yDistance = y - kernelSize / 2;
                float weight = (float)(Math.Exp(-(xDistance * xDistance + yDistance * yDistance) / (2 * sigma * sigma)) / (2 * Math.PI * sigma * sigma));
                kernel[x, y] = weight;
                kernelSum += weight;
            }
        }

        // Loop through the kernel
        for (int x = 0; x < kernelSize; x++) {

            for (int y = 0; y < kernelSize; y++) {

                // Normalize the kernel
                kernel[x, y] /= kernelSum;
            }
        }

        // Apply the blur to the input map
        for (int x = 0; x < texelSize; x++) {

            for (int y = 0; y < texelSize; y++) {

                // The sum for this pixel
                float sum = 0;

                // Loop through the kernel
                for (int kX = 0; kX < kernelSize; kX++) {

                    for (int kY = 0; kY < kernelSize; kY++) {

                        // Calculate the input heightmap's coordinates
                        int sampleX = x + kX - kernelSize / 2;
                        int sampleY = y + kY - kernelSize / 2;

                        // Check if the coordinates are within range
                        if (sampleX >= 0 && sampleX < texelSize && sampleY >= 0 && sampleY < texelSize) {

                            // Multiply the input value by the kernel's weight
                            // and add it to the sum
                            sum += input[sampleX, sampleY] * kernel[kX, kY];
                        }
                    }
                }

                // Apply the sum to the output
                output[x, y] = sum;
            }
        }

        // Return the output heightmap
        return output;
    }

    private Vector3[] FindTexelNodeLocations(int[] trail) {

        Vector3[] texelNodes = new Vector3[trail.Length];

        for (int i = 0; i < texelNodes.Length; i++) {

            Vector3 texelPos = VectorToTexel(graph.Nodes[trail[i]]);
            texelNodes[i] = texelPos;
        }

        return texelNodes;
    }

    private Vector3 VectorToTexel(Vector3 v) {

        // The graph's limit (-dim to dim)
        float dim = graph.BaseDim;

        Vector3 texelPos = new() {
            x = ((v.x + dim) / (dim + dim)) * texelSize,
            y = ((v.z + dim) / (dim + dim)) * texelSize,
            z = v.y
        };

        //print($"Vector: {v} | Texel: {texelPos}");

        return texelPos;
    }

    private void BresenhamLine(Vector3 from, Vector3 to, Action<int, int> action) {

        int x0 = (int)from.x,
            x1 = (int)to.x;

        int y0 = (int)from.y,
            y1 = (int)to.y;

        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true) {

            // Call the lambda expression with the current point
            action(x0, y0);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;

            if (e2 > -dy) {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx) {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void OnApplicationQuit() {

        // Flatten the heightmap when quitting app
        FlattenHeightmap();
    }
}
