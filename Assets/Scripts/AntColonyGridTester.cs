using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Generator;
using TMPro;
using Random = UnityEngine.Random;
using Unity.VisualScripting;

namespace LandscaperAnts {

    public sealed class AntColonyGridTester : MonoBehaviour {

        [Header("ACO Settings")]

        [SerializeField] private bool antsInPlace = true;                   // Should Ants be able to select the next cell as the one they're on?
        [SerializeField] private bool shuffleAnts = false;                  // Should the Ants be shuffled when iterated?

        [SerializeField, Range(1, 50)] private int nAnts = 2;               // The amount of Ants

        [SerializeField, Range(1, 50000)] private float maxSteps = 1000;    // The number of steps an Ant can perform

        [SerializeField, Range(0, 1)] private float pheromoneWeight = 1;    // Pheromone weight used on cell selection
        [SerializeField, Range(0, 1)] private float slopeWeight = 1;        // Slope weight used on cell selection
        [SerializeField, Range(0, 1)] private float directionWeight = 1;    // Direction (to starting cell) weight used on cell selection

        [SerializeField, Range(0, 1)] private float rho = 0.01f;            // Pheromone evaporation coefficient
        [SerializeField, Range(1, 5)] private int Q = 1;                    // Pheromone deposit coefficient

        [SerializeField, Range(1, 100)] private int r = 1;                  // The Moore neighbourdhood coefficient
        [SerializeField] private float heightIncr = 0.0001f;                // The value that controls "heightmap digging"

        [SerializeField, Range(1, 10)] private float maxPheromones = 1;     // The max amount of pheromones allowed to be on any given cell
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

            float pheromoneInfluence = 1;       // The factor that increases pheromone influence in the overall percentage calculation
            float slopeInfluence = 1;           // The factor that increases slope influence in the overall percentage calculation
            float directionInfluence = 1;       // The factor that increases direction influence in the overall percentage calculation

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

                directionPortions[i] = CalcDirectionPortion(angle, directionInfluence);

                pheromonePortions[i] = CalcPheromonePortion(grid.Pheromones[n.y, n.x], pheromoneInfluence);

                slopePortions[i] = CalcSlopePortion(grid.Heights[origin.y, origin.x], grid.Heights[n.y, n.x], slopeInfluence);

                denominator +=
                    directionPortions[i]
                    //pheromonePortions[i] +  
                    //slopePortions[i]
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

        // Returns a (pheromone and slope influenced) point for the Ant to move towards
        private Vector2Int GetNextPoint(Vector2Int current, Vector2Int[] neighbours) {

            int neighboursAmount = neighbours.Length;

            float[] pheromonePortions = new float[neighboursAmount];
            float[] slopePortions = new float[neighboursAmount];

            // Calculate individual variable influences and save their sum

            float totalSum = 0;

            for (int i = 0; i < neighboursAmount; i++) {

                Vector2Int n = neighbours[i];

                pheromonePortions[i] = CalcPheromonePortion(grid.Pheromones[n.y, n.x], pheromoneWeight);

                slopePortions[i] = CalcSlopePortion(grid.Heights[current.y, current.x], grid.Heights[n.y, n.x], slopeWeight);

                totalSum += pheromonePortions[i] + slopePortions[i];
            }

            // Calculate each neighbour's percentage based on the sum of its values divided by the total sum

            (int index, float percentage)[] nPerctgs = new (int, float)[neighboursAmount];

            for (int i = 0; i < neighboursAmount; i++) {

                float percentage = (pheromonePortions[i] + slopePortions[i]) / totalSum;

                nPerctgs[i] = (i, percentage);
            }

            // Randomly select the next cell
            Vector2Int nextCell = ChooseRandom(neighbours, nPerctgs);

            return nextCell;
        }

        // Returns a (slope and direction [to destination] influenced) point for the Ant to move towards
        private Vector2Int GetNextPoint(Vector2Int current, Vector2Int destination, Vector2Int[] neighbours) {

            int neighboursAmount = neighbours.Length;

            float[] slopePortions = new float[neighboursAmount];
            float[] directionPortions = new float[neighboursAmount];

            Vector2Int mainDirection = destination - current;

            // Calculate individual variable influences and save their sum

            float totalSum = 0;

            for (int i = 0; i < neighboursAmount; i++) {

                Vector2Int n = neighbours[i];

                // Calculate direction and angle with [origin to destination] vector
                Vector2Int direction = n - current;
                float angle = Vector2.Angle(mainDirection, direction);

                slopePortions[i] = CalcSlopePortion(grid.Heights[current.y, current.x], grid.Heights[n.y, n.x], slopeWeight);

                directionPortions[i] = CalcDirectionPortion(angle, directionWeight);

                totalSum += slopePortions[i] + directionPortions[i];
            }

            // Calculate each neighbour's percentage based on the sum of its values divided by the total sum

            (int index, float percentage)[] nPerctgs = new (int, float)[neighboursAmount];

            for (int i = 0; i < neighboursAmount; i++) {

                float percentage = (slopePortions[i] + directionPortions[i]) / totalSum;

                nPerctgs[i] = (i, percentage);
            }

            // Randomly select the next cell
            Vector2Int nextCell = ChooseRandom(neighbours, nPerctgs);

            return nextCell;
        }

        // Graphics with the functions present below
        // https://www.desmos.com/calculator/i6mplh4a4f

        private float CalcDirectionPortion(float angle, float weight) {

            return (1 - (angle / 180f)) * weight;
        }

        private float CalcPheromonePortion(float ph, float weight) {

            return (ph / maxPheromones) * weight;
        }

        private float CalcSlopePortion(float from, float to, float weight) {

            // The current terrain already goes from height of 0-1
            // If this changes (e.g. 0-50), divide 'Mathf.Abs(to - from)' by the max height (e.g. 50)
            return (1 - Mathf.Abs(to - from)) * weight;
        }

        // Choose a random member of a collection based on a roulette wheel operation
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
                UpdatePheromones();

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
            for (int i = 0; i < nAnts; i++) {

                // Get the current ant's cell
                Vector2Int current = ants[i].CurrentCell;

                // Get the (moore) neighbours of the current cell (can include itself)
                Vector2Int[] neighbours = GetMooreNeighbours(current, antsInPlace);

                // The cell the Ant will go to next
                Vector2Int next;

                // Check if the Ant is "carrying" food
                if (ants[i].HasFood) {

                    // If the Ant gets back home, stop carrying food
                    if (IsHome(ants[i]))
                        ants[i].HasFood = false;

                    // Choose the next cell with the idea of coming back home
                    next = GetNextPoint(current, ants[i].StartCell, neighbours);

                    // Increment the value of pheromone deposit on the selected cell
                    grid.Pheromones[next.y, next.x] += 0.01f;

                } else {

                    // If the Ant found some food, start carrying it
                    if (FoundFood(neighbours))
                        ants[i].HasFood = true;

                    // Choose the next cell with the idea of aimlessly searching for food
                    next = GetNextPoint(current, neighbours);
                }

                // Decrement a manual value from the heightmap at the select cell
                grid.Heights[next.y, next.x] -= heightIncr;

                // Update the ant's current cell
                ants[i].CurrentCell = next;
            }
        }

        private void UpdatePheromones() {

            // Evaporation & Dissipation

            for (int i = 0; i < grid.BaseDim; i++) {

                for (int j = 0; j < grid.BaseDim; j++) {

                    // Calculate new value based on evaporation
                    float evaporated = (1 - rho) * grid.Pheromones[i, j];

                    // Update pheromone value in specific cell
                    grid.Pheromones[i, j] = evaporated;
                }
            }
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

        // Check if the Ant is (back) at home
        private bool IsHome(TestAnt ant) => ant.CurrentCell == ant.StartCell;

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
