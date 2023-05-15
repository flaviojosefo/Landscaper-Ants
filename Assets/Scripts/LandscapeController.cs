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

        [SerializeField] private bool useSeed = false;                        // Should the algorithm use a fixed seed?
        [SerializeField] private int rndSeed = -188287030;                    // The user given random controlling seed
        [Space]
        [SerializeField] private bool shuffleAnts = false;                    // Should Ants be shuffled when iterated?
        [SerializeField] private bool individualStart = false;                // Should Ants' starting position be randomly different between eachother?

        [SerializeField, Range(1, 500)] private int nAnts = 2;                // The amount of Ants

        [SerializeField, Range(1, 100000)] private float maxSteps = 1000;     // The number of steps any Ant can perform

        [Header("Behavioural Settings")]

        [SerializeField] private bool antsInPlace = false;                    // Should Ants be able to select the next cell as the one they're on?
        [SerializeField] private bool absSlope = false;                       // Controls whether Ants should use the absolute of slopes or a min/max method
        [Space]
        [SerializeField, Range(0, 1)] private float pheromoneWeight = 1f;     // Pheromone weight used on cell selection
        [SerializeField, Range(0, 1)] private float slopeWeight = 1f;         // Slope weight used on cell selection
        [SerializeField, Range(0, 1)] private float directionWeight = 1f;     // Direction (to starting cell) weight used on cell selection
        [SerializeField, Range(0, 1)] private float randomWeight = 1f;        // Random weight used on cell selection
        [Space]
        [SerializeField, Range(0, 1)] private float pheromoneDeposit = 0.1f;  // The pheromone value that ants deposit on a given cell
        [Space]
        [SerializeField, Range(0, 1)] private float phEvap = 0.05f;           // Pheromone evaporation coefficient
        [SerializeField, Range(0, 1)] private float phDiff = 0.05f;           // Pheromone diffusion coefficient

        [Header("Heightmap Settings")]

        [SerializeField] private float foodHeightIncr = 0.02f;                // The height value that ants that have food remove from a given cell
        [SerializeField] private float noFoodHeightIncr = 0.01f;              // The height value that ants that DON'T have food remove from a given cell

        [SerializeField] private Terrain terrain;                             // The terrain that will be affected by the heightmap changes

        [SerializeField, Space] private Grid grid;                            // The collection of nodes and respective cost and pheromone matrices

        [Header("Display Settings")]

        [SerializeField] private bool displayHeight = true;                   // Display height transformations on terrain?
        [SerializeField] private bool displayPheromones = true;               // Display pheromone concentrations on terrain?
        [Space]
        [SerializeField] private TMP_Text stepsUI;
        [SerializeField] private GameObject homeSprite;
        [SerializeField] private Gradient phColorGradient;                    // Color gradient for representation of pheromone amount on the terrain

        private Ant[] ants;                                                   // The Ants which will be pathtracing

        private Coroutine antWork;                                            // The coroutine for the algorithm's main loop (1 iteration/step per frame)

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
                IList<Vector2Int> neighbours = GetMooreNeighbours(current, antsInPlace);

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
                        grid.Pheromones[current.y, current.x] = Mathf.Clamp(newPheromoneValue, 0f, 1f);
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
        private Vector2Int GetNextPoint(IList<Vector2Int> neighbours, Vector2Int current, Vector2Int? destination = null)
        {
            int neighboursAmount = neighbours.Count;

            float[] pheromonePortions = new float[neighboursAmount];
            float[] slopePortions = new float[neighboursAmount];
            float[] directionPortions = new float[neighboursAmount];
            float[] randomPortions = new float[neighboursAmount];

            // Get the current cell's height
            float currentHeight = grid.Heights[current.y, current.x];

            // Fetch min/max height
            float minHeight = currentHeight;
            float maxHeight = currentHeight;

            // Fetch neighbours' heights and override min/max heights if others are found
            float[] nHeights = FetchNeighboursHeights(neighbours, ref minHeight, ref maxHeight);

            // Calculate individual variable influences and save their sum

            float totalSum = 0;

            for (int i = 0; i < neighboursAmount; i++)
            {
                // Calculate slope portion outside of the if, since it's common between both types of ants (with food vs no food)
                if (minHeight == maxHeight)
                {
                    // Apply a value of 1 to all neighbours's slope portion if there is no difference between the min and max heights
                    slopePortions[i] = 1f;
                } 
                else
                {
                    // Get height differences between min/max and the ant's cell's height
                    float minDiff = minHeight - currentHeight;
                    float maxDiff = maxHeight - currentHeight;

                    // Check which method to use
                    if (absSlope)
                    {
                        float minAbs = Mathf.Abs(minDiff);
                        float maxAbs = Mathf.Abs(maxDiff);

                        float maxAbsDiff = minAbs > maxAbs ? minAbs : maxAbs;

                        slopePortions[i] = CalcSlopePortion(currentHeight, nHeights[i], maxAbsDiff) * slopeWeight;
                    }
                    else
                    {
                        slopePortions[i] = CalcSlopePortion(currentHeight, nHeights[i], minDiff, maxDiff) * slopeWeight;
                    }
                }

                // destination is null = exploring = the ant has no food
                if (destination is null)
                {
                    pheromonePortions[i] = CalcPheromonePortion(grid.Pheromones[neighbours[i].y, neighbours[i].x]) * pheromoneWeight;

                    randomPortions[i] = CalcRandomPortion() * randomWeight;
                }
                else
                {
                    Vector2Int mainDirection = (Vector2Int)destination - current;

                    // Calculate direction and angle with [origin to destination] vector
                    Vector2Int direction = neighbours[i] - current;
                    float angle = Vector2.Angle(mainDirection, direction);

                    // If the ants are allowed to stay in the same cell and that's the cell being evaluated
                    // calculate its direction portion as 0 (i.e. the ant doesn't like it)
                    directionPortions[i] = antsInPlace && direction == Vector2Int.zero ?
                        0f : CalcDirectionPortion(angle) * directionWeight;
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

        private float CalcDirectionPortion(float angle)
        {
            return 1f - (angle / 180f);
        }

        private float CalcPheromonePortion(float ph)
        {
            return ph;
        }

        // Cells with less slope are preferential; all cells are treated as positive values
        private float CalcSlopePortion(float from, float to, float maxDiff)
        {
            return 1f - (Mathf.Abs(to - from) / maxDiff);
        }

        // Cells with lower height values are preferential
        private float CalcSlopePortion(float from, float to, float minDiff, float maxDiff)
        {
            return 1f - ((to - from + Mathf.Abs(minDiff)) / (Mathf.Abs(minDiff) + Mathf.Abs(maxDiff)));
        }

        private float CalcRandomPortion()
        {
            return Random.value;
        }

        // Fetches the height values of the found neighbours
        private float[] FetchNeighboursHeights(IList<Vector2Int> neighbours, 
            ref float minHeight, ref float maxHeight)
        {
            float[] heights = new float[neighbours.Count];

            for (int i = 0; i < neighbours.Count; i++)
            {
                float nHeight = grid.Heights[neighbours[i].y, neighbours[i].x];

                if (nHeight < minHeight)
                    minHeight = nHeight;

                if (nHeight > maxHeight)
                    maxHeight = nHeight;

                heights[i] = nHeight;
            }

            return heights;
        }

        // Choose a random member of a collection based on a roulette wheel operation
        private T ChooseRandom<T>(IList<T> collection, (int index, float percentage)[] probabilities)
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
            // Create a copy of the pheromones matrix
            float[,] newPheromones = new float[grid.BaseDim, grid.BaseDim];

            // Evaporation & Diffusion
            for (int y = 0; y < grid.BaseDim; y++)
            {
                for (int x = 0; x < grid.BaseDim; x++)
                {
                    // Get the current pheromone concentration
                    float currentPhCon = grid.Pheromones[y, x];

                    // The total concentration of pheromones on all neighbours
                    float neighbrsTotalPhCon = 0f;

                    // Get a cell's neighbours
                    Vector2Int[] neighbours = GetNeighbours(new(x, y));

                    // Get the pheromone concentration of each found neighbour
                    for (int k = 0; k < neighbours.Length; k++)
                    {
                        // Get a reference to current neighbour
                        Vector2Int n = neighbours[k];

                        // Increase total concentration only if coordinates are inside of the available 2D space
                        if (n.x >= 0 && n.y >= 0 && n.x < grid.BaseDim && n.y < grid.BaseDim)
                        {
                            neighbrsTotalPhCon += grid.Pheromones[n.y, n.x];
                        }
                    }

                    // Only calculate evaporation and diffusion if there's any
                    // pheromone concentration in either the cell or its neighbours
                    if ((neighbrsTotalPhCon != 0f) || (currentPhCon != 0f))
                    {
                        // Update pheromone value in specific cell based on evaporation and diffusion
                        newPheromones[y, x] =
                            (1f - phEvap) * (currentPhCon + (phDiff * ((neighbrsTotalPhCon / neighbours.Length) - currentPhCon)));
                    }
                }
            }

            // Apply the new pheromone values to the general pheromone matrix
            grid.Pheromones = newPheromones;
        }

        // Gets the neighbours surrounding a cell, including those that don't exist in the grid
        private Vector2Int[] GetNeighbours(Vector2Int cell)
        {
            // The list of neighbours to find
            return new Vector2Int[8]
            {
                new(cell.x - 1, cell.y - 1),
                new(cell.x - 1, cell.y),
                new(cell.x - 1, cell.y + 1),
                new(cell.x, cell.y - 1),
                new(cell.x, cell.y + 1),
                new(cell.x + 1, cell.y - 1),
                new(cell.x + 1, cell.y),
                new(cell.x + 1, cell.y + 1)
            };
        }

        // Checks if the Ant is next to a food source
        private bool FoundFood(IList<Vector2Int> neighbours, out Food food)
        {
            // Returns true if one of the neighbouring cells is a food source
            for (int i = 0; i < grid.Foods.Length; i++)
            {
                for (int j = 0; j < neighbours.Count; j++)
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
        private IList<Vector2Int> GetMooreNeighbours(Vector2Int p, bool includeOrigin = false)
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
            return neighbours;
        }

        // Updates the terrain's components
        private void UpdateTerrain()
        {
            if (displayHeight)
                DisplayHeight();

            if (displayPheromones)
                DisplayPheromoneColor();
        }

        // Display height changes on the terrain (applying a new heightmap)
        private void DisplayHeight()
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

        // Display pheromone concentration on the terrain as a color from the available gradient
        private void DisplayPheromoneColor()
        {
            Texture2D phTex = new(grid.BaseDim, grid.BaseDim);

            for (int y = 0; y < grid.BaseDim; y++)
            {
                for (int x = 0; x < grid.BaseDim; x++)
                {
                    Color gradientColor = phColorGradient.Evaluate(grid.Pheromones[y, x]);

                    phTex.SetPixel(x, y, gradientColor);
                }
            }

            phTex.Apply();

            terrain.materialTemplate.SetTexture("_PheromoneTex", phTex);
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
        private void OnApplicationQuit() 
        { 
            FlattenHeightmap();
            terrain.materialTemplate.SetTexture("_PheromoneTex", Texture2D.whiteTexture);
        }
    }
}
