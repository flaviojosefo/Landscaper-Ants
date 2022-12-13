using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour {

    [SerializeField]
    private Terrain terrain;

    [SerializeField]
    private float tileSize = 10.0f;

    private int dim;
    private float[,] heights;

    private void Start() {

        // Get heightmap width/height
        dim = terrain.terrainData.heightmapResolution;
        heights = new float[dim, dim];
    }

    private void GenerateHeightMap() {

        Flatten();

        for (int i = 0; i < dim; i++) {

            for (int j = 0; j < dim; j++) {

                heights[i, j] += Mathf.PerlinNoise(
                    tileSize * i / (float)dim,
                    tileSize * j / (float)dim);
            }
        }

        terrain.terrainData.SetHeights(0, 0, heights);
    }

    public void Flatten() {

        for (int i = 0; i < dim; i++) {

            for (int j = 0; j < dim; j++) {

                heights[i, j] = 0.0f;
            }
        }

        terrain.terrainData.SetHeights(0, 0, heights);
    }

    public void Generate() {
        GenerateHeightMap();
    }

    private void OnApplicationQuit() {
        Flatten();
    }
}
