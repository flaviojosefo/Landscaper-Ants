using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LandscaperAnts {

    [Serializable]
    public sealed class Grid {

        [Header("Nodes Generation Settings")]

        [SerializeField]
        [Range(2, 100)]
        [Tooltip("The amount of nodes to generate")]
        private int nodesAmount = 4;

        [SerializeField]
        [Tooltip("The dimensions of the grid")]
        private int baseDim = 513;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("The initial pheromone levels on all grid elements")]
        private float initPheromones = 0f;

        private Vector2Int[] foodCells;  // The points of interest on the grid
        private float[,] heights;    // The height values on all elements of the grid
        private float[,] pheromones; // The pheromones values on all elements of the grid

        public int BaseDim => baseDim;

        public Vector2Int[] FoodCells => foodCells;
        public float[,] Heights => heights;
        public float[,] Pheromones => pheromones;

        // Setup all grid variables
        public void Generate() {

            CreateNodes();

            CreateMatrices();
        }

        // Create a collection of points on random positions
        private void CreateNodes() {

            foodCells = new Vector2Int[nodesAmount];

            for (int i = 0; i < nodesAmount; i++) {

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

                    // Set inital height
                    heights[i, j] = 1.0f;

                    // Set initial pheromone amount
                    pheromones[i, j] = initPheromones;
                }
            }
        }

        // Get a node's neighbours' indices
        public int[] GetNeighbours(int node) {

            // Neighbours are all points which don't correspond to the node itself
            int[] neighbours = new int[nodesAmount - 1];

            // Find neighbours of 'node' based on index
            for (int i = 0; i < nodesAmount; i++) {

                // node = "from" | i = "to"
                if (node != i) {

                    neighbours[i] = i;
                }
            }

            return neighbours;
        }

        public void ResetMatrices() => CreateMatrices();
    }
}
