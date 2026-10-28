using UnityEngine;

// The look of the mountains (SB_TerrainSnowCommon.cginc, SBCliffs): bare rock on the steep slopes, laid on from the
// side so it is not stretched, with big patches and layers of stone. This gives the terrain shader its rock picture
// (the terrain's own rock layer). Called by SnowCover when the game starts.
public static class TerrainLook
{
    // metres for one repeat of the rock picture on the cliffs
    public const float RockTile = 9f;
    public static readonly Color RockTint = new Color(1.16f, 1.03f, 0.84f);

    public static void Apply(Terrain terrain)
    {
        Texture rock = null, bumps = null;
        if (terrain != null && terrain.terrainData != null)
        {
            // the best rock picture first (Rock_TerrainLayer), then any other rock layer
            foreach (var layer in terrain.terrainData.terrainLayers)
                if (layer != null && layer.diffuseTexture != null && layer.name.StartsWith("Rock_")) { rock = layer.diffuseTexture; bumps = layer.normalMapTexture; break; }
            if (rock == null)
                foreach (var layer in terrain.terrainData.terrainLayers)
                    if (layer != null && layer.diffuseTexture != null && layer.name.ToLowerInvariant().Contains("rock")) { rock = layer.diffuseTexture; bumps = layer.normalMapTexture; break; }
        }
        Shader.SetGlobalTexture("_SB_CliffRock", rock != null ? rock : Texture2D.grayTexture);
        Shader.SetGlobalFloat("_SB_CliffTile", rock != null ? RockTile : 0f);
        Shader.SetGlobalColor("_SB_CliffTint", RockTint);
        Shader.SetGlobalTexture("_SB_CliffRockNormal", bumps != null ? bumps : Texture2D.grayTexture);
        Shader.SetGlobalFloat("_SB_CliffBump", bumps != null ? 1.3f : 0f);
    }
}
