using UnityEngine;
using System.Collections;
using System.Linq; // used for Sum of array




/* TODO:

    scroll back from main boat follow camera
    
    ECS entity component system
        split into moving mode --> aiming mode --> fire mode

    camera.LookAt(projectile) <--- for following projectile after launch

    on overhead view, make objects more exaggerated so they're easier to see

    Look into UI options
        power 
        height angle
        compass angle
        

    

*/

public class TerrainGenerator : MonoBehaviour
{
    
    public int width = 1024; //x-axis of the terrain 256x256   512x512  1024x1024 reeeee
    public int height = 1024; //z-axis

    public int depth = 20; //y-axis

    public float scale = 35f; //this is just to get bigger/smaller map on the fly


    //Init map and prep noise for terrain layer
    public float noiseScale = 1.2f;

    /*
    public float noiseFrequency = 0.25f;
    public int seed = 10;
    public float fractalLacunarity = 2.0f;
    public float fractalGain = 0.5f;
    public int fractalOctaves = 5;
    */
    [SerializeField]
    private FastNoiseLiteParams noiseParams = new FastNoiseLiteParams();

    FastNoiseLite noise = new FastNoiseLite();

    Terrain terrain;


    // gives us a random noise each time we run
    private void Start()
    {
        terrain = GetComponent<Terrain>(); //the Terrain object
            }
    // Using Update() instead of Start() for testing
    // so can update values in real-time

    public void OnGenerate()
    {
        terrain.terrainData = GenerateTerrain(terrain.terrainData);

    }

    TerrainData GenerateTerrain(TerrainData terrainData)
    {
        ReadNoiseParams();

        // sets initial Terrain data
        terrainData.heightmapResolution = (width + 1);
        terrainData.size = new Vector3(width, depth, height);

        //sets heights with perlin
        terrainData.SetHeights(0, 0, GenerateHeights());

        //add texture depending on height, blue for water, green for ground, white for peaks
        // https://discussions.unity.com/t/how-to-automatically-apply-different-textures-on-terrain-based-on-height/2013
        // https://alastaira.wordpress.com/2013/11/14/procedural-terrain-splatmapping/ this one is better


        // Splatmap data is stored internally as a 3d array of floats, so declare a new empty array ready for your custom splatmap data:
        float[,,] splatmapData = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, terrainData.alphamapLayers];

        for (int y = 0; y < terrainData.alphamapHeight; y++)
        {
            for (int x = 0; x < terrainData.alphamapWidth; x++)
            {
                // Normalise x/y coordinates to range 0-1 
                float y_01 = (float)y / (float)terrainData.alphamapHeight;
                float x_01 = (float)x / (float)terrainData.alphamapWidth;

                // Sample the height at this location (note GetHeight expects int coordinates corresponding to locations in the heightmap array)
                //float height = terrainData.GetHeight(Mathf.RoundToInt(y_01 * terrainData.heightmapHeight), Mathf.RoundToInt(x_01 * terrainData.heightmapWidth));
                float height = terrainData.GetHeight(Mathf.RoundToInt(y_01 * terrainData.heightmapResolution), Mathf.RoundToInt(x_01 * terrainData.heightmapResolution));
                
                // Calculate the normal of the terrain (note this is in normalised coordinates relative to the overall terrain dimensions)
                Vector3 normal = terrainData.GetInterpolatedNormal(y_01, x_01);

                // Calculate the steepness of the terrain
                float steepness = terrainData.GetSteepness(y_01, x_01);

                // Setup an array to record the mix of texture weights at this point
                float[] splatWeights = new float[terrainData.alphamapLayers];

                // CHANGE THE RULES BELOW TO SET THE WEIGHTS OF EACH TEXTURE ON WHATEVER RULES YOU WANT

                /*
                Ok soooo whatever weight is the highest is what it will be colored as.
                For the following group of weights, water would be selected for that area because it got the highest score.
                splatWeights[0] = 0.1f; //brown
                splatWeights[1] = 0.3f; //grass
                splatWeights[2] = 0.5f; //water
                splatWeights[3] = 0.1f; //snow

                The end goal would be:

                snow (cap)          /\
                dirt               /
                grass           __/   
                beach    ______/
                water   /
                _______/
                      
               
                */

                splatWeights[0] = 0.0f;
                splatWeights[1] = 0.0f;
                splatWeights[2] = 0.0f;
                splatWeights[3] = 0.0f;
                       
                // the percent of terrains max height that this area is
                float hm_perc = (height / terrainData.heightmapResolution) *10f;

                Biome(); //sets the biome

                void Biome()
                {
                    // will need to tune further but this will work for the basics now. 
                    // RedBlob had a better implementation of this that might be worth looking into
                    if (hm_perc == 0.0) { splatWeights[2] = 1.0f; return; } //water
                    if (hm_perc < 0.10) { splatWeights[0] = 1.0f; return; } //beach sand
                    if (hm_perc < 0.60) { splatWeights[1] = 1.0f; return; } // grass
                    if (hm_perc >= 0.60) { splatWeights[3] = 1.0f; return; } //snow

                }
                
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

    public void ReadNoiseParams()
    {
        if (noise == null)
            noise = new FastNoiseLite();

        noise.SetSeed(noiseParams.seed);
        noise.SetNoiseType(noiseParams.noiseType);
        noise.SetFractalType(noiseParams.fractalType);
        noise.SetFractalGain(noiseParams.fractalGain);
        noise.SetFractalLacunarity(noiseParams.fractalLacunarity);
        noise.SetFractalOctaves(noiseParams.fractalOctaves);
        noise.SetFrequency(noiseParams.frequency);
    }


    float[,] GenerateHeights()
    {

        //TODO add more layers so we can do multiple passes for more variability in the Perlin noise
        //I think Sebastian talked about it a bit in one of his vids go find it
        //  noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        

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
        //float xCoord = (float)x / width * scale;
        //float yCoord = (float)y / height * scale;

        //TODO fix
        float value = noise.GetNoise(x * noiseScale, y * noiseScale);
        //float value = noise.GetNoise(xCoord * noiseScale, yCoord * noiseScale);
        //float value = noise.GetNoise(xCoord * noiseScale, yCoord * noiseScale) / 2f + .5f; // returns value between 0 and 1 
        value = Mathf.Lerp(-1, 1, value); //Normalize the returned noise
                                          
        return value;
    }
}
