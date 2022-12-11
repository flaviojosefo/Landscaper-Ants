using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class AntColonyTerrain : AntColonyOptimization {

    [Header("Display Settings")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private TMP_Text itersUI;

    public override void GenerateGraph() {

        // Return if the algorithm is executing
        if (aco is not null)
            return;

        base.GenerateGraph();
    }

    public override void GenerateTrail() {

        if (aco is not null && graph.Nodes is null)
            return;

        base.GenerateTrail();
    }

    protected override void GetBestTrail() {
        base.GetBestTrail();
    }

    protected override void DisplayBestTrail() {

        // Define the number of points the line will have
        string best = "NEW Best Trial: ";

        // Add each node's position to each available position on the line
        for (int i = 0; i < bestTrail.Length; i++) {

            best += $"{bestTrail[i]}" + (i + 1 == bestTrail.Length ? "" : "->");
        }

        // Update the highlighted node
        graph.UpdateHighlightedNode(bestTrail[0]);

        // Print a message on the console displaying the best trail and its cost
        best += $" | Cost: {GetTrailCost(bestTrail)}";
        print(best);
    }

    protected override void DisplayIterations(int iter) =>
            itersUI.text = $"{iter}";
}
