using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Tools > Island > Build Island And Ocean
//   Makes the map an island: a new terrain twice as wide with the whole old map copied in the middle (nothing is
//   cut), new land around it up to an irregular oval, a sand beach and a sea floor, plus the sea all around
//   (Ocean + OceanSafety). The old terrain asset is not changed, it stays as a backup.
//   Running it again rebuilds the island from the old terrain.
// Tools > Island > Restore Original Terrain
//   Puts the old terrain back and removes the sea.
// Also makes the sand texture and its terrain layer (Assets/Island/Sand/TerrainSand), so it can be painted by hand.
public static class IslandTool
{
    const string Folder = "Assets/Island";
    const string TerrainPath = Folder + "/IslandTerrain.asset";
    const string SourceInfoPath = Folder + "/IslandSource.json";
    const string SandFolder = Folder + "/Sand";
    const string SandDiffusePath = SandFolder + "/Sand_Diffuse.png";
    const string SandNormalPath = SandFolder + "/Sand_Normal.png";
    const string SandLayerPath = SandFolder + "/TerrainSand.terrainlayer";
    const string OceanFolder = Folder + "/Ocean";
    const string WaveNormalPath = OceanFolder + "/OceanWaves_Normal.png";
    const string FoamPath = OceanFolder + "/OceanFoam.png";
    const string OceanMaterialPath = OceanFolder + "/IslandOcean.mat";
    const string OceanShaderName = "SurvivalBD/Island Ocean";

    // shape of the island (metres)
    const float SeaBelowLand = 1.2f;          // sea level under the flat land
    const float BeachWidth = 28f;             // beach from the waterline to the grass
    const float LandBlend = 25f;              // the land melts into the beach over this distance
    const float SeaDepth = 12f;               // deepest sea floor
    const float MarginOutsideOldMap = 40f;    // the waterline stays at least this far outside the old map
    const float ExtendFade = 110f;            // hills cut by the old map's edge fade out over this distance
    const float OvalLength = 840f;            // half length of the oval
    const float OvalWidth = 730f;             // half width of the oval
    const float OvalTurn = 25f;               // degrees
    const float MaxShoreRadius = 880f;        // leaves room for the sea floor inside the terrain
    const float CameraFar = 2500f;            // the island is ~1.7 km across, the camera must see all of it
    const int ShapeSamples = 720;

    [Serializable]
    class SourceInfo
    {
        public string terrainDataPath;
        public Vector3 position;
        public float cameraFar;
    }

    [MenuItem("Tools/Island/Build Island And Ocean", false, 1)]
    static void BuildMenu()
    {
        if (!EditorUtility.DisplayDialog("Island",
                "Make the map an island surrounded by sea?\n\nThe old map is copied into a new, bigger terrain (nothing is cut). " +
                "The old terrain asset stays as a backup: Tools > Island > Restore Original Terrain puts it back.", "Build", "Cancel")) return;
        if (Build(false, out var message)) EditorUtility.DisplayDialog("Island", message + "\n\nSave the scene (Ctrl+S) to keep it.", "OK");
        else EditorUtility.DisplayDialog("Island", message, "OK");
    }

    [MenuItem("Tools/Island/Restore Original Terrain", false, 20)]
    static void RestoreMenu()
    {
        if (Restore(out var message)) EditorUtility.DisplayDialog("Island", message + "\n\nSave the scene (Ctrl+S) to keep it.", "OK");
        else EditorUtility.DisplayDialog("Island", message, "OK");
    }

    public static bool Build(bool remakeTextures, out string message)
    {
        var terrain = Object.FindObjectOfType<Terrain>();
        if (terrain == null || terrain.terrainData == null)
        {
            message = "No terrain found in the open scene.";
            return false;
        }

        // always build from the original terrain, also when the island was made before
        var info = LoadSourceInfo();
        var islandAsset = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        TerrainData source;
        Vector3 sourcePosition;
        var camera = FindMainCamera();
        if (info != null && islandAsset != null && terrain.terrainData == islandAsset)
        {
            source = AssetDatabase.LoadAssetAtPath<TerrainData>(info.terrainDataPath);
            sourcePosition = info.position;
            if (source == null)
            {
                message = "The original terrain (" + info.terrainDataPath + ") is missing, so the island cannot be rebuilt.";
                return false;
            }
        }
        else
        {
            source = terrain.terrainData;
            sourcePosition = terrain.transform.position;
            info = new SourceInfo
            {
                terrainDataPath = AssetDatabase.GetAssetPath(source),
                position = sourcePosition,
                cameraFar = camera != null ? camera.farClipPlane : 1000f,
            };
        }
        if (AssetDatabase.GetAssetPath(source) == TerrainPath)
        {
            message = "The terrain already uses the island, but the original terrain is unknown.";
            return false;
        }
        if (Mathf.Abs(source.size.x - source.size.z) > 0.01f)
        {
            message = "Only square terrains are supported.";
            return false;
        }

        EnsureFolder(Folder);
        EnsureFolder(SandFolder);
        EnsureFolder(OceanFolder);
        File.WriteAllText(SourceInfoPath, JsonUtility.ToJson(info, true));
        AssetDatabase.ImportAsset(SourceInfoPath);

        var sand = CreateSandLayer(remakeTextures);
        var oceanMaterial = CreateOceanMaterial(remakeTextures);
        if (sand == null || oceanMaterial == null)
        {
            message = "Could not make the sand layer or the sea material (see the Console).";
            return false;
        }

        var island = BuildTerrainData(source, sourcePosition, sand, out var islandPosition, out var centre, out var seaLevel, out var shore, out var report);
        if (island == null)
        {
            message = report;
            return false;
        }

        // put it in the scene
        Undo.SetCurrentGroupName("Build Island And Ocean");
        var group = Undo.GetCurrentGroup();
        var collider = terrain.GetComponent<TerrainCollider>();
        Undo.RecordObject(terrain, "Build Island");
        Undo.RecordObject(terrain.transform, "Build Island");
        if (collider != null) Undo.RecordObject(collider, "Build Island");
        terrain.terrainData = island;
        if (collider != null) collider.terrainData = island;
        terrain.transform.position = islandPosition;
        terrain.Flush();

        var ocean = Object.FindObjectOfType<Ocean>(true);
        if (ocean == null)
        {
            var go = new GameObject("Ocean");
            Undo.RegisterCreatedObjectUndo(go, "Build Island");
            var water = LayerMask.NameToLayer("Water");
            go.layer = water >= 0 ? water : 0;
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            ocean = go.AddComponent<Ocean>();
        }
        Undo.RecordObject(ocean, "Build Island");
        Undo.RecordObject(ocean.transform, "Build Island");
        ocean.transform.SetParent(null);
        ocean.transform.SetPositionAndRotation(new Vector3(centre.x, seaLevel, centre.y), Quaternion.identity);
        ocean.transform.localScale = Vector3.one;
        ocean.shoreRadius = shore;
        var oceanRenderer = ocean.GetComponent<MeshRenderer>();
        Undo.RecordObject(oceanRenderer, "Build Island");
        oceanRenderer.sharedMaterial = oceanMaterial;
        if (ocean.GetComponent<OceanSafety>() == null) Undo.AddComponent<OceanSafety>(ocean.gameObject);
        ocean.enabled = false;
        ocean.enabled = true; // rebuild the water mesh

        if (camera != null && camera.farClipPlane < CameraFar)
        {
            Undo.RecordObject(camera, "Build Island");
            camera.farClipPlane = CameraFar;
        }
        Undo.CollapseUndoOperations(group);

        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        AssetDatabase.SaveAssets();
        message = "The island and the sea are ready.\n\n" + report;
        Debug.Log("[Island] " + message.Replace("\n\n", "\n"));
        return true;
    }

    public static bool Restore(out string message)
    {
        var info = LoadSourceInfo();
        var terrain = Object.FindObjectOfType<Terrain>();
        if (info == null || terrain == null)
        {
            message = "Nothing to restore (no island was built here).";
            return false;
        }
        var source = AssetDatabase.LoadAssetAtPath<TerrainData>(info.terrainDataPath);
        if (source == null)
        {
            message = "The original terrain (" + info.terrainDataPath + ") is missing.";
            return false;
        }

        Undo.SetCurrentGroupName("Restore Original Terrain");
        var group = Undo.GetCurrentGroup();
        var collider = terrain.GetComponent<TerrainCollider>();
        Undo.RecordObject(terrain, "Restore Terrain");
        Undo.RecordObject(terrain.transform, "Restore Terrain");
        if (collider != null) Undo.RecordObject(collider, "Restore Terrain");
        terrain.terrainData = source;
        if (collider != null) collider.terrainData = source;
        terrain.transform.position = info.position;
        terrain.Flush();
        foreach (var ocean in Object.FindObjectsOfType<Ocean>(true)) Undo.DestroyObjectImmediate(ocean.gameObject);
        var camera = FindMainCamera();
        if (camera != null && info.cameraFar > 0f)
        {
            Undo.RecordObject(camera, "Restore Terrain");
            camera.farClipPlane = info.cameraFar;
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        message = "The original terrain is back and the sea was removed.";
        return true;
    }

    // ---------------------------------------------------------------- terrain

    static TerrainData BuildTerrainData(TerrainData source, Vector3 sourcePosition, TerrainLayer sand,
        out Vector3 islandPosition, out Vector2 centre, out float seaLevel, out float[] shore, out string report)
    {
        var sRes = source.heightmapResolution;
        var last = sRes - 1;
        var sSize = source.size;
        var cell = sSize.x / last;
        var offset = last / 2;                   // the old map starts this many cells in
        var nRes = last * 2 + 1;
        var nWidth = sSize.x * 2f;
        centre = new Vector2(sourcePosition.x + sSize.x * 0.5f, sourcePosition.z + sSize.z * 0.5f);
        var cornerX = centre.x - nWidth * 0.5f;
        var cornerZ = centre.y - nWidth * 0.5f;

        // the old ground in world heights
        var raw = source.GetHeights(0, 0, sRes, sRes);
        var old = new float[sRes, sRes];
        var highest = float.MinValue;
        for (var z = 0; z < sRes; z++)
        {
            for (var x = 0; x < sRes; x++)
            {
                old[z, x] = raw[z, x] * sSize.y + sourcePosition.y;
                highest = Mathf.Max(highest, old[z, x]);
            }
        }

        var plain = FindPlainLevel(old, sourcePosition.y);
        var filled = FillLowEdge(old, plain);
        var sea = plain - SeaBelowLand;
        seaLevel = sea;
        var bottom = sea - SeaDepth - 1.5f;
        var height = Mathf.Max(sSize.y, highest - bottom + 10f);
        islandPosition = new Vector3(cornerX, bottom, cornerZ);

        float Land(int i, int j)
        {
            var ox = i - offset;
            var oz = j - offset;
            if (ox >= 0 && ox <= last && oz >= 0 && oz <= last) return old[oz, ox];

            // outside the old map: mirror its edge and let it fade into gently rolling plain
            var dx = ox < 0 ? -ox : ox > last ? ox - last : 0;
            var dz = oz < 0 ? -oz : oz > last ? oz - last : 0;
            var d = cell * Mathf.Sqrt(dx * dx + dz * dz);
            var rise = Mathf.Max(old[Mirror(oz, last), Mirror(ox, last)] - plain, -0.3f);
            var wx = cornerX + i * cell;
            var wz = cornerZ + j * cell;
            return plain + rise * (1f - Smooth01(d / ExtendFade)) + Mounds(wx, wz) * Smooth01(d / 80f);
        }

        float LandAt(float wx, float wz)
        {
            var i = Mathf.Clamp(Mathf.RoundToInt((wx - cornerX) / cell), 0, nRes - 1);
            var j = Mathf.Clamp(Mathf.RoundToInt((wz - cornerZ) / cell), 0, nRes - 1);
            return Land(i, j);
        }

        // the waterline: an irregular oval that keeps all of the old map (and any hills near its edge) on land
        var shape = new float[ShapeSamples];
        var needed = new float[ShapeSamples];
        var turn = OvalTurn * Mathf.Deg2Rad;
        for (var k = 0; k < ShapeSamples; k++)
        {
            var angle = k * 2f * Mathf.PI / ShapeSamples;
            var u = angle - turn;
            var oval = 1f / Mathf.Sqrt(Sq(Mathf.Cos(u) / OvalLength) + Sq(Mathf.Sin(u) / OvalWidth));
            var wobble = 1f + 0.045f * Mathf.Sin(2f * angle + 1.3f) + 0.035f * Mathf.Sin(3f * angle + 0.4f)
                         + 0.025f * Mathf.Sin(5f * angle + 2.1f) + 0.015f * Mathf.Sin(7f * angle + 4.0f) + 0.01f * Mathf.Sin(11f * angle + 0.7f);
            shape[k] = oval * wobble;

            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            var edge = sSize.x * 0.5f / Mathf.Max(Mathf.Abs(cos), Mathf.Abs(sin));
            var need = edge + MarginOutsideOldMap;
            for (var r = edge - 60f; r < edge + ExtendFade + 40f; r += cell)
            {
                var rise = LandAt(centre.x + cos * r, centre.y + sin * r) - plain;
                if (rise > 0.5f) need = Mathf.Max(need, r + BeachWidth + rise * 0.6f);
            }
            needed[k] = need;
        }
        needed = Blur(MaxFilter(needed, 8), 8);
        shore = new float[ShapeSamples];
        var pushedOut = 0;
        for (var k = 0; k < ShapeSamples; k++)
        {
            var r = SoftMin(SoftMax(shape[k], needed[k], 40f), MaxShoreRadius, 30f);
            if (needed[k] > shape[k]) pushedOut++;
            shore[k] = Mathf.Max(r, needed[k]);
        }
        shore = Blur(shore, 3);
        for (var k = 0; k < ShapeSamples; k++) shore[k] = Mathf.Max(shore[k], needed[k]);
        var shoreTable = shore;
        var mid = centre;

        float DistanceFromShore(float wx, float wz)
        {
            var x = wx - mid.x;
            var z = wz - mid.y;
            var f = Mathf.Repeat(Mathf.Atan2(z, x) / (2f * Mathf.PI), 1f) * ShapeSamples;
            var k = Mathf.FloorToInt(f);
            var r = Mathf.Lerp(shoreTable[k % ShapeSamples], shoreTable[(k + 1) % ShapeSamples], f - k);
            return Mathf.Sqrt(x * x + z * z) - r;
        }

        // heights
        var heights = new float[nRes, nRes];
        var lowest = float.MaxValue;
        for (var j = 0; j < nRes; j++)
        {
            for (var i = 0; i < nRes; i++)
            {
                var wx = cornerX + i * cell;
                var wz = cornerZ + j * cell;
                var s = DistanceFromShore(wx, wz);
                float h;
                if (s >= 0f)
                {
                    // sea floor: shallow for the first ~15 m, then dropping to the deep sea
                    h = sea - SeaFloorDepth(s) + (Mathf.PerlinNoise(wx / 23f + 5.1f, wz / 23f + 9.3f) - 0.5f) * 0.6f * Smooth01((s - 8f) / 30f);
                }
                else
                {
                    var beach = sea + (plain - sea) * Smooth01(-s / BeachWidth);
                    var t = Smooth01((-s - BeachWidth * 0.5f) / (BeachWidth * 0.5f + LandBlend));
                    h = t >= 1f ? Land(i, j) : Mathf.Lerp(beach, Land(i, j), t);
                }
                lowest = Mathf.Min(lowest, h);
                heights[j, i] = Mathf.Clamp01((h - bottom) / height);
            }
        }

        // paint: the old map in the middle, grass on the new land, sand on the beach and the sea floor
        var oldLayers = source.terrainLayers;
        var layers = new List<TerrainLayer>(oldLayers);
        var sandIndex = layers.IndexOf(sand);
        if (sandIndex < 0)
        {
            layers.Add(sand);
            sandIndex = layers.Count - 1;
        }
        var sAlpha = source.alphamapResolution;
        var nAlpha = sAlpha * 2;
        var alphaOffset = sAlpha / 2;
        var alphaCell = sSize.x / sAlpha;
        var oldAlpha = source.GetAlphamaps(0, 0, sAlpha, sAlpha);
        var grass = MostUsedLayer(oldAlpha);
        var alpha = new float[nAlpha, nAlpha, layers.Count];
        for (var j = 0; j < nAlpha; j++)
        {
            for (var i = 0; i < nAlpha; i++)
            {
                int ox = i - alphaOffset, oz = j - alphaOffset;
                if (ox >= 0 && ox < sAlpha && oz >= 0 && oz < sAlpha)
                {
                    for (var l = 0; l < oldLayers.Length; l++) alpha[j, i, l] = oldAlpha[oz, ox, l];
                }
                else
                {
                    alpha[j, i, grass] = 1f;
                }

                var wx = cornerX + (i + 0.5f) * alphaCell;
                var wz = cornerZ + (j + 0.5f) * alphaCell;
                var sandWeight = SandWeight(DistanceFromShore(wx, wz), wx, wz);
                if (sandWeight <= 0f) continue;
                for (var l = 0; l < layers.Count; l++) alpha[j, i, l] *= 1f - sandWeight;
                alpha[j, i, sandIndex] += sandWeight;
            }
        }

        // create or update the asset (same file every time, so the scene keeps pointing at it)
        var island = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        if (island == null)
        {
            island = new TerrainData();
            AssetDatabase.CreateAsset(island, TerrainPath);
        }
        island.heightmapResolution = nRes;
        island.size = new Vector3(nWidth, height, nWidth);
        island.SetHeights(0, 0, heights);
        island.alphamapResolution = nAlpha;
        island.baseMapResolution = source.baseMapResolution;
        island.terrainLayers = layers.ToArray();
        island.SetAlphamaps(0, 0, alpha);

        // grass and flowers of the old map (none on the sand)
        var sDetail = source.detailResolution;
        island.detailPrototypes = source.detailPrototypes;
        island.SetDetailResolution(sDetail * 2, source.detailResolutionPerPatch);
        island.wavingGrassAmount = source.wavingGrassAmount;
        island.wavingGrassSpeed = source.wavingGrassSpeed;
        island.wavingGrassStrength = source.wavingGrassStrength;
        island.wavingGrassTint = source.wavingGrassTint;
        var detailCell = sSize.x / sDetail;
        for (var layer = 0; layer < source.detailPrototypes.Length; layer++)
        {
            var details = source.GetDetailLayer(0, 0, sDetail, sDetail, layer);
            for (var z = 0; z < sDetail; z++)
            {
                for (var x = 0; x < sDetail; x++)
                {
                    if (details[z, x] == 0) continue;
                    var wx = sourcePosition.x + (x + 0.5f) * detailCell;
                    var wz = sourcePosition.z + (z + 0.5f) * detailCell;
                    if (SandWeight(DistanceFromShore(wx, wz), wx, wz) > 0.2f) details[z, x] = 0;
                }
            }
            island.SetDetailLayer(sDetail / 2, sDetail / 2, layer, details);
        }

        // trees painted on the terrain (if any)
        island.treePrototypes = source.treePrototypes;
        var trees = source.treeInstances;
        for (var t = 0; t < trees.Length; t++)
        {
            var p = trees[t].position;
            trees[t].position = new Vector3((p.x * sSize.x + sSize.x * 0.5f) / nWidth, 0f, (p.z * sSize.z + sSize.z * 0.5f) / nWidth);
        }
        island.SetTreeInstances(trees, true);

        // holes (if any)
        var holeRes = source.holesResolution;
        var holes = source.GetHoles(0, 0, holeRes, holeRes);
        var anyHole = false;
        foreach (var solid in holes) anyHole |= !solid;
        if (anyHole) island.SetHoles(holeRes / 2, holeRes / 2, holes);

        EditorUtility.SetDirty(island);

        var minShore = float.MaxValue;
        var maxShore = 0f;
        foreach (var r in shore)
        {
            minShore = Mathf.Min(minShore, r);
            maxShore = Mathf.Max(maxShore, r);
        }
        report = $"Terrain: {nWidth:F0} x {nWidth:F0} m (was {sSize.x:F0} x {sSize.z:F0} m, copied in the middle).\n" +
                 $"Flat land at {plain:F2} m, sea level {sea:F2} m, sea floor down to {lowest:F2} m.\n" +
                 $"Waterline {minShore:F0}-{maxShore:F0} m from the middle; low edge filled: {filled} points; shore pushed out for hills in {pushedOut} of {ShapeSamples} directions.";
        return island;
    }

    static float SeaFloorDepth(float s)
    {
        var t = Smooth01((s - 12f) / 78f);
        return 0.085f * s * (1f - t) + SeaDepth * t;
    }

    static float SandWeight(float s, float wx, float wz)
    {
        var jitter = (Mathf.PerlinNoise(wx / 14f + 3.7f, wz / 14f + 1.9f) - 0.5f) * 8f + (Mathf.PerlinNoise(wx / 4.5f + 7.1f, wz / 4.5f + 2.3f) - 0.5f) * 3f;
        return Smooth01((s + BeachWidth + 4f + jitter) / 10f);
    }

    // low rolling hills on the new land (0 .. ~2 m)
    static float Mounds(float wx, float wz)
    {
        var n = 0.6f * Mathf.PerlinNoise(wx / 220f + 13.1f, wz / 220f + 7.7f) + 0.4f * Mathf.PerlinNoise(wx / 90f + 3.3f, wz / 90f + 11.9f);
        return 2.2f * Smooth01((n - 0.42f) / 0.4f);
    }

    // the most common height of the map (the big flat plain)
    static float FindPlainLevel(float[,] heights, float terrainBottom)
    {
        var counts = new Dictionary<int, int>();
        foreach (var h in heights)
        {
            if (h <= terrainBottom + 0.2f) continue;
            var key = Mathf.FloorToInt(h * 10f);
            counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;
        }
        var best = 0;
        var bestCount = -1;
        foreach (var pair in counts)
        {
            if (pair.Value <= bestCount) continue;
            best = pair.Key;
            bestCount = pair.Value;
        }
        return (best + 0.5f) / 10f;
    }

    // The old map is lower all along its edge; raise that low band (only the part connected to the edge,
    // so the lake stays) to the plain, then smooth the seam.
    static int FillLowEdge(float[,] heights, float plain)
    {
        var res = heights.GetLength(0);
        var low = new bool[res, res];
        var stack = new Stack<Vector2Int>();
        for (var k = 0; k < res; k++)
        {
            foreach (var p in new[] { new Vector2Int(k, 0), new Vector2Int(k, res - 1), new Vector2Int(0, k), new Vector2Int(res - 1, k) })
            {
                if (low[p.y, p.x] || heights[p.y, p.x] >= plain - 0.3f) continue;
                low[p.y, p.x] = true;
                stack.Push(p);
            }
        }
        var count = 0;
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            count++;
            foreach (var n in new[] { p + Vector2Int.right, p + Vector2Int.left, p + Vector2Int.up, p + Vector2Int.down })
            {
                if (n.x < 0 || n.y < 0 || n.x >= res || n.y >= res || low[n.y, n.x] || heights[n.y, n.x] >= plain - 0.3f) continue;
                low[n.y, n.x] = true;
                stack.Push(n);
            }
        }

        var near = new bool[res, res];
        for (var z = 0; z < res; z++)
        {
            for (var x = 0; x < res; x++)
            {
                if (!low[z, x]) continue;
                heights[z, x] = plain;
                for (var oz = -3; oz <= 3; oz++)
                    for (var ox = -3; ox <= 3; ox++)
                    {
                        int nx = x + ox, nz = z + oz;
                        if (nx >= 0 && nz >= 0 && nx < res && nz < res) near[nz, nx] = true;
                    }
            }
        }
        for (var pass = 0; pass < 2; pass++)
        {
            var copy = (float[,])heights.Clone();
            for (var z = 1; z < res - 1; z++)
            {
                for (var x = 1; x < res - 1; x++)
                {
                    if (!near[z, x]) continue;
                    var sum = 0f;
                    for (var oz = -1; oz <= 1; oz++)
                        for (var ox = -1; ox <= 1; ox++) sum += copy[z + oz, x + ox];
                    heights[z, x] = sum / 9f;
                }
            }
        }
        return count;
    }

    static int MostUsedLayer(float[,,] alpha)
    {
        var layers = alpha.GetLength(2);
        var sums = new float[layers];
        for (var z = 0; z < alpha.GetLength(0); z++)
            for (var x = 0; x < alpha.GetLength(1); x++)
                for (var l = 0; l < layers; l++) sums[l] += alpha[z, x, l];
        var best = 0;
        for (var l = 1; l < layers; l++) if (sums[l] > sums[best]) best = l;
        return best;
    }

    static int Mirror(int index, int last)
    {
        if (index < 0) index = -index;
        if (index > last) index = 2 * last - index;
        return Mathf.Clamp(index, 0, last);
    }

    static float[] MaxFilter(float[] values, int radius)
    {
        var n = values.Length;
        var result = new float[n];
        for (var k = 0; k < n; k++)
        {
            var m = float.MinValue;
            for (var o = -radius; o <= radius; o++) m = Mathf.Max(m, values[(k + o + n) % n]);
            result[k] = m;
        }
        return result;
    }

    static float[] Blur(float[] values, int radius)
    {
        var n = values.Length;
        var result = new float[n];
        for (var k = 0; k < n; k++)
        {
            var sum = 0f;
            for (var o = -radius; o <= radius; o++) sum += values[(k + o + n) % n];
            result[k] = sum / (2 * radius + 1);
        }
        return result;
    }

    static float SoftMax(float a, float b, float k) => 0.5f * (a + b + Mathf.Sqrt((a - b) * (a - b) + k * k));
    static float SoftMin(float a, float b, float k) => 0.5f * (a + b - Mathf.Sqrt((a - b) * (a - b) + k * k));
    static float Smooth01(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
    static float Sq(float x) => x * x;

    // ---------------------------------------------------------------- sand

    static TerrainLayer CreateSandLayer(bool remake)
    {
        if (remake || !File.Exists(SandDiffusePath) || !File.Exists(SandNormalPath))
        {
            MakeSandTextures(1024, out var color, out var normal);
            File.WriteAllBytes(SandDiffusePath, color.EncodeToPNG());
            File.WriteAllBytes(SandNormalPath, normal.EncodeToPNG());
            Object.DestroyImmediate(color);
            Object.DestroyImmediate(normal);
            AssetDatabase.ImportAsset(SandDiffusePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SandNormalPath, ImportAssetOptions.ForceUpdate);
        }
        SetupTexture(SandDiffusePath, false, true, 1024);
        SetupTexture(SandNormalPath, true, false, 1024);

        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(SandLayerPath);
        var isNew = layer == null;
        if (isNew) layer = new TerrainLayer();
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SandDiffusePath);
        layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SandNormalPath);
        if (isNew)
        {
            layer.tileSize = new Vector2(7f, 7f);
            layer.normalScale = 0.8f;
            layer.smoothness = 0f;
            layer.metallic = 0f;
            AssetDatabase.CreateAsset(layer, SandLayerPath);
        }
        EditorUtility.SetDirty(layer);
        AssetDatabase.SaveAssets();
        MobileSetupTool.FixShinyTerrainOnAndroid(); // same Android import as the other terrain textures
        return AssetDatabase.LoadAssetAtPath<TerrainLayer>(SandLayerPath);
    }

    // Warm beach sand that repeats seamlessly: fine grains, soft blotches and wind ripples.
    static void MakeSandTextures(int size, out Texture2D color, out Texture2D normal)
    {
        var baseColor = new Color(0.96f, 0.79f, 0.54f);
        var height = new float[size * size];
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                var large = TileFbm(u, v, 4, 4, 1);
                var medium = TileFbm(u, v, 24, 3, 2);
                var grain = Hash(x, y, 3);
                var clumps = TileNoise(u * 256f, v * 256f, 256, 4);
                var warp = TileFbm(u, v, 3, 3, 5);
                var ripple = 0.5f + 0.5f * Mathf.Sin((u * 7f + v * 2f + warp * 1.6f) * 2f * Mathf.PI);
                ripple = Mathf.Pow(ripple, 1.5f);
                height[y * size + x] = ripple * 0.55f + medium * 0.3f + clumps * 0.1f + grain * 0.05f;

                var shade = 1f + (large - 0.5f) * 0.14f + (medium - 0.5f) * 0.08f + (ripple - 0.5f) * 0.07f + (grain - 0.5f) * 0.1f + (clumps - 0.5f) * 0.06f;
                var c = baseColor * shade;
                c.r += (large - 0.5f) * 0.03f;
                c.b -= (large - 0.5f) * 0.03f;
                var speck = Hash(x, y, 9);
                if (speck > 0.996f) c *= 0.55f;
                else if (speck > 0.99f) c = Color.Lerp(c, new Color(1f, 0.97f, 0.9f), 0.5f);
                c.a = 1f;
                pixels[y * size + x] = c;
            }
        }
        color = new Texture2D(size, size, TextureFormat.RGB24, false);
        color.SetPixels32(pixels);
        color.Apply();
        normal = HeightToNormal(height, size, 6f);
    }

    // ---------------------------------------------------------------- sea

    static Material CreateOceanMaterial(bool remake)
    {
        if (remake || !File.Exists(WaveNormalPath) || !File.Exists(FoamPath))
        {
            var waves = MakeWaveNormal(512);
            var foam = MakeFoam(256);
            File.WriteAllBytes(WaveNormalPath, waves.EncodeToPNG());
            File.WriteAllBytes(FoamPath, foam.EncodeToPNG());
            Object.DestroyImmediate(waves);
            Object.DestroyImmediate(foam);
            AssetDatabase.ImportAsset(WaveNormalPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(FoamPath, ImportAssetOptions.ForceUpdate);
        }
        SetupTexture(WaveNormalPath, true, false, 512);
        SetupTexture(FoamPath, false, false, 256);

        var shader = Shader.Find(OceanShaderName);
        if (shader == null)
        {
            Debug.LogError("[Island] Shader '" + OceanShaderName + "' not found.");
            return null;
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>(OceanMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, OceanMaterialPath);
        }
        material.shader = shader;
        material.enableInstancing = false;
        material.SetTexture("_WaveNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(WaveNormalPath));
        material.SetTexture("_FoamTex", AssetDatabase.LoadAssetAtPath<Texture2D>(FoamPath));
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    // Small criss-crossing waves that repeat seamlessly.
    static Texture2D MakeWaveNormal(int size)
    {
        // whole numbers of waves across the tile, so it repeats
        var waves = new[]
        {
            new Vector4(3, 1, 1.0f, 0.3f), new Vector4(-2, 3, 0.8f, 1.7f), new Vector4(5, -2, 0.5f, 2.9f), new Vector4(1, 6, 0.45f, 4.1f),
            new Vector4(-7, -3, 0.3f, 0.8f), new Vector4(8, 5, 0.25f, 5.2f), new Vector4(-4, 9, 0.2f, 2.2f), new Vector4(11, -6, 0.15f, 3.7f),
            new Vector4(13, 7, 0.1f, 1.1f), new Vector4(-9, 14, 0.08f, 0.5f),
        };
        var height = new float[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                var h = 0f;
                foreach (var w in waves)
                {
                    var s = 0.5f + 0.5f * Mathf.Sin((w.x * u + w.y * v) * 2f * Mathf.PI + w.w);
                    h += w.z * Mathf.Pow(s, 1.15f) / Mathf.Sqrt(w.x * w.x + w.y * w.y);
                }
                h += (TileFbm(u, v, 8, 4, 21) - 0.5f) * 0.08f;
                height[y * size + x] = h;
            }
        }
        return HeightToNormal(height, size, size * 0.1f);
    }

    // Foam: a web of bubbly lines with patches (cellular noise), repeating seamlessly.
    static Texture2D MakeFoam(int size)
    {
        const int cells = 10;
        var points = new Vector2[cells, cells];
        for (var cy = 0; cy < cells; cy++)
            for (var cx = 0; cx < cells; cx++) points[cy, cx] = new Vector2(Hash(cx, cy, 31), Hash(cx, cy, 32));

        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                float u = (float)x / size * cells, v = (float)y / size * cells;
                int ix = Mathf.FloorToInt(u), iy = Mathf.FloorToInt(v);
                float f1 = 9f, f2 = 9f;
                for (var oy = -1; oy <= 1; oy++)
                {
                    for (var ox = -1; ox <= 1; ox++)
                    {
                        int cx = ix + ox, cy = iy + oy;
                        var p = points[Mod(cy, cells), Mod(cx, cells)] + new Vector2(cx, cy);
                        var d = Vector2.Distance(p, new Vector2(u, v));
                        if (d < f1) { f2 = f1; f1 = d; }
                        else if (d < f2) f2 = d;
                    }
                }
                var web = 1f - Mathf.SmoothStep(0f, 0.18f, f2 - f1);
                var patches = TileFbm((float)x / size, (float)y / size, 6, 4, 33);
                var value = Mathf.Clamp01(web * 0.85f + (patches - 0.45f) * 1.2f);
                var b = (byte)Mathf.RoundToInt(value * 255f);
                pixels[y * size + x] = new Color32(b, b, b, 255);
            }
        }
        var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    // ---------------------------------------------------------------- helpers

    static Texture2D HeightToNormal(float[] height, int size, float strength)
    {
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = height[y * size + Mod(x + 1, size)] - height[y * size + Mod(x - 1, size)];
                var dy = height[Mod(y + 1, size) * size + x] - height[Mod(y - 1, size) * size + x];
                var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }
        var texture = new Texture2D(size, size, TextureFormat.RGB24, false, true);
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    static void SetupTexture(string path, bool normalMap, bool sRGB, int maxSize)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        var changed = false;
        void Set<T>(T current, T wanted, Action<T> apply)
        {
            if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
            apply(wanted);
            changed = true;
        }
        Set(importer.textureType, normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default, v => importer.textureType = v);
        if (!normalMap) Set(importer.sRGBTexture, sRGB, v => importer.sRGBTexture = v);
        Set(importer.alphaSource, TextureImporterAlphaSource.None, v => importer.alphaSource = v);
        Set(importer.wrapMode, TextureWrapMode.Repeat, v => importer.wrapMode = v);
        Set(importer.mipmapEnabled, true, v => importer.mipmapEnabled = v);
        Set(importer.anisoLevel, 4, v => importer.anisoLevel = v);
        Set(importer.maxTextureSize, maxSize, v => importer.maxTextureSize = v);
        if (changed) importer.SaveAndReimport();
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xffffff) / 16777215f;
        }
    }

    // smooth noise that repeats every `period` units
    static float TileNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int ax = Mod(x0, period), bx = Mod(x0 + 1, period), ay = Mod(y0, period), by = Mod(y0 + 1, period);
        var bottom = Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), fx);
        var top = Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), fx);
        return Mathf.Lerp(bottom, top, fy);
    }

    // several layers of TileNoise; u, v in 0..1 repeat seamlessly
    static float TileFbm(float u, float v, int period, int octaves, int seed)
    {
        float sum = 0f, amplitude = 0.5f, total = 0f;
        for (var o = 0; o < octaves; o++)
        {
            sum += amplitude * TileNoise(u * period, v * period, period, seed + o * 31);
            total += amplitude;
            amplitude *= 0.5f;
            period *= 2;
        }
        return sum / total;
    }

    static int Mod(int a, int n) => ((a % n) + n) % n;

    static Camera FindMainCamera()
    {
        foreach (var camera in Object.FindObjectsOfType<Camera>(true))
        {
            if (camera.CompareTag("MainCamera")) return camera;
        }
        return null;
    }

    static SourceInfo LoadSourceInfo()
    {
        return File.Exists(SourceInfoPath) ? JsonUtility.FromJson<SourceInfo>(File.ReadAllText(SourceInfoPath)) : null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
