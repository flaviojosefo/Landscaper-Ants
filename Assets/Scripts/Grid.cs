using System;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace LandscaperAnts {

    [Serializable]
    public sealed class Grid {

        [Header("Grid Generation Settings")]

        [SerializeField]
        [Tooltip("Controls wether the terrain will start as a float surface or with Perlin Noise applied")]
        private bool flatTerrain = true;

        [SerializeField]
        [Range(1, 100)]
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

        private Food[] foods;            // The points of interest on the grid
        private float[,] heights;        // The height values on all elements of the grid
        private float[,] pheromones;     // The pheromones values on all elements of the grid

        public int BaseDim => baseDim;
        public Vector3 TerrainSize => terrainSize;
        public float MinHeight { get; set; }

        public Food[] Foods => foods;
        public float[,] Heights => heights;
        public float[,] Pheromones => pheromones;

        // Setup all grid variables
        public void Generate() {

            CreateNodes();

            CreateMatrices();

            DisplayFoodSprites();
        }

        // Create a collection of points on random positions
        private void CreateNodes() {

            foods = new Food[foodAmount];

            for (int i = 0; i < foodAmount; i++) {

                int x = Random.Range(0, baseDim);
                int y = Random.Range(0, baseDim);

                Vector2Int foodCell = new(x, y);

                foods[i] = new Food(foodCell, maxFoodBites);
            }
        }

        // Create height and pheromone matrices
        private void CreateMatrices() {

            heights = new float[baseDim, baseDim];

            pheromones = new float[baseDim, baseDim];

            // Initiate terrain as non flat
            if (!flatTerrain) {

                for (int i = 0; i < baseDim; i++) {

                    for (int j = 0; j < baseDim; j++) {

                        heights[i, j] = Mathf.PerlinNoise(
                                (10f * i) / baseDim,
                                (10f * j) / baseDim);
                    }
                }
            }
        }

        // Get a node's neighbours' indices
        public int[] GetNeighbours(int node) {

            // Neighbours are all points which don't correspond to the node itself
            int[] neighbours = new int[foodAmount - 1];

            // Find neighbours of 'node' based on index
            for (int i = 0; i < foodAmount; i++) {

                // node = "from" | i = "to"
                if (node != i) {

                    neighbours[i] = i;
                }
            }

            return neighbours;
        }

        // Returns the minimum current height 
        public float GetMinHeight() {

            // The value to be returned
            float min = float.MaxValue;

            // Loop through the heightmap as a 1D array
            for (int i = 0; i < baseDim * baseDim; i++) {

                // Get the x and y coordinates based on the current index
                int x = i % baseDim;
                int y = i / baseDim;

                // Get the float at the above coordinates
                float current = heights[y, x];

                // Update the min value if the current float is lower
                if (current < min)
                    min = current;
            }

            return min;
        }

        public void ResetMatrices() => CreateMatrices();

        // Assumes the terrain is in the center of the 3D world
        public Vector3 TexelToVector(Vector2Int texel) {

            return new() {
                x = ((texel.x / (float)baseDim) * 10f) - 5,
                y = 0.01f,
                z = ((texel.y / (float)baseDim) * 10f) - 5
            };
        }

        // Display sprites representing food at their 3D equivalent location
        private void DisplayFoodSprites() {

            // Skip creation if no sprite is given
            if (foodSprite is null)
                return;

            // Instantiate each sprite
            for (int i = 0; i < foodAmount; i++) {

                Vector3 spritePos = TexelToVector(foods[i].Cell);

                Object.Instantiate(foodSprite, spritesParent).transform.position = spritePos;
            }
        }
    }
}
