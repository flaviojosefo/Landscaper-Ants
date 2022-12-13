using System;
using TMPro;
using UnityEngine;

public sealed class AntColonyTerrain : AntColonyOptimization {

    [Header("Display Settings")]
    [SerializeField] private int texelSize = 513;
    [SerializeField] private float tileSize = 10f;
    [SerializeField] private Terrain terrain;
    [SerializeField] private TMP_Text itersUI;
    [SerializeField] private LineRenderer trailLine; // For testing, remove when unnecessary

    private float[,] heights;

    public override void GenerateGraph() {

        // Return if the algorithm is executing
        if (aco is not null)
            return;

        trailLine.positionCount = 0;

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

    protected override void DisplayIterations(int iter) =>
            itersUI.text = $"{iter}";

    public void FindTexelNodesPath() {

        terrain.terrainData.heightmapResolution = texelSize;

        heights = new float[texelSize, texelSize];

        Vector3[] nodes = FindTexelNodeLocations();

        for (int i = 0; i < nodes.Length - 1; i++) {

            PlotLine(nodes[i], nodes[i + 1]);
        }

        terrain.terrainData.SetHeights(0, 0, heights);
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
