using System;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace LandscaperAnts
{
    [Serializable]
    public sealed class Grid
    {
        [Header("Grid Generation Settings")]

        [SerializeField]
        [Tooltip("Controls wether the terrain will start as a float surface or with Perlin Noise applied")]
        private bool flatTerrain = true;

        [SerializeField]
        [Tooltip("The amount of food to generate")]
        private int foodAmount = 4;

        [SerializeField]
        [Tooltip("The max amount times a food can be accessed")]
        private int maxFoodBites = 10;

        [SerializeField]
        [Tooltip("The max terrain height in UNITS")]
        private Vector3 terrainSize = new(10, 5, 10);

        [SerializeField]
        [Tooltip("The dimensions of the grid")]
        private int baseDim = 513;

        [Header("Display Settings")]

        [SerializeField]
        private GameObject foodSprite;

        [SerializeField]
        private Transform spritesParent;

        public bool FlatTerrain 
        { 
            get => flatTerrain; 
            set => flatTerrain = value; 
        }

        public int FoodAmount
        {
            get => foodAmount;
            set => foodAmount = value;
        }

        public int MaxFoodBites
        {
            get => maxFoodBites;
            set => maxFoodBites = value;
        }

        public int BaseDim => baseDim;
        public Vector3 TerrainSize => terrainSize;
        public float MinHeight { get; set; }

        public Food[] Foods { get; private set; } // The points of interest on the grid
        public float[,] Heights { get; set; }     // The height values on all elements of the grid
        public float[,] Pheromones { get; set; }  // The pheromones values on all elements of the grid

        // Setup all grid variables
        public void Generate()
        {
            CreateNodes();

            CreateMatrices();

            //DisplayFoodSprites();
        }

        // Create a collection of points on random positions
        private void CreateNodes()
        {
            Foods = new Food[foodAmount];

            for (int i = 0; i < foodAmount; i++)
            {
                int x = Random.Range(0, baseDim);
                int y = Random.Range(0, baseDim);

                Vector2Int foodCell = new(x, y);

                Foods[i] = new Food(foodCell, maxFoodBites);
            }
        }

        // Create height and pheromone matrices
        private void CreateMatrices()
        {
            Heights = new float[baseDim, baseDim];

            Pheromones = new float[baseDim, baseDim];

            // Initiate terrain as non flat
            if (!flatTerrain)
            {
                for (int i = 0; i < baseDim; i++)
                {
                    for (int j = 0; j < baseDim; j++)
                    {
                        float perlin = Mathf.PerlinNoise(
                                (10f * i) / baseDim,
                                (10f * j) / baseDim);

                        Heights[i, j] = perlin;

                        if (perlin < MinHeight)
                            MinHeight = perlin;
                    }
                }
            }
        }

        // Returns the minimum current height 
        public float GetMinHeight()
        {
            // The value to be returned
            float min = float.MaxValue;

            // Loop through the heightmap as a 1D array
            for (int i = 0; i < baseDim * baseDim; i++)
            {
                // Get the x and y coordinates based on the current index
                int x = i % baseDim;
                int y = i / baseDim;

                // Get the float at the above coordinates
                float current = Heights[y, x];

                // Update the min value if the current float is lower
                if (current < min)
                    min = current;
            }

            return min;
        }

        public void ResetMatrices() => CreateMatrices();

        // Assumes the terrain is in the center of the 3D world
        public Vector3 TexelToVector(Vector2Int texel)
        {
            return new()
            {
                x = ((texel.x / (float)baseDim) * 10f) - 5,
                y = 0.01f,
                z = ((texel.y / (float)baseDim) * 10f) - 5
            };
        }

        public void AddLump(Vector2Int cell, int radius, float height, float power)
        {
            // Ants are supposed to dodge artificial lumps

            // Lump: 380, 201

            float radiusIncrease = Mathf.Pow(radius, power);

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    // Get neighbour coordinates
                    int nx = cell.x + dx,
                        ny = cell.y + dy;

                    // Skip neighbour if coordinates are outside of the available 2D space
                    if (nx < 0 || ny < 0 || nx >= baseDim || ny >= baseDim)
                        continue;

                    // Calculate the outer layer
                    int layer = Mathf.Abs(dx) > Mathf.Abs(dy) ? Mathf.Abs(dx) : Mathf.Abs(dy);

                    // Calculate the height to be added
                    float newHeight = height - ((height * Mathf.Pow(layer, power)) / radiusIncrease);

                    // Update the heightmap
                    Heights[ny, nx] += newHeight;

                    // Define the minimum height (if necessary) to correct
                    // heightmap placement when displaying
                    if (Heights[ny, nx] < MinHeight)
                        MinHeight = Heights[ny, nx];
                }
            }
        }

        // Display sprites representing food at their 3D equivalent location
        private void DisplayFoodSprites()
        {
            // Skip creation if no sprite is given
            if (foodSprite == null)
                return;

            // Instantiate each sprite
            for (int i = 0; i < foodAmount; i++)
            {
                Vector3 spritePos = TexelToVector(Foods[i].Cell);

                Object.Instantiate(foodSprite, spritesParent).transform.position = spritePos;
            }
        }
    }
}
