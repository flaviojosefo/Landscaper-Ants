using System;
using System.Linq;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Generator;
using NaughtyAttributes;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace LandscaperAnts
{
    public sealed class LandscapeController : MonoBehaviour
    {
        // ----- Constants -----
        private const string ExprmntrSettings = "Experimenter Settings";
        private const string GeneralParams = "General Parameters";
        private const string BehaviouralParams = "Behavioural Parameters";
        private const string HeightmapParams = "Heightmap Parameters";
        private const string DisplayParams = "Display Parameters";

        // ----- Experimenter reference -----

        // Should the next run be an experiment run?
        [BoxGroup(ExprmntrSettings)]
        [SerializeField]
        private bool runExperiments;

        // A collection of predefined parameters
        [BoxGroup(ExprmntrSettings)]
        [SerializeField]
        [ShowIf(nameof(runExperiments))]
        private Experimenter experimenter;

        // ----- Private EDITOR config parameters -----

        // Should the algorithm use a fixed seed?
        [BoxGroup(GeneralParams)]
        [SerializeField]
        private bool useSeed = false;

        // The user given random controlling seed
        [BoxGroup(GeneralParams)]
        [SerializeField]
        [EnableIf(nameof(useSeed))]
        private int rndSeed = -188287030;

        // Should Ants be shuffled when iterated?
        [Space]
        [BoxGroup(GeneralParams)]
        [SerializeField]
        private bool shuffleAnts = false;

        // Should Ants' starting position be randomly different between each other?
        [BoxGroup(GeneralParams)]
        [SerializeField]
        private bool individualStart = false;

        // The amount of Ants
        [BoxGroup(GeneralParams)]
        [Range(1, 5000)]
        [SerializeField]
        private int nAnts = 2;

        // The number of steps any Ant can perform
        [BoxGroup(GeneralParams)]
        [Range(1, 100000)]
        [SerializeField]
        private float maxSteps = 1000;

        // Should Ants be able to select the next cell as the one they're on?
        [BoxGroup(BehaviouralParams)]
        [SerializeField]
        private bool antsInPlace = false;

        // Controls whether Ants should use the absolute of slopes or a min/max method
        [BoxGroup(BehaviouralParams)]
        [SerializeField]
        private bool absSlope = false;

        // Pheromone weight used on cell selection
        [BoxGroup(BehaviouralParams)]
        [Space]
        [Range(0, 1)]
        [SerializeField]
        private float pheromoneWeight = 1f;

        // Slope weight used on cell selection
        [BoxGroup(BehaviouralParams)]
        [Range(0, 1)]
        [SerializeField]
        private float slopeWeight = 1f;

        // Direction (to starting cell) weight used on cell selection
        [BoxGroup(BehaviouralParams)]
        [Range(0, 1)]
        [SerializeField]
        private float directionWeight = 1f;

        // Random weight used on cell selection
        [BoxGroup(BehaviouralParams)]
        [Range(0, 1)]
        [SerializeField]
        private float randomWeight = 1f;

        // The pheromone value that ants deposit on a given cell
        [BoxGroup(BehaviouralParams)]
        [Space]
        [Range(0, 1)]
        [SerializeField]
        private float pheromoneDeposit = 0.1f;

        // Pheromone evaporation coefficient
        [BoxGroup(BehaviouralParams)]
        [Space]
        [Range(0, 1)]
        [SerializeField]
        private float phEvap = 0.05f;

        // Pheromone diffusion coefficient
        [BoxGroup(BehaviouralParams)]
        [Range(0, 1)]
        [SerializeField]
        private float phDiff = 0.05f;

        // The height value that ants that have food remove from a given cell
        [BoxGroup(HeightmapParams)]
        [SerializeField]
        private float foodHeightIncr = 0.02f;

        // The height value that ants that DON'T have food remove from a given cell
        [BoxGroup(HeightmapParams)]
        [SerializeField]
        private float noFoodHeightIncr = 0.01f;

        // The terrain that will be affected by the heightmap changes
        [BoxGroup(HeightmapParams)]
        [SerializeField]
        private Terrain terrain;

        // The collection of nodes and respective cost and pheromone matrices
        [Space]
        [BoxGroup()]
        [SerializeField]
        private Grid grid;

        // Display height transformations on terrain?
        [BoxGroup(DisplayParams)]
        [SerializeField]
        private bool displayHeight = true;

        // Display pheromone concentrations on terrain?
        [BoxGroup(DisplayParams)]
        [SerializeField]
        private bool displayPheromones = true;

        // The sprite used in locating the ants' colony
        [BoxGroup(DisplayParams)]
        [SerializeField]
        private GameObject homeSprite;

        // The scene's main camera
        [BoxGroup(DisplayParams)]
        [SerializeField]
        private GameObject sceneCam;

        // Color gradient for representation of pheromone amount on the terrain
        [BoxGroup(DisplayParams)]
        [SerializeField]
        private Gradient phColorGradient;

        // ----- Private instance variables -----

        // The Ants which will be pathtracing
        private Ant[] ants;

        private readonly int[] neighboursX = { -1, -1, -1,  0, 0,  1, 1, 1 };
        private readonly int[] neighboursY = { -1,  0,  1, -1, 1, -1, 0, 1 };

        // ----- METHODS -----

        [Button("Start", EButtonEnableMode.Editor)]
        private void StartAlgorithm()
        {
            // Assign a seed, if requested
            if (useSeed)
                Random.InitState(rndSeed);

            // Reset terrain's components
            ResetTerrain();

            // Start a timer to measure total algorithm time
            Stopwatch sw = Stopwatch.StartNew();

            // Create the necessary height/pheromone matrices
            grid.Generate();

            print("----- Generated NEW Grid -----");

            // Create a lump, to test the ants' dodging capabilities (or lack thereof)
            //grid.AddLump(new(380, 201), 15, 1f, 2f);

            print("----- Algorithm STARTED -----");

            // Create array of Ants
            InitAnts();

            // The current number of iterations
            int step = 0;

            // Main algorithm loop
            while (step < maxSteps)
            {
                UpdateAnts();
                UpdatePheromones();

                step++;

                //UpdateTerrain();

                // Stop the algorithm if the user decides to cancel it
                if (EditorUtility.DisplayCancelableProgressBar(
                    $"The ants are working!",
                    $"On step {step} of {maxSteps}...",
                    step / maxSteps))
                {
                    // Leave the loop and print a message
                    print("----- Algorithm CANCELLED -----");
                    break;
                }
            }

            // Clear progress bar
            EditorUtility.ClearProgressBar();

            // Print the time taken
            DisplayCalculationTime(sw.Elapsed.TotalMinutes);

            // Update the terrain's heightmap
            UpdateTerrain();
        }

        // Resets the terrain's components (heightmap and textures) back to default
        [Button("Reset", EButtonEnableMode.Editor)]
        private void ResetTerrain()
        {
            FlattenHeightmap();
            terrain.materialTemplate.SetTexture("_PheromoneTex", null);
        }

        private Vector2Int GetRandomPos()
        {
            int x = Random.Range(0, grid.BaseDimMinusOne);
            int y = Random.Range(0, grid.BaseDimMinusOne);

            return new(x, y);
        }

        // Initiate Ants' state (mainly their position)
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

            //DisplayHomeSprite(colony);

            //print($"Ants' colony is at {colony}");
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

                    if (f1.x >= 1 && f1.y >= 1 && f1.x < grid.BaseDimMinusOne && f1.y < grid.BaseDimMinusOne)
                        grid.Heights[f1.y, f1.x] += digAmount * percentage;

                    if (f2.x >= 1 && f2.y >= 1 && f2.x < grid.BaseDimMinusOne && f2.y < grid.BaseDimMinusOne)
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

                    if (f1.x >= 1 && f1.y >= 1 && f1.x < grid.BaseDimMinusOne && f1.y < grid.BaseDimMinusOne)
                        grid.Heights[f1.y, f1.x] += digAmount * percentage;

                    if (f2.x >= 1 && f2.y >= 1 && f2.x < grid.BaseDimMinusOne && f2.y < grid.BaseDimMinusOne)
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
            for (int y = 1; y < grid.BaseDimMinusOne; y++)
            {
                for (int x = 1; x < grid.BaseDimMinusOne; x++)
                {
                    // Get the current pheromone concentration
                    float currentPhCon = grid.Pheromones[y, x];

                    // The total concentration of pheromones on all neighbours
                    float neighbrsTotalPhCon = 0f;

                    // Get the pheromone concentration of each found neighbour
                    for (int k = 0; k < neighboursX.Length; k++)
                    {
                        neighbrsTotalPhCon += grid.Pheromones[y + neighboursY[k], x + neighboursX[k]];
                    }

                    // Only calculate evaporation and diffusion if there's any
                    // pheromone concentration in either the cell or its neighbours
                    if ((neighbrsTotalPhCon != 0f) || (currentPhCon != 0f))
                    {
                        // Update pheromone value in specific cell based on evaporation and diffusion
                        newPheromones[y, x] =
                            (1f - phEvap) * (currentPhCon + (phDiff * ((neighbrsTotalPhCon / neighboursX.Length) - currentPhCon)));
                    }
                }
            }

            // Apply the new pheromone values to the general pheromone matrix
            grid.Pheromones = newPheromones;
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
                    if (nx < 1 || ny < 1 || nx >= grid.BaseDimMinusOne || ny >= grid.BaseDimMinusOne)
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

            // Reset the terrain's position
            terrain.transform.position = new(-5, 0, -5);
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

        // Displays the algorithm's ellapsed minutes and seconds
        private void DisplayCalculationTime(double ellapsedMinutes)
        {
            int minutes = (int)ellapsedMinutes;
            double fractionalPart = ellapsedMinutes - minutes;

            int seconds = (int)(fractionalPart * 60d);

            string calcTime = "----- Algorithm finished after ";

            if (minutes == 0)
            {
                calcTime += $"{seconds}";
            }
            else if (minutes == 1)
            {
                calcTime += $"{minutes} minute and {seconds}";
            }
            else
            {
                calcTime += $"{minutes} minutes and {seconds}";
            }

            calcTime += " seconds. -----";

            print(calcTime);
        }

        [Button(enabledMode: EButtonEnableMode.Playmode)]
        [ShowIf(nameof(runExperiments))]
        private IEnumerator RunExperiments()
        {
            // Check if Experimenter and Camera both have assigned references
            if (experimenter == null)
            {
                Debug.LogWarning("Experimenter was not assigned!");
                yield break;
            }
            else if (sceneCam == null)
            {
                Debug.LogWarning("Scene Camera was not assigned!");
                yield break;
            }

            // Disable rendering
            sceneCam.SetActive(false);

            // Create the main experiments directory, if it doesn't exist
            experimenter.CreateMainDirectory();

            // Start a counter to determine the experiment's index
            int expIndex = 0;

            // Assign values to parameters that only need it once
            useSeed = true;

            maxSteps = experimenter.MaxSteps;

            grid.MaxFoodBites = experimenter.MaxFoodBites;

            // Nested for loops to combine different parameters and run the algorithm
            for (int i = 0; i < experimenter.Seeds.Length; i++)
            {
                rndSeed = experimenter.Seeds[i];

                for (int j = 0; j < experimenter.ShuffleAnts.Length; j++)
                {
                    shuffleAnts = experimenter.ShuffleAnts[j];

                    for (int k = 0; k < experimenter.IndividualStart.Length; k++)
                    {
                        individualStart = experimenter.IndividualStart[k];

                        for (int l = 0; l < experimenter.NAnts.Length; l++)
                        {
                            nAnts = experimenter.NAnts[l];

                            for (int m = 0; m < experimenter.AntsInPlace.Length; m++)
                            {
                                antsInPlace = experimenter.AntsInPlace[m];

                                for (int n = 0; n < experimenter.AbsSlope.Length; n++)
                                {
                                    absSlope = experimenter.AbsSlope[n];

                                    for (int o = 0; o < experimenter.Weights.Length; o++)
                                    {
                                        pheromoneWeight = experimenter.Weights[o].x;
                                        slopeWeight = experimenter.Weights[o].y;
                                        directionWeight = experimenter.Weights[o].z;
                                        randomWeight = experimenter.Weights[o].w;

                                        for (int p = 0; p < experimenter.PhEvap.Length; p++)
                                        {
                                            phEvap = experimenter.PhEvap[p];

                                            for (int q = 0; q < experimenter.PhDiff.Length; q++)
                                            {
                                                phDiff = experimenter.PhDiff[q];

                                                for (int r = 0; r < experimenter.HeightIncr.Length; r++)
                                                {
                                                    foodHeightIncr = experimenter.HeightIncr[r].x;
                                                    noFoodHeightIncr = experimenter.HeightIncr[r].y;

                                                    for (int s = 0; s < experimenter.FlatTerrain.Length; s++)
                                                    {
                                                        grid.FlatTerrain = experimenter.FlatTerrain[s];

                                                        for (int t = 0; t < experimenter.FoodAmount.Length; t++)
                                                        {
                                                            grid.FoodAmount = experimenter.FoodAmount[t];

                                                            // Create this experiment's necessary directories
                                                            experimenter.CreateExperimentDirectory(expIndex);

                                                            // Only execute an experiment if it wasn't already performed
                                                            if (!experimenter.ParamsFileExits(expIndex))
                                                            {
                                                                // Assign a seed, if requested
                                                                Random.InitState(rndSeed);

                                                                // Reset terrain's components
                                                                ResetTerrain();

                                                                // Create the necessary height/pheromone matrices
                                                                grid.Generate();

                                                                // Create array of Ants
                                                                InitAnts();

                                                                // The current number of iterations
                                                                int step = 0;

                                                                // Main algorithm loop
                                                                while (step < maxSteps)
                                                                {
                                                                    UpdateAnts();
                                                                    UpdatePheromones();

                                                                    step++;

                                                                    if (step % experimenter.PrintStep == 0)
                                                                    {
                                                                        // Re-enable rendering
                                                                        sceneCam.SetActive(true);
                                                                        
                                                                        // Update the terrain
                                                                        UpdateTerrain();

                                                                        // Cycle through all desired camera positions/rotations
                                                                        for (int u = 0; u < experimenter.CameraPrintPositions.Length; u++)
                                                                        {
                                                                            // Assign a position
                                                                            sceneCam.transform.position = experimenter.CameraPrintPositions[u];

                                                                            // Assign a rotation
                                                                            sceneCam.transform.eulerAngles = experimenter.CameraPrintEulerAngles[u];

                                                                            // Wait until end of current frame
                                                                            yield return new WaitForEndOfFrame();

                                                                            // Take a screenshot
                                                                            experimenter.PrintScreen(expIndex, step, u);
                                                                        }

                                                                        // Disable rendering
                                                                        sceneCam.SetActive(false);
                                                                    }

                                                                    // Stop the algorithm if the user decides to cancel it
                                                                    if (EditorUtility.DisplayCancelableProgressBar(
                                                                        $"Running experiment {expIndex}",
                                                                        $"On step {step} of {maxSteps}...",
                                                                        step / maxSteps))
                                                                    {
                                                                        // Clear progress bar
                                                                        EditorUtility.ClearProgressBar();

                                                                        print("----- ALL EXPERIMENTS CANCELED! -----");

                                                                        // Stop running the Application
                                                                        EditorApplication.ExitPlaymode();

                                                                        yield break;
                                                                    }

                                                                    yield return null;
                                                                }

                                                                // Clear progress bar
                                                                EditorUtility.ClearProgressBar();

                                                                // Create the content to write on the parameters file
                                                                string content =
                                                                    $"RndSeed: {rndSeed}\n" +
                                                                    $"ShuffleAnts: {shuffleAnts}\n" +
                                                                    $"IndividualStart: {individualStart}\n" +
                                                                    $"NAnts: {nAnts}\n" +
                                                                    $"MaxSteps: {maxSteps}\n" +
                                                                    $"AntsInPlace: {antsInPlace}\n" +
                                                                    $"AbsSlope: {absSlope}\n" +
                                                                    $"PheromoneWeight: {pheromoneWeight}\n" +
                                                                    $"SlopeWeight: {slopeWeight}\n" +
                                                                    $"DirectionWeight: {directionWeight}\n" +
                                                                    $"RandomWeight: {randomWeight}\n" +
                                                                    $"PhEvap: {phEvap}\n" +
                                                                    $"PhDiff: {phDiff}\n" +
                                                                    $"FoodHeightIncr: {foodHeightIncr}\n" +
                                                                    $"NoFoodHeightIncr: {noFoodHeightIncr}\n" +
                                                                    $"FlatTerrain: {grid.FlatTerrain}\n" +
                                                                    $"FoodAmount: {grid.FoodAmount}\n" +
                                                                    $"MaxFoodBites: {grid.MaxFoodBites}\n";

                                                                // Create the parameters file and write to it
                                                                experimenter.CreateParamsFile(expIndex, content);

                                                                print($"Experiment {expIndex} completed!");
                                                            }

                                                            // Increment the experiment counter
                                                            expIndex++;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            print("----- ALL EXPERIMENTS FINISHED! -----");

            // Stop running the Application
            EditorApplication.ExitPlaymode();
        }

        // Reset the terrain on quitting the application
        private void OnApplicationQuit()
        {
            ResetTerrain();
        }
    }
}
