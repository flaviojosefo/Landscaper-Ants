using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Generator;
using TMPro;
using Random = UnityEngine.Random;

namespace LandscaperAnts {

    public sealed class AntColonyGridTester : MonoBehaviour {

        [Header("ACO Settings")]

        [SerializeField] private bool antsInPlace = true;                   // Should Ants be able to select the next cell as the one they're on?
        [SerializeField] private bool shuffleAnts = false;  // Should the Ants be shuffled when iterated?

        [SerializeField, Range(1, 10000)] private int maxIterations = 1000; // The number of iterations on the main algorithm loop

        [SerializeField, Range(1, 50)] private int nAnts = 2;               // The amount of Ants

        [SerializeField, Range(1, 10000)] private float antSteps = 1;       // The number of steps an Ant can perform

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

        private TestAnt[] ants;                                             // The Ants which will be pathtracing

        private Coroutine aco;                                              // The coroutine for the ACO main loop (1 iteration per frame)

        private void Start() {

            // ----- TEST PERCENTAGE SPREADING OF NEIGHBOURS -----

            // Main Variables

            Vector2Int origin = new(7, 4);      // The point the Ant is standing on
            Vector2Int destination = new(2, 4); // The Ant's destination

            //float distanceGapInfluence = 3;     // The factor which increases the gap between distance percentages (Higher values == closer points have a higher percentage!)

            //float distanceInfluence = 1;        // The factor that increases distance influence in the overall percentage calculation
            float pheromoneInfluence = 1;       // The factor that increases pheromone influence in the overall percentage calculation
            float slopeInfluence = 1;           // The factor that increases slope influence in the overall percentage calculation

            // ORDER OF IMPORTANCE: Slope --> Pheromones --> Distance (?)

            // Generate a new grid
            grid.Generate();

            // Get the origin's neighbouring points
            Vector2Int[] neighbours = GetMooreNeighbours(origin, true);

            // The neighbours' heights
            float[] nHeights = { 1f,  // ORIGIN
                                 1f,  // F
                                 1f,  // D
                                 1f,  // A
                                 1f,  // G
                                 1f,  // B
                                 1f,  // H
                                 1f,  // E
                                 1f   // C
            };

            // The neighbours' pheromone level
            float[] nPheromones = { 0.00f,  // ORIGIN
                                    0.00f,  // F
                                    0.00f,  // D
                                    0.00f,  // A
                                    0.00f,  // G
                                    0.00f,  // B
                                    0.00f,  // H
                                    0.00f,  // E
                                    0.00f   // C
            };

            // Fill Pheromone and height levels at the neighbours to simulate a "realtime scenario"
            for (int i = 0; i < neighbours.Length; i++) {

                Vector2Int n = neighbours[i];

                grid.Pheromones[n.y, n.x] = nPheromones[i];
                grid.Heights[n.y, n.x] = nHeights[i];
            }

            // Denominator Calculation

            float denominator = 0;

            for (int i = 0; i < neighbours.Length; i++) {

                Vector2Int n = neighbours[i];

                //float distance = Vector2Int.Distance(n, destination);

                //float distancePortion = Mathf.Pow(1 / Mathf.Pow(distance, distanceGapInfluence), distanceInfluence);

                float pheromonePortion = CalcPheromonePercentage(grid.Pheromones[n.y, n.x], pheromoneInfluence);

                float slopePortion = CalcSlopePercentage(grid.Heights[origin.y, origin.x], grid.Heights[n.y, n.x], slopeInfluence);

                denominator +=
                    //distancePortion + 
                    pheromonePortion +  
                    slopePortion;

            }

            // Nominator Calculation

            float totalPercentage = 0;

            (int index, float percentage)[] nPerctgs = new (int, float)[neighbours.Length];

            for (int i = 0; i < neighbours.Length; i++) {

                Vector2Int n = neighbours[i];

                float distance = Vector2Int.Distance(n, destination);

                //float distancePortion = Mathf.Pow(1 / Mathf.Pow(distance, distanceGapInfluence), distanceInfluence);

                float pheromonePortion = CalcPheromonePercentage(grid.Pheromones[n.y, n.x], pheromoneInfluence);

                float slopePortion = CalcSlopePercentage(grid.Heights[origin.y, origin.x], grid.Heights[n.y, n.x], slopeInfluence);

                float percentage = (
                    //distancePortion + 
                    pheromonePortion + 
                    slopePortion
                    ) 
                    / denominator;

                totalPercentage += percentage;

                nPerctgs[i] = (i, percentage);

                print($"{n} | Distance: {distance} | PH: {pheromonePortion} | Percentage: {percentage}");
            }

            Vector2Int nextCell = ChooseRandom(neighbours, nPerctgs);

            print(nextCell);

            print($"Total: {denominator} | Total Percentage: {totalPercentage}");
        }

        private Vector2Int GetNextPoint(Vector2Int origin, Vector2Int[] neighbours) {

            // Denominator Calculation

            float denominator = 0;

            for (int i = 0; i < neighbours.Length; i++) {

                Vector2Int n = neighbours[i];

                float pheromonePortion = CalcPheromonePercentage(grid.Pheromones[n.y, n.x], 1);

                float slopePortion = CalcSlopePercentage(grid.Heights[origin.y, origin.x], grid.Heights[n.y, n.x], 1);

                denominator += pheromonePortion + slopePortion;

            }

            // Nominator Calculation

            float totalPercentage = 0;

            (int index, float percentage)[] nPerctgs = new (int, float)[neighbours.Length];

            for (int i = 0; i < neighbours.Length; i++) {

                Vector2Int n = neighbours[i];

                float pheromonePortion = CalcPheromonePercentage(grid.Pheromones[n.y, n.x], 1);

                float slopePortion = CalcSlopePercentage(grid.Heights[origin.y, origin.x], grid.Heights[n.y, n.x], 1);

                float percentage = (pheromonePortion + slopePortion) / denominator;

                totalPercentage += percentage;

                nPerctgs[i] = (i, percentage);
            }

            Vector2Int nextCell = ChooseRandom(neighbours, nPerctgs);

            return nextCell;
        }

        private float CalcPheromonePercentage(float ph, float influence) {

            return 1 + Mathf.Pow(ph, influence);
        }

        private T ChooseRandom<T>(T[] collection, (int index, float percentage)[] probabilities) {

            // ##### ROULETTE WHEEL #####

            probabilities = probabilities.OrderBy(x => x.percentage).ToArray();

            // Calculate cumulative sum
            float[] cumulSum = new float[probabilities.Length + 1];

            for (int i = 0; i < probabilities.Length; i++) {

                cumulSum[i + 1] = cumulSum[i] + probabilities[i].percentage;
            }

            // Get random value between 0 and 1 (both inclusive)
            float rnd = Random.value;

            for (int i = 0; i < cumulSum.Length - 1; i++) {

                if ((rnd >= cumulSum[i]) && (rnd < cumulSum[i + 1])) {

                    return collection[probabilities[i].index];
                }
            }

            // Choose the last cell if cumulative sum didn't achieve 1
            return collection[probabilities[^1].index];
        }

        private float CalcSlopePercentage(float from, float to, float influence) {

            return Mathf.Pow(Mathf.Abs(1 + to - from), influence);
        }

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

            if (aco is not null && grid.FoodCells is null)
                return;

            grid.ResetMatrices();
            print("----- Started ACO -----");
            aco = StartCoroutine(Run());
        }

        // ACO main method
        private IEnumerator Run() {

            InitAnts();

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

        // Initiate Ant state
        private void InitAnts() {

            // The starting position for all ants
            int xStart = Random.Range(0, grid.BaseDim);
            int yStart = Random.Range(0, grid.BaseDim);

            Vector2Int start = new(xStart, yStart);

            // Create the necessary ants
            ants = new TestAnt[nAnts];

            for (int i = 0; i < nAnts; i++) {

                ants[i] = new TestAnt(start);
            }

            print($"Ants start at {start}");
        }

        // Update the Ants' status
        private void UpdateAnts() {

            // Loop through the amount of cells an ant is allowed to visit
            for (int i = 0; i < antSteps; i++) {

                if (shuffleAnts)
                    ants.Shuffle();

                // Loop through the number of ants
                for (int j = 0; j < nAnts; j++) {

                    // Get the current ant's cell
                    Vector2Int current = ants[j].CurrentCell;

                    // Get the (moore) neighbours of the current cell (can include itself)
                    Vector2Int[] neighbours = GetMooreNeighbours(current, antsInPlace);

                    // Check if the ant found a food source
                    if (FoundFood(neighbours)) {

                        //// Start travelling back!
                        //print($"Ant {j} found some food!");

                        //// Decrement a manual value from the heightmap at the select cell
                        //grid.Heights[current.y, current.x] -= heightIncr;

                        // Get the next cell based on height and pheromone levels
                        Vector2Int next = GetNextPoint(current, neighbours);

                        // Decrement a manual value from the heightmap at the select cell
                        grid.Heights[next.y, next.x] -= heightIncr;

                        // Update the ant's current cell
                        ants[j].CurrentCell = next;

                    } else {

                        // Get the next cell based on height and pheromone levels
                        Vector2Int next = GetNextPoint(current, neighbours);

                        // Decrement a manual value from the heightmap at the select cell
                        grid.Heights[next.y, next.x] -= heightIncr;

                        // Update the ant's current cell
                        ants[j].CurrentCell = next;
                    }
                }
            }
        }

        private void UpdateMatrices() {


        }

        // Main algorithm for path tracing
        private void DigTrail() {

            
        }

        // Checks if the Ant is next to a food source
        private bool FoundFood(Vector2Int[] neighbours) {

            // Returns true if one of the neighbouring cells is a food source
            for (int i = 0; i < grid.FoodCells.Length; i++) {

                for (int j = 0; j < neighbours.Length; j++) {

                    if (grid.FoodCells[i] == neighbours[j]) {

                        return true;
                    }
                }
            }

            // Returns false if no neighbour is a food source
            return false;
        }

        // Search for a point's neighbours using Moore's neighbourhood algorithm
        private Vector2Int[] GetMooreNeighbours(Vector2Int p, bool includeOrigin = false) {

            // The list of neighbours to find
            List<Vector2Int> neighbours = new();

            // Add the origin as a neighbour if we want to include it
            if (includeOrigin)
                neighbours.Add(p);

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
