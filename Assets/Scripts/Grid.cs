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
        [Range(2, 100)]
        [Tooltip("The amount of food to generate")]
        private int foodAmount = 4;

        [SerializeField]
        [Tooltip("The dimensions of the grid")]
        private int baseDim = 513;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("The initial pheromone levels on all grid elements")]
        private float initPheromones = 0f;

        [Header("Display Settings")]

        [SerializeField]
        private GameObject foodSprite;

        [SerializeField]
        private Transform spritesParent;

        private Vector2Int[] foodCells;  // The points of interest on the grid
        private float[,] heights;        // The height values on all elements of the grid
        private float[,] pheromones;     // The pheromones values on all elements of the grid

        public int BaseDim => baseDim;

        public Vector2Int[] FoodCells => foodCells;
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

            foodCells = new Vector2Int[foodAmount];

            for (int i = 0; i < foodAmount; i++) {

                int x = Random.Range(0, baseDim);
                int y = Random.Range(0, baseDim);

                foodCells[i] = new Vector2Int(x, y);
            }
        }

        // Create height and pheromone matrices
        private void CreateMatrices() {

            heights = new float[baseDim, baseDim];

            pheromones = new float[baseDim, baseDim];

            for (int i = 0; i < baseDim; i++) {

                for (int j = 0; j < baseDim; j++) {

                    // Set initial height
                    if (flatTerrain) {

                        heights[i, j] = 1.0f;

                    } else {

                        heights[i, j] = Mathf.PerlinNoise(
                            (10f * i) / baseDim,
                            (10f * j) / baseDim);
                    }

                    // Set initial pheromone amount
                    pheromones[i, j] = initPheromones;
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

        public void ResetMatrices() => CreateMatrices();

        // Assumes the terrain is in the center of the 3D world
        public Vector3 TexelToVector(Vector2Int texel) {

            return new() {
                x = ((texel.x / (float)baseDim) * 10f) - 5,
                y = 1.01f,
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

                Vector3 spritePos = TexelToVector(foodCells[i]);

                Object.Instantiate(foodSprite, spritesParent).transform.position = spritePos;
            }
        }
    }
}
