using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class AntColonyTerrain : AntColonyOptimization {

    [Header("Display Settings")]
    [SerializeField] private int texelSize = 513;
    [SerializeField] private float tileSize = 10f;
    [SerializeField] private Terrain terrain;
    [SerializeField] private TMP_Text itersUI;
    [SerializeField] private LineRenderer trailLine; // For testing, remove when unnecessary
    [SerializeField] private RectTransform phMatrix;

    [SerializeField, Range(1, 100)] private int r = 50;
    [SerializeField] private float heightIncr = 0.0001f;

    private float[,] heights;

    // Called whenever a new graph e created
    public override void GenerateGraph() {

        // Return if the algorithm is executing
        if (aco is not null)
            return;

        trailLine.positionCount = 0;

        terrain.terrainData.heightmapResolution = texelSize;

        heights = new float[texelSize, texelSize];

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
        string best = "NEW Best Trial: ";

        // Add each node's position to each available position on the line
        for (int i = 0; i < bestTrail.Length; i++) {

            best += $"{bestTrail[i]}" + (i + 1 == bestTrail.Length ? "" : "->");
            trailLine.SetPosition(i, graph.Nodes[bestTrail[i]]);
        }

        // Update the highlighted node
        graph.UpdateHighlightedNode(bestTrail[0]);

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

        // Get the nodes locations in texel coordinates
        Vector3[] nodes = FindTexelNodeLocations(bestTrail);

        // Structure to place all cells found between all node paths
        Queue<Vector2Int> cells = new();

        // 1D array to keep track of already encountered cells
        // This prevents "main" cells from being processed as neighbours
        // It also keeps track of similar neighbours of different "main" cells
        bool[] processedCells = new bool[texelSize * texelSize];

        // Loop through the nodes' locations
        for (int i = 0; i < nodes.Length - 1; i++) {

            // Get a line of cells in the heightmap between 2 nodes
            BresenhamLine(nodes[i], nodes[i + 1], (x, y) => {

                // THIS CODE PROCESSES EACH "MAIN" CELL 
                // FOUND BETWEEN THE 2 NODES (INCLUSIVE)

                // Place the found cell into the queue
                cells.Enqueue(new(x, y));

                // Increase the cell's height
                heights[y, x] += heightIncr;

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

                    // Check if Manhattan distance is within range
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > r)
                        continue;

                    // Skip if neighbour was already processed
                    if (processedCells[ny + nx * texelSize])
                        continue;

                    // Increase the neighbouring cell's height
                    heights[ny, nx] += heightIncr;

                    // Mark the neighbour as processed
                    processedCells[ny + nx * texelSize] = true;
                }
            }
        }

        // Apply the new heightmap on the terrain
        terrain.terrainData.SetHeights(0, 0, heights);
    }

    public void FindTexelNodesPath() {

        terrain.terrainData.heightmapResolution = texelSize;

        heights = new float[texelSize, texelSize];

        Vector3[] nodes = FindTexelNodeLocations();

        for (int i = 0; i < nodes.Length - 1; i++) {

            PlotLine(nodes[i], nodes[i + 1]);
        }

        terrain.terrainData.SetHeights(0, 0, heights);
    }

    private Vector3[] FindTexelNodeLocations(int[] trail) {

        Vector3[] texelNodes = new Vector3[trail.Length];

        for (int i = 0; i < texelNodes.Length; i++) {

            Vector3 texelPos = VectorToTexel(graph.Nodes[trail[i]]);
            texelNodes[i] = texelPos;
        }

        return texelNodes;
    }

    private Vector3[] FindTexelNodeLocations() {

        Vector3[] texelNodes = new Vector3[graph.Nodes.Length];

        for (int i = 0; i < texelNodes.Length; i++) {

            Vector3 texelPos = VectorToTexel(graph.Nodes[i]);
            texelNodes[i] = texelPos;

            // x and y are swapped in texel coordinates
            //heights[(int)texelPos.y, (int)texelPos.x] = texelPos.z;
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

    private void PlotPoint(Vector3 v) {

        // x and y are swapped in texel coordinates
        heights[(int)v.y, (int)v.x] = 1;
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

    #region Bresenham's Line Algorithm

    public void PlotLine(Vector3 from, Vector3 to) {

        if (Math.Abs((int)to.y - (int)from.y) < Math.Abs((int)to.x - (int)from.x)) {

            if ((int)from.x > (int)to.x) {

                PlotLineLow(to, from);

            } else {

                PlotLineLow(from, to);
            }

        } else {

            if ((int)from.y > (int)to.y) {

                PlotLineHigh(to, from);

            } else {

                PlotLineHigh(from, to);
            }
        }
    }

    private void PlotLineLow(Vector3 from, Vector3 to) {

        int dx = (int)to.x - (int)from.x;
        int dy = (int)to.y - (int)from.y;

        int yi = 1;

        if (dy < 0) {

            yi = -1;
            dy = -dy;
        }

        int D = (2 * dy) - dx;
        int y = (int)from.y;

        for (int i = (int)from.x; i <= (int)to.x; i++) {

            PlotPoint(new Vector3(i, y));

            if (D > 0) {

                y += yi;
                D += 2 * (dy - dx);

            } else {

                D += 2 * dy;
            }
        }
    }

    private void PlotLineHigh(Vector3 from, Vector3 to) {

        int dx = (int)to.x - (int)from.x;
        int dy = (int)to.y - (int)from.y;

        int xi = 1;

        if (dx < 0) {

            xi = -1;
            dx = -dx;
        }

        int D = (2 * dx) - dy;
        int x = (int)from.x;

        for (int i = (int)from.y; i < (int)to.y; i++) {

            PlotPoint(new Vector3(x, i));

            if (D > 0) {

                x += xi;
                D += 2 * (dx - dy);

            } else {

                D += 2 * dx;
            }
        }
    }

    #endregion
}
