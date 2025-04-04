using TMPro;
using UnityEngine;

namespace LegacyACO {

    public sealed class AntColonyNodes : AntColonyOptimization {

        [Header("Display Settings")]
        [SerializeField] private LineRenderer trailLine;
        [SerializeField] private TMP_Text itersUI;

        public override void GenerateGraph() {

            // Return if the algorithm is executing
            if (aco is not null)
                return;

            // Reset the trail's display line
            trailLine.positionCount = 0;

            base.GenerateGraph();
        }

        public override void GenerateTrail() {

            if (aco is not null && graph.Nodes is null)
                return;

            // Reset the trail's display line
            trailLine.positionCount = 0;

            base.GenerateTrail();
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

        protected override void DisplayIterations(int iter) =>
                itersUI.text = $"{iter}";
    }
}
