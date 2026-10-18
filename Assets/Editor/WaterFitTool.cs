using System.Collections.Generic;
using Bitgem.VFX.StylisedWater;
using UnityEditor;
using UnityEngine;

// Tools > Water > Fit Water To Crater         moves and resizes the existing water
// Tools > Water > Recreate Water In Crater    deletes the water and builds a clean one in the crater
// Both find the terrain hollow (a crater surrounded by hills) near the water, put the water surface a bit
// below the lowest point of the hollow's rim and size the water box so its sides end inside the hills.
// They work on the terrain as it is in the editor right now. Ctrl+Z undoes them.
public static class WaterFitTool
{
    const float RimClearance = 0.5f;   // how far below the lowest rim point the water surface sits
    const float MinHollowDepth = 0.5f; // hollows shallower than this are ignored
    const int MinHollowCells = 10;     // ...and so are tiny ones
    const float SearchRadius = 150f;   // how far from the water to look for a hollow (metres)
    const string DefaultWaterMaterialGuid = "008541be8ad8b3f42a5318e1c4a73d1c"; // Bitgem example-water-03

    static readonly int[] dx = { 1, -1, 0, 0 };
    static readonly int[] dz = { 0, 0, 1, -1 };

    struct Placement
    {
        public Rect Area;   // world XZ covered by the water
        public int TilesX;
        public int TilesZ;
        public float Level; // world height of the water surface
    }

    [MenuItem("Tools/Water/Fit Water To Crater")]
    static void FitWaterToCrater()
    {
        var water = FindWater();
        if (water == null) return;
        if (water.transform.childCount == 0)
        {
            Fail("The water volume has no child box to resize.");
            return;
        }

        if (!TryFindPlacement(new[] { water.transform.position, SceneViewPivot() }, water.TileSize, out var placement, out var error))
        {
            Fail(error);
            return;
        }

        Undo.SetCurrentGroupName("Fit Water To Crater");
        var group = Undo.GetCurrentGroup();
        var oldParent = water.transform.parent;
        var waterRenderer = water.GetComponent<MeshRenderer>();
        if (waterRenderer != null) PrepareMaterial(waterRenderer.sharedMaterial);
        Apply(water, placement);
        Undo.CollapseUndoOperations(group);

        var parentNote = oldParent != null ? $"\n\nIt was a child of \"{oldParent.name}\", so it was moved to the top of the hierarchy." : "";
        Done(water, placement, "Water placed in the crater." + parentNote);
    }

    [MenuItem("Tools/Water/Recreate Water In Crater")]
    static void RecreateWaterInCrater()
    {
        if (RecreateWater(out var water, out var placement, out var message)) Done(water, placement, message);
        else Fail(message);
    }

    // Deletes every water volume in the open scene and builds a clean one in the crater (one undo step).
    public static bool RecreateWater(out WaterVolumeTransforms water, out string message)
    {
        return RecreateWater(out water, out _, out message);
    }

    static bool RecreateWater(out WaterVolumeTransforms water, out Placement placement, out string message)
    {
        water = null;
        var oldWaters = Object.FindObjectsOfType<WaterVolumeTransforms>();

        // keep the look of the old water, but none of its transform settings
        Material material = null;
        var tileSize = 1.6f;
        var faces = WaterVolumeBase.TileFace.NegX | WaterVolumeBase.TileFace.NegZ | WaterVolumeBase.TileFace.PosX | WaterVolumeBase.TileFace.PosZ;
        var foam = faces;
        var siblingIndex = -1;
        var seeds = new List<Vector3>();
        if (oldWaters.Length > 0)
        {
            var old = oldWaters[0];
            var oldRenderer = old.GetComponent<MeshRenderer>();
            if (oldRenderer != null) material = oldRenderer.sharedMaterial;
            tileSize = old.TileSize;
            faces = old.IncludeFaces;
            foam = old.IncludeFoam;
            if (old.transform.parent == null) siblingIndex = old.transform.GetSiblingIndex();
            foreach (var w in oldWaters) seeds.Add(w.transform.position);
        }
        seeds.Add(SceneViewPivot());
        if (material == null) material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(DefaultWaterMaterialGuid));

        // work out where it goes before deleting anything
        if (!TryFindPlacement(seeds.ToArray(), tileSize, out placement, out var error))
        {
            message = error + "\n\nNothing was changed.";
            return false;
        }

        Undo.SetCurrentGroupName("Recreate Water In Crater");
        var group = Undo.GetCurrentGroup();

        PrepareMaterial(material);
        foreach (var w in oldWaters) Undo.DestroyObjectImmediate(w.gameObject);

        var go = new GameObject("WaterVolume");
        Undo.RegisterCreatedObjectUndo(go, "Recreate Water In Crater");
        if (siblingIndex >= 0) go.transform.SetSiblingIndex(siblingIndex);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        water = go.AddComponent<WaterVolumeTransforms>();
        water.TileSize = tileSize;
        water.IncludeFaces = faces;
        water.IncludeFoam = foam;
        go.AddComponent<WaterVolumeHelper>().WaterVolume = water;

        var box = new GameObject("WaterBox");
        Undo.RegisterCreatedObjectUndo(box, "Recreate Water In Crater");
        box.transform.SetParent(go.transform, false);
        box.transform.localScale = Vector3.one * tileSize;

        Apply(water, placement);
        Undo.CollapseUndoOperations(group);

        message = "The old water was deleted and a new one was made in the crater.";
        return true;
    }

    // With the Built-in Render Pipeline the water shader only works when the material is drawn after the
    // opaque objects (it needs their depth and colour) and without GPU instancing (that draws it in the wrong place).
    static void PrepareMaterial(Material material)
    {
        if (material == null || (material.enableInstancing == false && material.renderQueue > 2500)) return;

        Undo.RecordObject(material, "Recreate Water In Crater");
        material.enableInstancing = false;
        if (material.renderQueue <= 2500) material.renderQueue = -1; // back to the shader's own (transparent) queue
        EditorUtility.SetDirty(material);
    }

    static bool TryFindPlacement(Vector3[] seeds, float tile, out Placement placement, out string error)
    {
        placement = default;
        error = "No terrain found under the water.";
        foreach (var seed in seeds)
        {
            var terrain = FindTerrain(seed);
            if (terrain == null) continue;
            if (TryFindPlacement(terrain, seed, tile, out placement, out error)) return true;
        }
        return false;
    }

    static bool TryFindPlacement(Terrain terrain, Vector3 center, float tile, out Placement placement, out string error)
    {
        placement = default;
        error = null;

        // read the heightmap around the water
        var data = terrain.terrainData;
        var origin = terrain.transform.position;
        var cellX = data.heightmapScale.x;
        var cellZ = data.heightmapScale.z;
        var res = data.heightmapResolution;
        var range = Mathf.CeilToInt((SearchRadius + 60f) / cellX);
        var cx = Mathf.RoundToInt((center.x - origin.x) / cellX);
        var cz = Mathf.RoundToInt((center.z - origin.z) / cellZ);
        var x0 = Mathf.Clamp(cx - range, 0, res - 1);
        var z0 = Mathf.Clamp(cz - range, 0, res - 1);
        var w = Mathf.Clamp(cx + range, 0, res - 1) - x0 + 1;
        var h = Mathf.Clamp(cz + range, 0, res - 1) - z0 + 1;
        var raw = data.GetHeights(x0, z0, w, h);
        var height = new float[w * h];
        for (var z = 0; z < h; z++)
        {
            for (var x = 0; x < w; x++)
            {
                height[z * w + x] = raw[z, x] * data.size.y + origin.y;
            }
        }

        // how high water could rise in every spot before it spills out
        var filled = FillHollows(height, w, h);

        // group the spots that hold water into hollows and take the biggest one near the water
        var label = new int[w * h];
        for (var i = 0; i < label.Length; i++) label[i] = -1;
        List<int> hollow = null;
        float hollowFloor = 0f, hollowRim = 0f;
        var floorCell = -1;
        for (var i = 0; i < label.Length; i++)
        {
            if (label[i] >= 0 || filled[i] - height[i] <= 0.05f) continue;

            var cells = new List<int> { i };
            label[i] = i;
            float floor = float.MaxValue, rim = float.MinValue, nearest = float.MaxValue;
            var deepest = i;
            for (var k = 0; k < cells.Count; k++)
            {
                var c = cells[k];
                int x = c % w, z = c / w;
                if (height[c] < floor) { floor = height[c]; deepest = c; }
                rim = Mathf.Max(rim, filled[c]);
                nearest = Mathf.Min(nearest, new Vector2((x + x0 - cx) * cellX, (z + z0 - cz) * cellZ).magnitude);
                for (var d = 0; d < 4; d++)
                {
                    int nx = x + dx[d], nz = z + dz[d];
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    var n = nz * w + nx;
                    if (label[n] >= 0 || filled[n] - height[n] <= 0.05f) continue;
                    label[n] = i;
                    cells.Add(n);
                }
            }

            if (rim - floor < MinHollowDepth || cells.Count < MinHollowCells || nearest > SearchRadius) continue;
            if (hollow != null && cells.Count <= hollow.Count) continue;
            hollow = cells;
            hollowFloor = floor;
            hollowRim = rim;
            floorCell = deepest;
        }
        if (hollow == null)
        {
            error = $"No crater or hollow found within {SearchRadius} m of the water.";
            return false;
        }

        var level = hollowRim - RimClearance;
        if (level < hollowFloor + 0.3f) level = (hollowFloor + hollowRim) * 0.5f;

        // the part of the hollow that ends up under water
        var wet = new bool[w * h];
        var stack = new Stack<int>();
        stack.Push(floorCell);
        wet[floorCell] = true;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        while (stack.Count > 0)
        {
            var c = stack.Pop();
            int x = c % w, z = c / w;
            var wx = origin.x + (x + x0) * cellX;
            var wz = origin.z + (z + z0) * cellZ;
            minX = Mathf.Min(minX, wx); maxX = Mathf.Max(maxX, wx);
            minZ = Mathf.Min(minZ, wz); maxZ = Mathf.Max(maxZ, wz);
            for (var d = 0; d < 4; d++)
            {
                int nx = x + dx[d], nz = z + dz[d];
                if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                var n = nz * w + nx;
                if (wet[n] || label[n] != label[floorCell] || height[n] >= level) continue;
                wet[n] = true;
                stack.Push(n);
            }
        }

        // grow the box until all of its sides are buried in the hills around the hollow
        for (var margin = 0f; margin <= 10f; margin += 0.5f)
        {
            var tilesX = Mathf.CeilToInt((maxX - minX + 2f * margin) / tile);
            var tilesZ = Mathf.CeilToInt((maxZ - minZ + 2f * margin) / tile);
            if (tilesX > WaterVolumeBase.MAX_TILES_X || tilesZ > WaterVolumeBase.MAX_TILES_Z) break;

            var area = new Rect(minX - margin, minZ - margin, tilesX * tile, tilesZ * tile);
            if (!BoxFits(area, level, terrain, wet, w, h, x0, z0, cellX, cellZ)) continue;

            placement = new Placement { Area = area, TilesX = tilesX, TilesZ = tilesZ, Level = level };
            return true;
        }
        error = "Found the crater, but a single water box does not fit inside it without poking out of the hills.";
        return false;
    }

    static bool BoxFits(Rect rect, float level, Terrain terrain, bool[] wet, int w, int h, int x0, int z0, float cellX, float cellZ)
    {
        var origin = terrain.transform.position;
        const float step = 0.5f, edge = 0.3f;
        var stepsX = Mathf.CeilToInt(rect.width / step);
        var stepsZ = Mathf.CeilToInt(rect.height / step);
        for (var ix = 0; ix <= stepsX; ix++)
        {
            for (var iz = 0; iz <= stepsZ; iz++)
            {
                // the last row/column lands exactly on the far side of the box
                var x = Mathf.Min(rect.xMin + ix * step, rect.xMax);
                var z = Mathf.Min(rect.yMin + iz * step, rect.yMax);
                var ground = terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
                var onEdge = x - rect.xMin < edge || rect.xMax - x < edge || z - rect.yMin < edge || rect.yMax - z < edge;

                // the sides of the box must be hidden in the ground
                if (onEdge)
                {
                    if (ground < level + 0.25f) return false;
                    continue;
                }

                // and no low ground outside the crater may end up inside the box
                if (ground >= level) continue;
                var gx = Mathf.RoundToInt((x - origin.x) / cellX) - x0;
                var gz = Mathf.RoundToInt((z - origin.z) / cellZ) - z0;
                var nearWater = false;
                for (var ox = -1; ox <= 1 && !nearWater; ox++)
                {
                    for (var oz = -1; oz <= 1 && !nearWater; oz++)
                    {
                        int sx = gx + ox, sz = gz + oz;
                        nearWater = sx >= 0 && sz >= 0 && sx < w && sz < h && wet[sz * w + sx];
                    }
                }
                if (!nearWater) return false;
            }
        }
        return true;
    }

    // Call inside an undo group.
    static void Apply(WaterVolumeTransforms water, Placement placement)
    {
        var t = water.transform;
        var box = t.GetChild(0);
        var tile = water.TileSize;
        var tilesY = Mathf.Max(1, Mathf.RoundToInt(box.localScale.y / tile));

        // the water must not move with anything else (for example the player)
        if (t.parent != null) Undo.SetTransformParent(t, null, "Fit Water To Crater");

        Undo.RecordObject(t, "Fit Water To Crater");
        Undo.RecordObject(box, "Fit Water To Crater");

        // the water mesh starts half a unit before its transform and its top is tilesY tiles up from there
        t.rotation = Quaternion.identity;
        t.localScale = Vector3.one;
        t.position = new Vector3(placement.Area.xMin + 0.5f, placement.Level + 0.5f - tilesY * tile, placement.Area.yMin + 0.5f);
        box.localPosition = Vector3.zero;
        box.localRotation = Quaternion.identity;
        box.localScale = new Vector3(placement.TilesX * tile, tilesY * tile, placement.TilesZ * tile);

        water.Rebuild();
    }

    static void Done(WaterVolumeTransforms water, Placement placement, string message)
    {
        Selection.activeGameObject = water.gameObject;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

        var renderer = water.GetComponent<Renderer>();
        Debug.Log($"[Water] {message} Level {placement.Level:F2} m, area x {placement.Area.xMin:F1}..{placement.Area.xMax:F1}, " +
                  $"z {placement.Area.yMin:F1}..{placement.Area.yMax:F1}, transform {water.transform.position}, " +
                  $"renderer bounds {(renderer != null ? renderer.bounds.ToString() : "none")}");
        EditorUtility.DisplayDialog("Water",
            $"{message}\n\nWater level: {placement.Level:F2} m\nSize: {placement.Area.width:F1} x {placement.Area.height:F1} m\n\nSave the scene (Ctrl+S) to keep it, or Ctrl+Z to undo.",
            "OK");
    }

    static WaterVolumeTransforms FindWater()
    {
        if (Selection.activeGameObject != null)
        {
            var selected = Selection.activeGameObject.GetComponentInParent<WaterVolumeTransforms>();
            if (selected != null) return selected;
        }

        var all = Object.FindObjectsOfType<WaterVolumeTransforms>();
        if (all.Length == 1) return all[0];

        Fail(all.Length == 0
            ? "No water volume (Bitgem Water Volume Transforms) found in the open scene."
            : "There is more than one water volume in the scene. Select the one to move first.");
        return null;
    }

    static Vector3 SceneViewPivot()
    {
        return SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
    }

    static Terrain FindTerrain(Vector3 position)
    {
        foreach (var terrain in Terrain.activeTerrains)
        {
            var p = terrain.transform.position;
            var size = terrain.terrainData.size;
            if (position.x >= p.x && position.x <= p.x + size.x && position.z >= p.z && position.z <= p.z + size.z) return terrain;
        }
        return Terrain.activeTerrain;
    }

    // Priority flood: for every heightmap sample, the level water would reach there before spilling over the edge.
    static float[] FillHollows(float[] height, int w, int h)
    {
        var filled = new float[height.Length];
        var done = new bool[height.Length];
        var queue = new MinHeap();
        for (var z = 0; z < h; z++)
        {
            for (var x = 0; x < w; x++)
            {
                if (x != 0 && z != 0 && x != w - 1 && z != h - 1) continue;
                var i = z * w + x;
                filled[i] = height[i];
                done[i] = true;
                queue.Push(filled[i], i);
            }
        }
        while (queue.Count > 0)
        {
            var c = queue.Pop(out var level);
            int x = c % w, z = c / w;
            for (var d = 0; d < 4; d++)
            {
                int nx = x + dx[d], nz = z + dz[d];
                if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                var n = nz * w + nx;
                if (done[n]) continue;
                done[n] = true;
                filled[n] = Mathf.Max(height[n], level);
                queue.Push(filled[n], n);
            }
        }
        return filled;
    }

    static void Fail(string message)
    {
        EditorUtility.DisplayDialog("Water", message, "OK");
    }

    class MinHeap
    {
        readonly List<float> keys = new List<float>();
        readonly List<int> values = new List<int>();

        public int Count => keys.Count;

        public void Push(float key, int value)
        {
            keys.Add(key);
            values.Add(value);
            var i = keys.Count - 1;
            while (i > 0)
            {
                var parent = (i - 1) / 2;
                if (keys[parent] <= keys[i]) break;
                Swap(i, parent);
                i = parent;
            }
        }

        public int Pop(out float key)
        {
            key = keys[0];
            var value = values[0];
            var last = keys.Count - 1;
            keys[0] = keys[last];
            values[0] = values[last];
            keys.RemoveAt(last);
            values.RemoveAt(last);
            var i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, smallest = i;
                if (l < keys.Count && keys[l] < keys[smallest]) smallest = l;
                if (r < keys.Count && keys[r] < keys[smallest]) smallest = r;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
            return value;
        }

        void Swap(int a, int b)
        {
            (keys[a], keys[b]) = (keys[b], keys[a]);
            (values[a], values[b]) = (values[b], values[a]);
        }
    }
}
