using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Generator;
using TMPro;
using Random = UnityEngine.Random;

namespace LandscaperAnts
{
    public sealed class LandscapeController : MonoBehaviour
    {
        [Header("General Settings")]

        [SerializeField] private bool useSeed = false;                      // Should the algorithm use a fixed seed?
        [SerializeField] private int rndSeed = -188287030;                  // The user given random controlling seed
        [Space]
        [SerializeField] private bool shuffleAnts = false;                  // Should Ants be shuffled when iterated?
        [SerializeField] private bool individualStart = false;              // Should Ants' starting position be randomly different between eachother?

        [SerializeField, Range(1, 500)] private int nAnts = 2;              // The amount of Ants

        [SerializeField, Range(1, 100000)] private float maxSteps = 1000;   // The number of steps an Ant can perform

        [Header("Behavioural Settings")]

        [SerializeField] private bool antsInPlace = false;                  // Should Ants be able to select the next cell as the one they're on?
        [Space]
        [SerializeField, Range(0, 1)] private float pheromoneWeight = 1;    // Pheromone weight used on cell selection
        [SerializeField, Range(0, 1)] private float slopeWeight = 1;        // Slope weight used on cell selection
        [SerializeField, Range(0, 1)] private float directionWeight = 1;    // Direction (to starting cell) weight used on cell selection
        [SerializeField, Range(0, 1)] private float randomWeight = 1;       // Random weight used on cell selection
        [Space]
        [SerializeField] private float pheromoneDeposit = 0.1f;             // The pheromone value that ants deposit on a given cell
        [SerializeField, Range(1, 10)] private float maxPheromones = 1;     // The max amount of pheromones allowed to be on any given cell
        [Space]
        [SerializeField, Range(0, 1)] private float phEvap = 0.05f;         // Pheromone evaporation coefficient
        [Space]
        [SerializeField, Range(0, 1)] private float maxSlope = 0.9f;        // The max slope an Ant can endure

        [Header("Heightmap Settings")]

        [SerializeField] private float foodHeightIncr = 0.02f;              // The height value that ants that have food remove from a given cell
        [SerializeField] private float noFoodHeightIncr = 0.01f;            // The height value that ants that DON'T have food remove from a given cell

        [SerializeField] private Terrain terrain;                           // The terrain that will be affected by the heightmap changes

        [SerializeField, Space] private Grid grid;                          // The collection of nodes and respective cost and pheromone matrices

        [Header("Display Settings")]

        [SerializeField] private TMP_Text stepsUI;

        [SerializeField] private GameObject homeSprite;

        private Ant[] ants;                                                 // The Ants which will be pathtracing

        private Coroutine antWork;                                          // The coroutine for the algorithm's main loop (1 iteration/step per frame)

        // Method to generate a new graph
        public void GenerateGrid()
        {
            // Return if the algorithm is executing
            if (antWork is not null)
                return;

            // Assign a seed, if requested
            if (useSeed)
                Random.InitState(rndSeed);

            // Reset heightmap
            FlattenHeightmap();

            // Create graph and cost and pheromone matrices
            grid.Generate();

            // Create a lump, to test the ants' dodging capabilities (or lack thereof)
            //grid.AddLump(new(380, 201), 15, 1f, 2f);
            print("----- Generated NEW Graph -----");
        }

        // Method to generate trails
        public void StartAnts()
        {
            if (antWork is not null && grid.Foods is null)
                return;

            print("----- Algorithm STARTED -----");
            antWork = StartCoroutine(Run());
        }

        // Algorithm's main method -> Threads: https://github.com/nunofachada/AIUnityExamples/blob/main/MovementOptimize/Assets/Scripts/Optimizer.cs
        private IEnumerator Run()
        {
            InitAnts();

            // The current number of iterations
            int step = 0;

            // Main algorithm loop
            while (step < maxSteps)
            {
                UpdateAnts();
                UpdatePheromones();

                step++;
                DisplayCurrentStep(step);

                //UpdateTerrain();

                yield return null;
            }

            // Update the terrain's heightmap
            UpdateTerrain();

            antWork = null;

            // Print a final message
            print("----- Algorithm ENDED -----");
        }

        private Vector2Int GetRandomPos()
        {
            int x = Random.Range(0, grid.BaseDim);
            int y = Random.Range(0, grid.BaseDim);

            return new(x, y);
        }

        // Initiate Ant state (mainly their position)
        private void InitAnts()
        {
            // Create the necessary ants
            ants = new Ant[nAnts];

            // The ants' colony position
            Vector2Int colony = GetRandomPos();

            // Check if ants start in separate cells or not
            if (individualStart)
            {
                for (int i = 0; i < nAnts; i++)
                {
                    // The starting position for the current ant
                    Vector2Int start = GetRandomPos();

                    ants[i] = new(colony, start);
                }
            }
            else
            {
                for (int i = 0; i < nAnts; i++)
                {
                    ants[i] = new(colony);
                }
            }

            DisplayHomeSprite(colony);

            print($"Ants' colony is at {colony}");
        }

        // Update the Ants' status
        private void UpdateAnts()
        {
            // Shuffle ants if required
            if (shuffleAnts)
                ants.Shuffle();

            // Loop through the number of ants
            for (int i = 0; i < nAnts; i++)
            {
                // Get the current ant's cell
                Vector2Int current = ants[i].CurrentCell;

                // The cell the Ant will move to
                Vector2Int next;

                // Get the (moore) neighbours of the current cell (can include itself)
                Vector2Int[] neighbours = GetMooreNeighbours(current, antsInPlace);

                // Check if the Ant is "carrying" food
                if (ants[i].HasFood())
                {
                    // Check if the Ant reached its home
                    if (ants[i].IsHome())
                    {
                        // If so, stop carrying food
                        ants[i].Food = null;

                        // Have the Ant stay in place
                        next = current;
                    }
                    else
                    {
                        // Normal Search Behaviour

                        // Choose the next cell with the idea of coming back home
                        next = GetNextPoint(neighbours, current, ants[i].ColonyCell);

                        // Increment the value of pheromone deposit on the selected cell
                        float newPheromoneValue = 
                            grid.Pheromones[current.y, current.x] + ants[i].DropPheromone(pheromoneDeposit);

                        // Apply the new value but clamp between a min and max
                        grid.Pheromones[current.y, current.x] = Mathf.Clamp(newPheromoneValue, 0, maxPheromones);
                    }
                }
                else
                {
                    // Check if the Ant has found food and if any is left
                    if (FoundFood(neighbours, out Food food) &&
                        food.HasBitesLeft())
                    {
                        // "Take a bite" out of the food
                        food.TakeABite();

                        // Start carrying food
                        ants[i].Food = food;

                        // Have the Ant stay in place
                        next = current;
                    }
                    else
                    {
                        // Choose the next cell with the idea of aimlessly searching for food
                        next = GetNextPoint(neighbours, current);
                    }
                }

                // Have the Ant "dig" the terrain
                Dig(current, next, i);

                // Update the ant's current cell
                ants[i].CurrentCell = next;
            }
        }

        private void Dig(Vector2Int current, Vector2Int next, int index)
        {
            float digAmount = ants[index].HasFood() ? foodHeightIncr : noFoodHeightIncr;

            // Decrement a manual value from the heightmap at the select cell
            grid.Heights[next.y, next.x] -= digAmount;

            Vector2Int direction = next - current;

            // Chosen cells (C + A + B + C + D)
            //  A C X
            //  C N X
            //  B D X

            // Check if the ant is moving horizontally, vertically or diagonally
            if (direction.x == 0 || direction.y == 0)
            {
                Vector2Int perpendicular = new(-direction.y, direction.x);

                for (int i = 0; i < 2; i++)
                {
                    Vector2Int f1 = (current + perpendicular) + (i * direction);
                    Vector2Int f2 = (current - perpendicular) + (i * direction);

                    float percentage = 0.2f - (0.2f * i * 0.5f);

                    if (f1.x >= 0 && f1.y >= 0 && f1.x < grid.BaseDim && f1.y < grid.BaseDim)
                        grid.Heights[f1.y, f1.x] += digAmount * percentage;

                    if (f2.x >= 0 && f2.y >= 0 && f2.x < grid.BaseDim && f2.y < grid.BaseDim)
                        grid.Heights[f2.y, f2.x] += digAmount * percentage;
                }

            }
            else
            {
                Vector2Int horizontal = new(direction.x, 0);
                Vector2Int vertical = new(0, direction.y);

                for (int i = 0; i < 2; i++)
                {
                    Vector2Int f1 = current + (horizontal * (i + 1));
                    Vector2Int f2 = current + (vertical * (i + 1));

                    float percentage = 0.2f - (0.2f * i * 0.5f);

                    if (f1.x >= 0 && f1.y >= 0 && f1.x < grid.BaseDim && f1.y < grid.BaseDim)
                        grid.Heights[f1.y, f1.x] += digAmount * percentage;

                    if (f2.x >= 0 && f2.y >= 0 && f2.x < grid.BaseDim && f2.y < grid.BaseDim)
                        grid.Heights[f2.y, f2.x] += digAmount * percentage;
                }
            }

            grid.Heights[current.y, current.x] += digAmount * 0.4f;

            // Update the minimum height, if lower than the last min value
            if (grid.Heights[next.y, next.x] < grid.MinHeight)
                grid.MinHeight = grid.Heights[next.y, next.x];
        }

        // Returns the point the Ant will move towards
        private Vector2Int GetNextPoint(Vector2Int[] neighbours, Vector2Int current, Vector2Int? destination = null)
        {
            int neighboursAmount = neighbours.Length;

            float[] pheromonePortions = new float[neighboursAmount];
            float[] slopePortions = new float[neighboursAmount];
            float[] directionPortions = new float[neighboursAmount];
            float[] randomPortions = new float[neighboursAmount];

            float offset = Mathf.Abs(grid.MinHeight);

            float currentHeight = grid.Heights[current.y, current.x] + offset;
            currentHeight /= (offset == 0 ? 1 : offset);

            // Fetch min/max height

            //float minHeight = grid.Heights[current.y, current.x];
            //float maxHeight = minHeight;

            //for (int i = 0; i < neighboursAmount; i++)
            //{
            //    float nHeight = grid.Heights[neighbours[i].y, neighbours[i].x];

            //    if (nHeight < minHeight)
            //        minHeight = nHeight;

            //    if (nHeight > maxHeight)
            //        maxHeight = nHeight;
            //}

            // Calculate individual variable influences and save their sum

            float totalSum = 0;

            for (int i = 0; i < neighboursAmount; i++)
            {
                Vector2Int n = neighbours[i];

                // Calculate slope portion outside of the if, since it's common between both types of ants (with food vs no food)

                float neighbourHeight = grid.Heights[n.y, n.x] + offset;
                neighbourHeight /= (offset == 0 ? 1 : offset);

                slopePortions[i] = CalcSlopePortion(currentHeight, neighbourHeight, slopeWeight);
                //slopePortions[i] = CalcSlopePortionDOWN(grid.Heights[n.y, n.x], minHeight, maxHeight, 0.0f);

                // destination is null = exploring = the ant has no food
                if (destination is null)
                {
                    pheromonePortions[i] = CalcPheromonePortion(grid.Pheromones[n.y, n.x], pheromoneWeight);

                    randomPortions[i] = CalcRandomPortion(randomWeight);
                }
                else
                {
                    Vector2Int mainDirection = (Vector2Int)destination - current;

                    // Calculate direction and angle with [origin to destination] vector
                    Vector2Int direction = n - current;
                    float angle = Vector2.Angle(mainDirection, direction);

                    directionPortions[i] = CalcDirectionPortion(angle, directionWeight);
                }

                totalSum += pheromonePortions[i] + slopePortions[i] + directionPortions[i] + randomPortions[i];
            }

            // Calculate each neighbour's percentage based on the sum of its values divided by the total sum

            (int index, float percentage)[] nPerctgs = new (int, float)[neighboursAmount];

            for (int i = 0; i < neighboursAmount; i++)
            {
                float percentage = (pheromonePortions[i] + slopePortions[i] + directionPortions[i] + randomPortions[i]) / totalSum;

                nPerctgs[i] = (i, percentage);
            }

            // Randomly select the next cell
            Vector2Int nextCell = ChooseRandom(neighbours, nPerctgs);

            return nextCell;
        }

        // Graphics with the functions present below
        // https://www.desmos.com/calculator/gc1pygvebj

        private float CalcDirectionPortion(float angle, float weight)
        {
            return (1f - (angle / 180f)) * weight;
        }

        private float CalcPheromonePortion(float ph, float weight)
        {
            return (ph / maxPheromones) * weight;
        }

        private float CalcSlopePortion(float from, float to, float weight)
        {
            // The terrain must go from height of 0-1
            return (1f - Mathf.Abs(to - from)) * weight;
        }

        private float CalcSlopePortionNEW(float height, float minHeight, float maxHeight)
        {
            return 1f - Mathf.Abs(((height - minHeight) / (maxHeight - minHeight)) * 2f - 1f);
        }

        // NEW: https://www.desmos.com/calculator/xictvlxuyj
        private float CalcSlopePortionDOWN(float height, float min, float max, float minPerct)
        {
            // minPerct is the percentage that will be available at max(height)
            // this changes the function's output from 0-1 to minPerct-1

            return 1f - (((height - min) / (max - min)) * (1f - minPerct));
        }

        private float CalcRandomPortion(float weight)
        {
            return Random.value * weight;
        }

        // Choose a random member of a collection based on a roulette wheel operation
        private T ChooseRandom<T>(T[] collection, (int index, float percentage)[] probabilities)
        {
            // ##### ROULETTE WHEEL #####

            probabilities = probabilities.OrderBy(x => x.percentage).ToArray();

            // Calculate cumulative sum
            float[] cumulSum = new float[probabilities.Length + 1];

            for (int i = 0; i < probabilities.Length; i++)
            {
                cumulSum[i + 1] = cumulSum[i] + probabilities[i].percentage;
            }

            // Get random value between 0 and 1 (both inclusive)
            float rnd = Random.value;

            for (int i = 0; i < cumulSum.Length - 1; i++)
            {
                if ((rnd >= cumulSum[i]) && (rnd < cumulSum[i + 1]))
                {
                    return collection[probabilities[i].index];
                }
            }

            // Choose the last cell if cumulative sum didn't achieve 1
            return collection[probabilities[^1].index];
        }

        private void UpdatePheromones()
        {
            // Evaporation & Dissipation

            for (int i = 0; i < grid.BaseDim; i++)
            {
                for (int j = 0; j < grid.BaseDim; j++)
                {
                    // Calculate new value based on evaporation
                    float evaporated = (1 - phEvap) * grid.Pheromones[i, j];

                    // Update pheromone value in specific cell
                    grid.Pheromones[i, j] = evaporated;
                }
            }
        }

        // Checks if the Ant is next to a food source
        private bool FoundFood(Vector2Int[] neighbours, out Food food)
        {
            // Returns true if one of the neighbouring cells is a food source
            for (int i = 0; i < grid.Foods.Length; i++)
            {
                for (int j = 0; j < neighbours.Length; j++)
                {
                    if (grid.Foods[i].Cell == neighbours[j])
                    {
                        // 'out' the food that was found
                        food = grid.Foods[i];

                        return true;
                    }
                }
            }

            // No food was found
            food = null;

            // Returns false if no neighbour is a food source
            return false;
        }

        // Search for a point's neighbours using Moore's neighbourhood algorithm
        private Vector2Int[] GetMooreNeighbours(Vector2Int p, bool includeOrigin = false)
        {
            // The list of neighbours to find
            List<Vector2Int> neighbours = new();

            // Add the origin as a neighbour if we want to include it
            if (includeOrigin)
                neighbours.Add(p);

            // Search for all neighbouring cells (Moore neighbourhood)
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
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

        // Updates the terrain's heightmap
        private void UpdateTerrain()
        {
            // Get the value for translation
            float offset = Mathf.Abs(grid.MinHeight);

            // Create a normalized heightmap
            float[,] translated = new float[grid.BaseDim, grid.BaseDim];

            for (int i = 0; i < grid.BaseDim; i++)
            {
                for (int j = 0; j < grid.BaseDim; j++)
                {
                    translated[i, j] = (grid.Heights[i, j] + offset) / grid.TerrainSize.y;
                }
            }

            // Update the terrain's heightmap
            terrain.terrainData.SetHeights(0, 0, translated);

            // Lower the terrain's position to compensate the increase in height
            terrain.transform.position = new(-5, grid.MinHeight, -5);
        }

        // Stops the algorithm
        public void StopAnts()
        {
            // Return if the coroutine isn't running
            if (antWork is null)
                return;

            // Stops the algorithm's coroutine
            StopCoroutine(antWork);

            UpdateTerrain();

            // Print a final message
            print("----- Algorithm STOPPED -----");
        }

        // Resets the heightmap
        public void FlattenHeightmap()
        {
            int texelSize = grid.BaseDim;

            // Apply the heightmap resolution on the terrain
            terrain.terrainData.heightmapResolution = texelSize;

            // Apply the terrain's size vector
            terrain.terrainData.size = grid.TerrainSize;

            // Create the heightmap
            float[,] heights = new float[texelSize, texelSize];

            // Apply the flattened heightmap on the terrain
            terrain.terrainData.SetHeights(0, 0, heights);
        }

        // Display sprites representing food at their 3D equivalent location
        private void DisplayHomeSprite(Vector2Int start)
        {
            // Skip creation if no sprite is given
            if (homeSprite == null)
                return;

            // Instantiate sprite
            Vector3 spritePos = grid.TexelToVector(start);

            Instantiate(homeSprite, transform).transform.position = spritePos;
        }

        private void DisplayCurrentStep(int step) =>
            stepsUI.text = $"{step}";

        // Flatten the heightmap when quitting app
        private void OnApplicationQuit() => FlattenHeightmap();
    }
}
