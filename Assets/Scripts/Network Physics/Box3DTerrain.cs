using Box3D; // Adjust based on your wrapper's specific namespace (e.g., Suvitruf.Box3D)
using UnityEngine;
using static Unity.U2D.Physics.PhysicsBody;

public class UnityTerrainToBox3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Terrain unityTerrain;

    // Keep track of your allocated resources
    private HeightField terrainHeightField;
    private Body terrainBody;

    public void BuildBox3DTerrain(World targetWorld)
    {
       
        if (unityTerrain == null)
        {
            unityTerrain = Terrain.activeTerrain;
            if (unityTerrain == null) return;
        }

        TerrainData terrainData = unityTerrain.terrainData;

        // 1. Get exact grid resolution dimensions from Unity
        // Note: heightmapResolution is usually (2^n) + 1 (e.g., 513x513)
        int resolutionX = terrainData.heightmapResolution;
        int resolutionZ = terrainData.heightmapResolution;

        // 2. Extract Unity heights (Returns normalized values from 0.0 to 1.0)
        float[,] unityHeights = terrainData.GetHeights(0, 0, resolutionX, resolutionZ);

        // 3. Flatten into a 1D float array matching Box3D's continuous memory layout
        float[] flatHeights = new float[resolutionX * resolutionZ];
        Vector3 terrainSize = terrainData.size;

        for (int z = 0; z < resolutionZ; z++)
        {
            for (int x = 0; x < resolutionX; x++)
            {
                // Multiply the normalized float by the full Y-height of the terrain bounds
                float absoluteHeight = unityHeights[z, x] * terrainSize.y;

                // Box3D evaluates array indexing flatly: [z * countX + x]
                flatHeights[z * resolutionX + x] = absoluteHeight;
            }
        }

        // 4. Calculate exact spacing scale per grid cell
        // Spacing = Total physical metric size / (number of data points - 1)
        float cellSpacingX = terrainSize.x / (resolutionX - 1);
        float cellSpacingZ = terrainSize.z / (resolutionZ - 1);


        // Create the underlying height field asset record 
        terrainHeightField = HeightField.Create(flatHeights, resolutionX, resolutionZ, new Vector3(cellSpacingX, 1.0f, cellSpacingZ), 0.0f, terrainSize.y);

        // 6. Spawn the matching Static Rigidbody Container
        BodyDef groundDef = BodyDef.Default;
        groundDef.Type = Box3D.BodyType.Static; // Terrain must always remain static
        terrainBody = targetWorld.CreateBody(groundDef); 

        // Align the physics body exactly with the absolute bottom-left corner of the Unity Terrain asset
        Vector3 terrainWorldPos = unityTerrain.transform.position;
        terrainBody.SetTransform(new B3Pos(terrainWorldPos.x, terrainWorldPos.y, terrainWorldPos.z), Quaternion.identity);


        // 7. Bind the custom shape definition to the body
        terrainBody.CreateHeightFieldShape(ShapeDef.Default, terrainHeightField);

        Debug.Log($"Successfully baked Unity Terrain '{unityTerrain.name}' into Box3D Height Field!");
    }

    private void OnDestroy()
    {
        // Clean up the registered height field if the component is unloaded
        if (terrainHeightField.IsCreated)
        {
            terrainHeightField.Destroy();
        }
    }
}
