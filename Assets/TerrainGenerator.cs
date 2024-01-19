using UnityEngine;
using System.Collections;
using System.Linq; // used for Sum of array


public class TerrainGenerator : MonoBehaviour
{
    public int width = 256; //x-axis of the terrain
    public int height = 256; //z-axis

    public int depth = 20; //y-axis

    public float scale = 35f; //this is just to get bigger/smaller map on the fly

    public float offsetX = 100f;
    public float offsetY = 100f;

    //offsetX = Random.Range(0f, 9999f);
    //offsetY = Random.Range(0f, 9999f);

    //Init map and prep noise for terrain layer

    public float noiseScale = 0.9f;
    public float noiseFrequency = 0.25f;
    int seed = 10;

    FastNoiseLite noise = new FastNoiseLite();

    // gives us a random noise each time we run
    private void Start()
    {
        offsetX = Random.Range(0f, 9999f);
        offsetY = Random.Range(0f, 9999f);
    }
    // Using Update() instead of Start() for testing
    // so can update values in real-time

    private void Update()
    {
        Terrain terrain = GetComponent<Terrain>(); //the Terrain object
        terrain.terrainData = GenerateTerrain(terrain.terrainData);
    }

    TerrainData GenerateTerrain(TerrainData terrainData)
    {
        // sets initial Terrain data
        terrainData.heightmapResolution = width + 1;
        terrainData.size = new Vector3(width, depth, height);

        //sets heights with perlin
        terrainData.SetHeights(0, 0, GenerateHeights());

        //add texture depending on height, blue for water, green for ground, white for peaks
        // https://discussions.unity.com/t/how-to-automatically-apply-different-textures-on-terrain-based-on-height/2013
        // https://alastaira.wordpress.com/2013/11/14/procedural-terrain-splatmapping/ this one is better


        float[,,] splatmapData = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, terrainData.alphamapLayers];

        for (int y = 0; y < terrainData.alphamapHeight; y++)
        {
            for (int x = 0; x < terrainData.alphamapWidth; x++)
            {
                // Normalise x/y coordinates to range 0-1 
                float y_01 = (float)y / (float)terrainData.alphamapHeight;
                float x_01 = (float)x / (float)terrainData.alphamapWidth;

                // Sample the height at this location (note GetHeight expects int coordinates corresponding to locations in the heightmap array)
                float height = terrainData.GetHeight(Mathf.RoundToInt(y_01 * terrainData.heightmapResolution), Mathf.RoundToInt(x_01 * terrainData.heightmapResolution));

                // Calculate the normal of the terrain (note this is in normalised coordinates relative to the overall terrain dimensions)
                Vector3 normal = terrainData.GetInterpolatedNormal(y_01, x_01);

                // Calculate the steepness of the terrain
                float steepness = terrainData.GetSteepness(y_01, x_01);

                // Setup an array to record the mix of texture weights at this point
                float[] splatWeights = new float[terrainData.alphamapLayers];

                // CHANGE THE RULES BELOW TO SET THE WEIGHTS OF EACH TEXTURE ON WHATEVER RULES YOU WANT

                // Texture[0] has constant influence
                splatWeights[0] = 0.1f;

                // Texture[1] is stronger at lower altitudes
                //Clamp01 returns 0 if neg, 1 if > 1.

                splatWeights[1] = Mathf.Clamp01((terrainData.heightmapResolution - height)); 
                Debug.Log(Mathf.Clamp01((terrainData.heightmapResolution - height)));

                 // Texture[2] stronger on flatter terrain
                 // Note "steepness" is unbounded, so we "normalise" it by dividing by the extent of heightmap height and scale factor
                 // Subtract result from 1.0 to give greater weighting to flat surfaces
                 splatWeights[2] = 1.0f - Mathf.Clamp01(steepness * steepness / (terrainData.heightmapResolution / 5.0f));

                 // Texture[3] increases with height but only on surfaces facing positive Z axis 
                 splatWeights[3] = height * Mathf.Clamp01(normal.z);

                // Sum of all textures weights must add to 1, so calculate normalization factor from sum of weights
                float z = splatWeights.Sum();

                // Loop through each terrain texture
                for (int i = 0; i < terrainData.alphamapLayers; i++)
                {

                    // Normalize so that sum of all texture weights = 1
                    splatWeights[i] /= z;

                    // Assign this point to the splatmap array
                    splatmapData[x, y, i] = splatWeights[i];
                }
            }
        }

        // Finally assign the new splatmap to the terrainData:
        terrainData.SetAlphamaps(0, 0, splatmapData);
        return terrainData;
    }

    float[,] GenerateHeights()
    {
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFrequency(noiseFrequency);
        noise.SetSeed(seed);

        float[,] heights = new float[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                heights[x, y] = CalculateHeight(x, y);
            }
        }

        return heights;
    }

    float CalculateHeight(int x, int y)
    {
        float xCoord = (float)x / width * scale + offsetX;
        float yCoord = (float)y / height * scale + offsetY;

        float value = noise.GetNoise(xCoord * noiseScale, yCoord * noiseScale);

        return value;
    }
}
