using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public abstract class AntColonyOptimization : MonoBehaviour {

    [Header("ACO Settings")]
    [SerializeField] protected bool randomStart = true;
    [SerializeField] protected bool randomEnd = true;
    [SerializeField, Range(1, 10000)] protected int maxIterations = 1000;
    [SerializeField, Range(1, 10)] protected int nAnts = 2;
    [SerializeField, Range(0, 10)] protected int alpha = 1;
    [SerializeField, Range(1, 10)] protected int beta = 1;
    [SerializeField, Range(0f, 1f)] protected float rho = 0.01f;
    [SerializeField, Range(1, 5)] protected int Q = 1;

    [SerializeField, Space] protected Graph graph;

    protected Ant[] ants;
    protected int[] bestTrail;

    protected Coroutine aco;

    // Generate a graph on start
    protected void Start() => GenerateGraph();

    // Method to generate a new graph
    public virtual void GenerateGraph() {

        // Create graph and cost and pheromone matrices
        graph.Generate();
        print("----- Generated NEW Graph -----");
    }

    // Method to generate trails
    public virtual void GenerateTrail() {

        graph.ResetPheromones();
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
        while(iterations < maxIterations) {

            UpdateAnts();
            UpdatePheromones();

            GetBestTrail();

            iterations++;
            DisplayIterations(iterations);

            yield return null;
        }

        aco = null;
    }

    // Build all ants' trails and calculate their cost
    private void UpdateAnts() {

        for (int i = 0; i < ants.Length; i++) {

            int start = randomStart ? Random.Range(0, graph.Nodes.Length) : 0;
            int? end = randomEnd ? null : graph.Nodes.Length - 1;

            ants[i].Trail = BuildTrail(start, end);
            ants[i].TrailCost = GetTrailCost(ants[i].Trail);

            //string t = "Trail: ";
            //foreach(int j in ants[i].Trail) {
            //    t += $"{j} ";
            //}
            //print(t);
            //print("Trail Cost: " + ants[i].TrailCost);
        }
    }

    // Update pheromone values on all edges based on ants' trails and evaporation
    private void UpdatePheromones() {

        int nAmount = graph.Nodes.Length;

        for (int i = 0; i < nAmount; i++) {

            for (int j = i + 1; j < nAmount; j++) {

                for (int k = 0; k < ants.Length; k++) {

                    float evaporated = (1 - rho) * graph.PheromoneMatrix[i, j];

                    float deposited = IsEdgeOnTrail(i, j, ants[k]) ? Q / ants[k].TrailCost : 0;
                    //print("To be deposited: " + deposited);

                    float updatedPh = Mathf.Clamp(evaporated + deposited, 0.0001f, 100000f);

                    graph.PheromoneMatrix[i, j] = graph.PheromoneMatrix[j, i] = updatedPh;
                }
            }
        }
    }

    // Builds a trail, travelling through all connected nodes
    private int[] BuildTrail(int start, int? end) {

        int nodesAmount = graph.Nodes.Length;
        int nodesLoop = nodesAmount - 1;

        int[] trail = new int[nodesAmount];
        bool[] visitedNodes = new bool[nodesAmount];

        trail[0] = start;
        visitedNodes[start] = true;

        if (end is not null) {
            trail[nodesAmount - 1] = (int)end;
            visitedNodes[(int)end] = true;
            nodesLoop--;
        }

        for (int i = 0; i < nodesLoop; i++) {

            int currNode = trail[i];
            //print("Current Node: " + currNode);

            List<(int, float)> probs = GetNeighboursProbs(currNode, visitedNodes);

            int nextNode = GetNextNode(probs);

            //print("Next Node: " + nextNode);

            trail[i + 1] = nextNode;
            visitedNodes[nextNode] = true;
        }

        return trail;
    }

    // Gets the cost of a given trail
    protected virtual float GetTrailCost(int[] trail) {

        float trailCost = 0;

        for (int i = 0; i < trail.Length - 1; i++) {

            trailCost += graph.CostMatrix[trail[i], trail[i + 1]];
        }

        return trailCost;
    }

    // Returns true if 2 nodes are part of an ant's trail
    private bool IsEdgeOnTrail(int fromNode, int toNode, Ant ant) {

        for (int i = 0; i < ant.Trail.Length - 1; i++) {

            if ((ant.Trail[i] == fromNode) && (ant.Trail[i + 1] == toNode) ||
                (ant.Trail[i] == toNode) && (ant.Trail[i + 1] == fromNode)) {

                //print($"Valid edge on matrix pos: [{fromNode},{toNode}]");
                return true;
            }
        }

        return false;
    }

    // Gets the probability to visit all of the node's neighbours
    private List<(int, float)> GetNeighboursProbs(int currNode, bool[] visitedNodes) {

        int[] neighbours = graph.GetNeighbours(currNode);

        float denomSum = 0;

        // Calculate probability denominator
        for (int i = 0; i < neighbours.Length; i++) {

            int neighbour = neighbours[i];

            if (!visitedNodes[neighbour]) {

                float tau = graph.PheromoneMatrix[currNode, neighbour];
                float n = 1 / graph.CostMatrix[currNode, neighbour];

                denomSum += Mathf.Pow(tau, alpha) * Mathf.Pow(n, beta);
            }
        }

        // Save node and corresponding percentage
        List<(int, float)> probs = new();

        // Calculate probability nominator
        for (int i = 0; i < neighbours.Length; i++) {

            int neighbour = neighbours[i];

            if (!visitedNodes[neighbour]) {

                float tau = graph.PheromoneMatrix[currNode, neighbour];
                float n = 1 / graph.CostMatrix[currNode, neighbour];

                probs.Add((neighbour, Mathf.Pow(tau, alpha) * Mathf.Pow(n, beta) / denomSum));
            }
        }

        // Return indices and corresponding probabilities
        return probs;
    }

    // Gets the chosen (next) node, based on a 'roulette wheel'
    private int GetNextNode(List<(int node, float perct)> probs) {

        // Order percentages in ascending values
        probs = probs.OrderBy(x => x.perct).ToList();

        //string probsString = "Probabilities: ";
        //foreach((int index, float prob) in probs) {
        //    probsString += $"[{index}: {prob}], ";
        //}
        //print(probsString);

        // Calculate cumulative sum
        float[] cuSum = new float[probs.Count + 1];

        for (int i = 0; i < probs.Count; i++) {

            cuSum[i + 1] = cuSum[i] + probs[i].perct;
        }

        //string cuSumString = "CuSums: ";
        //foreach (float c in cuSum) {
        //    cuSumString += $"{c}, ";
        //}
        //print(cuSumString);

        float rnd = Random.Range(0f, 1f);
        //print("Random: " + rnd);

        // Choose next node based on the given random
        for (int i = 0; i < cuSum.Length - 1; i++) {

            if ((rnd >= cuSum[i]) && (rnd < cuSum[i + 1])) {

                return probs[i].node;
            }
        }

        // Should only show if (cumul[cumul.Length-1] > 1)
        Debug.LogWarning("Cumulative Sum went over 100%!");

        // If so, return a random node
        return probs[Random.Range(0, probs.Count)].node;
    }

    // Get the best trail from all the ants
    protected virtual void GetBestTrail() {

        //for (int i = 0; i < ants.Length; i++) {

        //    string trail = "";

        //    for (int j = 0; j < ants[i].Trail.Length; j++) {

        //        trail += $"{ants[i].Trail[j]}" + (j + 1 == ants[i].Trail.Length ? "" : "->");
        //    }

        //    print($"Ant_{i}: {ants[i].TrailCost} | [{trail}]");
        //}

        // Get the ant with the lowest trail cost (distance)
        Ant bestAnt = ants.OrderBy(a => a.TrailCost).First();

        // Update the best trail if the best ant has a lower cost
        if ((bestTrail is null) || 
            (bestAnt.TrailCost < GetTrailCost(bestTrail))) {

            bestTrail = bestAnt.Trail;

            DisplayBestTrail();
        }
    }

    // Displays the best trail as a line renderer
    protected abstract void DisplayBestTrail();

    // Displays the current iteration count
    protected abstract void DisplayIterations(int iter);
}
