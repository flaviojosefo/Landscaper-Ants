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
        [SerializeField] private bool shuffleAnts = false;                  // Should the Ants be shuffled when iterated?

        [SerializeField, Range(1, 50)] private int nAnts = 2;               // The amount of Ants

        [SerializeField, Range(1, 50000)] private float maxSteps = 1000;    // The number of steps an Ant can perform

        [SerializeField, Range(1, 10)] private int alpha = 1;               // Pheromone influence factor (for pathfinding)
        [SerializeField, Range(1, 10)] private int beta = 1;                // Slope influence factor (for pathfinding)
        [SerializeField, Range(1, 10)] private int gamma = 1;               // Direction to origin influence factor (for pathfinding)

        [SerializeField, Range(0f, 1f)] private float rho = 0.01f;          // Pheromone evaporation coefficient
        [SerializeField, Range(1, 5)] private int Q = 1;                    // Pheromone deposit coefficient

        [SerializeField, Range(1, 100)] private int r = 1;                  // The Moore neighbourdhood coefficient
        [SerializeField] private float heightIncr = 0.0001f;                // The value that controls "heightmap digging"

        [SerializeField, Range(0, 1)] private float maxSlope = 0.9f;        // The max slope an Ant can endure

        [SerializeField] private Terrain terrain;

        [SerializeField, Space] private Grid grid;                          // The collection of nodes and respective cost and pheromone matrices

        [Header("Display Settings")]

        [SerializeField] private TMP_Text stepsUI;

        private TestAnt[] ants;                                             // The Ants which will be pathtracing

        private Coroutine aco;                                              // The coroutine for the ACO main loop (1 iteration per frame)

        private void Start() {

            // ----- TEST PERCENTAGE SPREADING OF NEIGHBOURS -----

            // Main Variables

            Vector2Int origin = new(7, 4);      // The point the Ant is standing on
            Vector2Int destination = new(2, 4); // The Ant's destination

            //float distanceGapInfluence = 3;     // The factor which increases the gap between distance percentages (Higher values == closer points have a higher percentage!)

            float directionInfluence = 1;       // The factor that increases direction influence in the overall percentage calculation
            float pheromoneInfluence = 1;       // The factor that increases pheromone influence in the overall percentage calculation
            float slopeInfluence = 1;           // The factor that increases slope influence in the overall percentage calculation

            // ORDER OF IMPORTANCE: Slope --> Pheromones --> Distance (?)

            // Generate a new grid
            grid.Generate();

            // Get the origin's neighbouring points
            Vector2Int[] neighbours = GetMooreNeighbours(origin, false);

            int neighboursAmount = neighbours.Length;

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
            for (int i = 0; i < neighboursAmount; i++) {

                Vector2Int n = neighbours[i];

                grid.Pheromones[n.y, n.x] = nPheromones[i];
                grid.Heights[n.y, n.x] = nHeights[i];
            }

            float[] pheromonePortions = new float[neighboursAmount];
            float[] slopePortions = new float[neighboursAmount];
            float[] directionPortions = new float[neighboursAmount];

            // Denominator Calculation

            Vector2Int mainDirection = destination - origin;
            //print(mainDirection);

            float denominator = 0;

            for (int i = 0; i < neighboursAmount; i++) {

                Vector2Int n = neighbours[i];

                // Calculate direction and angle with [origin to destination] vector
                Vector2Int direction = n - origin;
                float angle = Vector2.Angle(mainDirection, direction);

                directionPortions[i] = CalcDirectionPercentage(angle, directionInfluence);

                pheromonePortions[i] = CalcPheromonePercentage(grid.Pheromones[n.y, n.x], pheromoneInfluence);

                slopePortions[i] = CalcSlopePercentage(grid.Heights[origin.y, origin.x], grid.Heights[n.y, n.x], slopeInfluence);

                denominator +=
                    directionPortions[i]
                    //pheromonePortion +  
                    //slopePortion
                    ;

            }

            // Nominator Calculation

            float totalPercentage = 0;

            (int index, float percentage)[] nPerctgs = new (int, float)[neighboursAmount];

            for (int i = 0; i < neighboursAmount; i++) {

                float percentage = (
                    directionPortions[i] 
                    //pheromonePortions[i] + 
                    //slopePortions[i]
                    ) 
                    / denominator;

                totalPercentage += percentage;

                nPerctgs[i] = (i, percentage);

                print($"{neighbours[i]} | PH: {pheromonePortions[i]} | Percentage: {percentage}");
            }

            Vector2Int nextCell = ChooseRandom(neighbours, nPerctgs);

            print(nextCell);

            print($"Total: {denominator} | Total Percentage: {totalPercentage}");
        }

        private Vector2Int GetNextPoint(Vector2Int origin, Vector2Int destination, Vector2Int[] neighbours, bool hasFood) {

            int foodMultiplier = hasFood ? 1 : 0;
            //int otherFoodMult = hasFood ? 0 : 1;

            int neighboursAmount = neighbours.Length;

            float[] pheromonePortions = new float[neighboursAmount];
            float[] slopePortions = new float[neighboursAmount];
            float[] directionPortions = new float[neighboursAmount];

            Vector2Int mainDirection = destination - origin;

            // Calculate individual variable influences and save their sum

            float totalSum = 0;

            for (int i = 0; i < neighboursAmount; i++) {

                Vector2Int n = neighbours[i];
                
                // Calculate direction and angle with [origin to destination] vector
                Vector2Int direction = n - origin;
                float angle = Vector2.Angle(mainDirection, direction);

                pheromonePortions[i] = CalcPheromonePercentage(grid.Pheromones[n.y, n.x], alpha);

                slopePortions[i] = CalcSlopePercentage(grid.Heights[origin.y, origin.x], grid.Heights[n.y, n.x], beta);

                directionPortions[i] = CalcDirectionPercentage(angle, gamma) * foodMultiplier;

                totalSum += pheromonePortions[i] + slopePortions[i] + directionPortions[i];
            }

            // Calculate each neighbour's percentage based on the sum of its values divided by the total sum

            (int index, float percentage)[] nPerctgs = new (int, float)[neighboursAmount];

            for (int i = 0; i < neighboursAmount; i++) {

                float percentage = (pheromonePortions[i] + slopePortions[i] + directionPortions[i]) / totalSum;

                nPerctgs[i] = (i, percentage);
            }

            // Randomly select the next cell
            Vector2Int nextCell = ChooseRandom(neighbours, nPerctgs);

            return nextCell;
        }

        private float CalcDirectionPercentage(float angle, float influence) {

            return Mathf.Pow(-(angle - 180f) / 180f, 1) * influence;
        }

        private float CalcPheromonePercentage(float ph, float influence) {

            return 1 + Mathf.Pow(ph, influence);
        }

        private float CalcSlopePercentage(float from, float to, float influence) {

            return Mathf.Pow(Mathf.Abs(1 + to - from), influence);
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
            int step = 0;

            // Main algorithm loop
            while (step < maxSteps) {

                UpdateAnts();
                UpdateMatrices();

                step++;
                DisplayCurrentStep(step);

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

            // Shuffle ants if required
            if (shuffleAnts)
                ants.Shuffle();

            // Loop through the number of ants
            for (int j = 0; j < nAnts; j++) {

                // Get the current ant's cell
                Vector2Int current = ants[j].CurrentCell;

                // Get the (moore) neighbours of the current cell (can include itself)
                Vector2Int[] neighbours = GetMooreNeighbours(current, antsInPlace);

                if (FoundFood(neighbours))
                    ants[j].HasFood = true;

                // Get the next cell based on height and pheromone levels
                Vector2Int next = GetNextPoint(current, ants[j].StartCell, neighbours, ants[j].HasFood);

                // Decrement a manual value from the heightmap at the select cell
                grid.Heights[next.y, next.x] -= heightIncr;

                // Update the ant's current cell
                ants[j].CurrentCell = next;
            }
        }

        private void UpdateMatrices() {

            // Evaporation & Dissipation
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

        private void DisplayCurrentStep(int step) =>
            stepsUI.text = $"{step}";

        // Flatten the heightmap when quitting app
        private void OnApplicationQuit() => FlattenHeightmap();
    }
}
