using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Nature > 1. Make Prefabs, Items And Icons
// Makes everything the forests and plants need (it can be run again, it overwrites what it made):
//  - Resources/Forest: the tree prefabs (Birch 1-2, Oak 1, Beech 1) with a SceneTree, and the pictures of every
//    tree for the far away trees (Billboards/*.png + .mat)
//  - Resources/Vegetation: the plant prefabs with a ScenePlant (mushrooms, grass that gives hay, berry bushes,
//    herbs, ferns, flowers, branches, stones, a plank of the wreck...)
//  - the new inventory items (hay, berries, porcini, red / field mushroom, chicory, heather, acorn, beechnut,
//    birch catkin) and their icons in Assets/ART/Icons
//  - the stone axe: the axe in the hand becomes the stone axe model, the item becomes "Stone Axe"
public static class NatureAssetsTool
{
    const string ForestDir = "Assets/Resources/Forest";
    const string BillboardDir = "Assets/Resources/Forest/Billboards";
    const string VegetationDir = "Assets/Resources/Vegetation";
    const string VegetationMeshes = "Assets/Resources/Vegetation/Meshes";
    const string IconDir = "Assets/ART/Icons";
    const string Pack = "Assets/ResourcesAndCraftIcon/transparent/";
    const string FE = "Assets/FantasyEnvironments/Environments/";
    const string LP = "Assets/Low Poly Free Vegetation Kit/Prefabs/";
    const string TS = "Assets/TerrainSampleAssets/Prefabs/";

    [MenuItem("Tools/Nature/1. Make Prefabs, Items And Icons")]
    public static void MakeAll()
    {
        Directory.CreateDirectory(ForestDir);
        Directory.CreateDirectory(BillboardDir);
        Directory.CreateDirectory(VegetationMeshes);
        Directory.CreateDirectory(IconDir);
        MakeTreePrefabs();
        MakePlantPrefabs();
        AssetDatabase.SaveAssets();
        CaveTool.MakeBookPrefabs(); // the monk's books and the glowing mushroom (the cave)
        MakeLastLevelAssets();      // hemp, iron ore, the raft
        TerrainPaintTool.MakeForestFloorLayer();
        MakeItems();
        MakeStoneAxe();
        MakeTorch();
        MakeBillboards();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Nature: prefabs, items, icons and tree pictures made");
    }

    // ---------------- trees ---------------- //

    static readonly (string name, string source, TreeSpecies.Kind kind)[] TreeModels =
    {
        ("Birch 1", FE + "Ambient-Occlusion-Trees/Prefabs/Birch_tree1.prefab", TreeSpecies.Kind.Birch),
        ("Birch 2", FE + "Ambient-Occlusion-Trees/Prefabs/Birch_tree2.prefab", TreeSpecies.Kind.Birch),
        ("Oak 1", FE + "Ambient-Occlusion-Trees/Prefabs/Oak_tree1.prefab", TreeSpecies.Kind.Oak),
        ("Beech 1", FE + "Ambient-Occlusion-Trees/Prefabs/Deciduous_tree1.prefab", TreeSpecies.Kind.Beech),
    };

    static void MakeTreePrefabs()
    {
        foreach (var t in TreeModels)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(t.source);
            if (source == null) { Debug.LogWarning("Nature: missing " + t.source); continue; }
            var filter = source.GetComponentInChildren<MeshFilter>();
            var renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
            if (renderer == null) continue;
            var go = new GameObject(t.name);
            go.transform.localScale = filter.transform.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            var info = TreeSpecies.Get((int)t.kind);
            var settings = go.AddComponent<SceneTree>();
            settings.species = t.kind;
            settings.height = Mathf.Round(filter.sharedMesh.bounds.size.y * go.transform.localScale.y * 10f) / 10f;
            settings.growsUpTo = Mathf.Round((info.minFull + info.maxFull) * 0.5f);
            PrefabUtility.SaveAsPrefabAsset(go, ForestDir + "/" + t.name + ".prefab");
            Object.DestroyImmediate(go);
        }
        AssetDatabase.Refresh();
    }

    // ---------------- plants ---------------- //

    public class PlantDef
    {
        public string name, source, item = "", display = "";
        public int amount = 1;
        public bool keep;
        public float regrow = 48f;
        public ScenePlant.PlantSize size = ScenePlant.PlantSize.Medium;
        public float scale = 1f;
        public bool hideUnderSnow = true;
        public bool berries;
    }

    static PlantDef P(string name, string source, float scale, ScenePlant.PlantSize size, string item = "", string display = "", float regrow = 48f)
    {
        return new PlantDef { name = name, source = source, scale = scale, size = size, item = item, display = display, regrow = regrow };
    }

    public static readonly PlantDef[] Plants =
    {
        P("Porcini", FE + "Prefabs/Mushroom1.prefab", 1.15f, ScenePlant.PlantSize.Small, "porcini", "Porcini", 72f),
        P("Red Mushroom", LP + "Vegetation/Mushroom 2.prefab", 1.7f, ScenePlant.PlantSize.Small, "redmushroom", "Red Mushroom", 60f),
        P("Field Mushroom", LP + "Vegetation/Mushroom 3.prefab", 2.4f, ScenePlant.PlantSize.Small, "fieldmushroom", "Field Mushroom", 60f),
        P("Dry Grass 1", TS + "GrassDry_A.prefab", 1.9f, ScenePlant.PlantSize.Medium, "hay", "Dry Grass (hay)", 36f),
        P("Dry Grass 2", TS + "GrassDry_B.prefab", 2.3f, ScenePlant.PlantSize.Medium, "hay", "Dry Grass (hay)", 36f),
        P("Dry Grass 3", TS + "GrassDry_C.prefab", 1.6f, ScenePlant.PlantSize.Medium, "hay", "Dry Grass (hay)", 36f),
        P("Grass Tuft 1", TS + "Grass_B.prefab", 1.4f, ScenePlant.PlantSize.Medium, "hay", "Grass (hay)", 30f),
        P("Grass Tuft 2", TS + "Grass_C.prefab", 1.4f, ScenePlant.PlantSize.Medium, "hay", "Grass (hay)", 30f),
        P("Grass Tuft 3", TS + "Grass_D.prefab", 1.3f, ScenePlant.PlantSize.Medium, "hay", "Grass (hay)", 30f),
        P("Tall Grass", FE + "Prefabs/Grass1.prefab", 1.2f, ScenePlant.PlantSize.Medium, "hay", "Tall Grass (hay)", 30f),
        new PlantDef { name = "Berry Bush 1", source = TS + "Bush_A.prefab", scale = 1.7f, size = ScenePlant.PlantSize.Large, item = "berries", display = "Berry Bush", amount = 2, keep = true, regrow = 72f, berries = true, hideUnderSnow = false },
        new PlantDef { name = "Berry Bush 2", source = TS + "Bush_B.prefab", scale = 1.5f, size = ScenePlant.PlantSize.Large, item = "berries", display = "Berry Bush", amount = 2, keep = true, regrow = 72f, berries = true, hideUnderSnow = false },
        new PlantDef { name = "Bush 1", source = TS + "BushDry_A.prefab", scale = 1.8f, size = ScenePlant.PlantSize.Large, hideUnderSnow = false },
        new PlantDef { name = "Bush 2", source = TS + "BushDry_B.prefab", scale = 1.6f, size = ScenePlant.PlantSize.Large, hideUnderSnow = false },
        P("Chicory", FE + "Prefabs/Flower2.prefab", 0.9f, ScenePlant.PlantSize.Medium, "chicory", "Chicory (herb)", 48f),
        P("Heather 1", TS + "Heather_A.prefab", 1.1f, ScenePlant.PlantSize.Medium, "heather", "Heather (herb)", 48f),
        P("Heather 2", TS + "Heather_B.prefab", 1.0f, ScenePlant.PlantSize.Medium, "heather", "Heather (herb)", 48f),
        P("Fern 1", TS + "Fern_A.prefab", 2.2f, ScenePlant.PlantSize.Medium),
        P("Fern 2", TS + "Fern_B.prefab", 2.4f, ScenePlant.PlantSize.Medium),
        P("Fern 3", TS + "Fern_C.prefab", 2.0f, ScenePlant.PlantSize.Medium),
        P("Fern Plant 1", LP + "Vegetation/Plant 1.prefab", 1.2f, ScenePlant.PlantSize.Medium),
        P("Fern Plant 2", LP + "Vegetation/Plant 2.prefab", 1.1f, ScenePlant.PlantSize.Medium),
        P("Fern Plant 3", LP + "Vegetation/Plant 3.prefab", 1.0f, ScenePlant.PlantSize.Medium),
        P("Wild Flower 1", LP + "Vegetation/Flower 1.prefab", 1.0f, ScenePlant.PlantSize.Small),
        P("Wild Flower 2", LP + "Vegetation/Flower 2.prefab", 1.0f, ScenePlant.PlantSize.Small),
        P("Wild Flower 3", LP + "Vegetation/Flower 3.prefab", 1.0f, ScenePlant.PlantSize.Small),
        P("Dry Weed 1", TS + "Plant_A.prefab", 1.3f, ScenePlant.PlantSize.Medium),
        P("Dry Weed 2", TS + "Plant_B.prefab", 1.3f, ScenePlant.PlantSize.Medium),
        P("Dry Weed 3", TS + "Plant_C.prefab", 1.3f, ScenePlant.PlantSize.Medium),
        P("Dry Weed 4", TS + "Plant_D.prefab", 1.5f, ScenePlant.PlantSize.Medium),
        P("Branch 1", LP + "Vegetation/Branch 1.prefab", 1.25f, ScenePlant.PlantSize.Small, "stick", "Branch", 60f),
        P("Branch 2", LP + "Vegetation/Branch 2.prefab", 1.35f, ScenePlant.PlantSize.Small, "stick", "Branch", 60f),
        P("Branch 3", LP + "Vegetation/Branch 3.prefab", 1.15f, ScenePlant.PlantSize.Small, "stick", "Branch", 60f),
        P("Stone 1", LP + "Rocks/Small Rock 1.prefab", 2.0f, ScenePlant.PlantSize.Small, "stone", "Stone", 120f),
        P("Stone 2", LP + "Rocks/Small Rock 2.prefab", 2.2f, ScenePlant.PlantSize.Small, "stone", "Stone", 120f),
        new PlantDef { name = "Driftwood", source = LP + "Vegetation/Log.prefab", scale = 2.4f, size = ScenePlant.PlantSize.Large, hideUnderSnow = false },
        new PlantDef { name = "Fallen Log", source = LP + "Vegetation/Log.prefab", scale = 2.8f, size = ScenePlant.PlantSize.Large, hideUnderSnow = false },
        new PlantDef { name = "Old Stump 1", source = LP + "Vegetation/Stump 1.prefab", scale = 2.4f, size = ScenePlant.PlantSize.Large, hideUnderSnow = false },
        new PlantDef { name = "Old Stump 2", source = LP + "Vegetation/Stump 2.prefab", scale = 1.8f, size = ScenePlant.PlantSize.Large, hideUnderSnow = false },
        new PlantDef { name = "Old Stump 3", source = LP + "Vegetation/Stump 3.prefab", scale = 2.2f, size = ScenePlant.PlantSize.Large, hideUnderSnow = false },
    };

    static void MakePlantPrefabs()
    {
        var berryMaterial = BerryMaterial();
        foreach (var def in Plants)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(def.source) == null) { Debug.LogWarning("Nature: missing " + def.source); continue; }
            var root = PrefabUtility.LoadPrefabContents(def.source);
            try
            {
                foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var l in root.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(l);
                foreach (var m in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(m);
                root.name = def.name;
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = root.transform.localScale * def.scale;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = def.size == ScenePlant.PlantSize.Large ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }
                var plant = root.AddComponent<ScenePlant>();
                plant.item = def.item;
                plant.displayName = def.display;
                plant.amount = def.amount;
                plant.keepPlant = def.keep;
                plant.regrowHours = def.regrow;
                plant.size = def.size;
                plant.hideUnderSnow = def.hideUnderSnow;
                if (def.berries) AddBerries(root, def.name, berryMaterial);
                // never static: a static-batched plant can not be picked (its mesh becomes the whole batch)
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                PrefabUtility.SaveAsPrefabAsset(root, VegetationDir + "/" + def.name + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // a plank of the broken ship, washed up on the beach
        var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(plank.GetComponent<Collider>());
        plank.name = "Wreck Plank";
        plank.transform.localScale = new Vector3(0.32f, 0.07f, 1.8f);
        var wood = WoodMaterial();
        if (wood != null) plank.GetComponent<MeshRenderer>().sharedMaterial = wood;
        var p = plank.AddComponent<ScenePlant>();
        p.item = "plank";
        p.displayName = "Plank (from the ship)";
        p.regrowHours = 0f;
        p.size = ScenePlant.PlantSize.Small;
        p.hideUnderSnow = false;
        PrefabUtility.SaveAsPrefabAsset(plank, VegetationDir + "/Wreck Plank.prefab");
        Object.DestroyImmediate(plank);
        AssetDatabase.Refresh();
    }

    static Material WoodMaterial()
    {
        var chopped = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ChoppedTree.prefab");
        if (chopped == null) return null;
        foreach (var r in chopped.GetComponentsInChildren<MeshRenderer>(true))
            if (r.name.StartsWith("inner_part")) return r.sharedMaterial;
        return null;
    }

    static Material BerryMaterial()
    {
        string path = VegetationMeshes + "/Berries.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard")) { name = "Berries" };
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = new Color(0.72f, 0.04f, 0.09f);
        m.SetFloat("_Glossiness", 0.62f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // red berries on the outside of the bush (one mesh, child "Fruit": picking hides it, it comes back later)
    static void AddBerries(GameObject bush, string name, Material material)
    {
        var filter = bush.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;
        Bounds b = filter.sharedMesh.bounds;
        float scale = Mathf.Max(0.01f, filter.transform.lossyScale.x);
        var rng = new System.Random(name.GetHashCode());
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        var sv = sphere.vertices; var sn = sphere.normals; var st = sphere.triangles;
        float r = 0.045f / scale;
        for (int k = 0; k < 14; k++)
        {
            // a cluster of 3 berries on the upper part of the bush
            float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float up = Mathf.Lerp(0.25f, 0.85f, (float)rng.NextDouble());
            Vector3 dir = new Vector3(Mathf.Cos(a) * Mathf.Sqrt(1f - up * up), up, Mathf.Sin(a) * Mathf.Sqrt(1f - up * up));
            Vector3 centre = b.center + Vector3.Scale(dir, b.extents * 0.86f);
            for (int j = 0; j < 3; j++)
            {
                Vector3 c = centre + new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * r * 3f;
                int start = vertices.Count;
                for (int i = 0; i < sv.Length; i++) { vertices.Add(c + sv[i] * r * 2f); normals.Add(sn[i]); }
                for (int i = 0; i < st.Length; i++) triangles.Add(start + st[i]);
            }
        }
        var mesh = new Mesh { name = name + " berries" };
        mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        string path = VegetationMeshes + "/" + name + " berries.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        var fruit = new GameObject("Fruit");
        fruit.transform.SetParent(filter.transform, false);
        fruit.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = fruit.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // the Fruit must be a direct child of the plant (Vegetation looks for it there)
        if (filter.transform != bush.transform) fruit.transform.SetParent(bush.transform, true);
    }

    // ---------------- inventory items ---------------- //

    class ItemDef
    {
        public string item, name, description, use;
        public bool food, seed, book;
        public float health, calories, water;
        public string icon;          // a picture of the icon pack (copied to Assets/ART/Icons)
        public string renderPlant;   // or: a picture made from this plant prefab
        public int renderSeed = -1;  // or: from this tree's seed model
        public string renderAsset;   // or: from this prefab (asset path)
    }

    static readonly ItemDef[] Items =
    {
        new ItemDef { item = "hay", name = "Hay", description = "Dry grass. It catches fire very easily.", use = "Needed to light a fire, together with wood.", icon = "herbs/hb_t_15.png" },
        new ItemDef { item = "berries", name = "Berries", description = "Sweet wild berries.", use = "Eat: +3 health, +120 food, +6 water.", food = true, health = 3, calories = 120, water = 6, icon = "addons/125_t.png" },
        new ItemDef { item = "porcini", name = "Porcini", description = "A big, tasty forest mushroom.", use = "Eat: +15 health, +250 food.", food = true, health = 15, calories = 250, renderPlant = "Porcini" },
        new ItemDef { item = "redmushroom", name = "Red Mushroom", description = "A red forest mushroom. Bitter, but safe.", use = "Eat: +8 health, +120 food.", food = true, health = 8, calories = 120, renderPlant = "Red Mushroom" },
        new ItemDef { item = "fieldmushroom", name = "Field Mushroom", description = "A small grey mushroom of the forest floor.", use = "Eat: +10 health, +150 food.", food = true, health = 10, calories = 150, renderPlant = "Field Mushroom" },
        new ItemDef { item = "chicory", name = "Chicory", description = "A herb with blue flowers. Good against weakness.", use = "Eat: +8 health, +4 water.", food = true, health = 8, water = 4, icon = "herbs/hb_t_07.png" },
        new ItemDef { item = "heather", name = "Heather", description = "A small purple heath herb.", use = "Eat: +6 health, +20 food.", food = true, health = 6, calories = 20, icon = "herbs/hb_t_09.png" },
        new ItemDef { item = "acorn", name = "Acorn", description = "The seed of an oak. Fallen acorns grow into new oaks.", use = "Use it (double tap / right click) to plant an oak in front of you.", seed = true, icon = "herbs/acorn_t_01.png" },
        new ItemDef { item = "beechnut", name = "Beechnut", description = "The seed of a beech. Fallen beechnuts grow into new beeches.", use = "Use it (double tap / right click) to plant a beech in front of you.", seed = true, renderSeed = 3 },
        new ItemDef { item = "birchseed", name = "Birch Catkin", description = "Full of tiny birch seeds. Fallen catkins grow into new birches.", use = "Use it (double tap / right click) to plant a birch in front of you.", seed = true, renderSeed = 1 },
        new ItemDef { item = "ironore", name = "Iron Ore", description = "A heavy rusty stone from the cave.", use = "10 of them and a log make an iron axe (Crafting > Tools).", icon = "stone/ore_01_n_t.PNG" },
        new ItemDef { item = "hemp", name = "Hemp", description = "A tall plant with strong fibres. It grows thickest around the lake.", use = "3 hemp make a rope (Crafting > Refine); 5 hemp and a stick make a snare (Crafting > Survival).", renderPlant = "Hemp" },
        new ItemDef { item = "rope", name = "Rope", description = "A strong rope twisted from hemp fibres.", use = "Ties the logs of a raft together.", icon = "addons/186_t.png" },
        new ItemDef { item = "journal", name = "Old Journal", description = "The journal of Brother Ioan, a monk who lived on this island long ago.", use = "Use it (double tap / right click) to read it.", book = true, icon = "parts/Journal_t_01.png" },
        new ItemDef { item = "prayerbook", name = "Prayer Book", description = "The old prayer book of Brother Ioan.", use = "Use it to read the prayers. At night the prayer drives the shadow away for a while.", book = true, renderPlant = "Prayer Book" },
    };

    static void MakeItems()
    {
        const string template = "Assets/Resources/pinecone.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(template) == null) { Debug.LogWarning("Nature: no " + template); return; }
        foreach (var def in Items)
        {
            Sprite sprite = def.icon != null ? CopyIcon(def.icon, def.item) : RenderIcon(def);
            string path = "Assets/Resources/" + def.item + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) AssetDatabase.CopyAsset(template, path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = def.item;
                root.tag = "Untagged";
                var image = root.GetComponent<UnityEngine.UI.Image>();
                if (image != null && sprite != null) image.sprite = sprite;
                var info = root.GetComponent<InventoryItem>();
                info.thisName = def.name;
                info.thisDescription = def.description;
                info.thisFunctionality = def.use;
                info.isConsumable = def.food;
                info.healthEffect = def.health;
                info.caloriesEffect = def.calories;
                info.hydrationEffect = def.water;
                info.isUseable = def.seed || def.book;
                info.isEquippable = false;
                info.isTrashable = true;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    // a picture of the icon pack, copied next to the game's other icons (the pack folder is not in git)
    internal static Sprite CopyIcon(string packPath, string item)
    {
        string source = Pack + packPath;
        string target = IconDir + "/" + item + ".png";
        if (!File.Exists(source))
        {
            // the pack mixes .png and .PNG
            string alt = Path.ChangeExtension(source, ".PNG");
            if (File.Exists(alt)) source = alt; else { Debug.LogWarning("Nature: no icon " + source); return null; }
        }
        File.Copy(source, target, true);
        AssetDatabase.ImportAsset(target);
        return ImportSprite(target);
    }

    internal static Sprite ImportSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // an icon drawn from the 3D model (mushrooms, seeds), on a transparent background
    static Sprite RenderIcon(ItemDef def)
    {
        string target = IconDir + "/" + def.item + ".png";
        GameObject model = null;
        if (def.renderPlant != null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VegetationDir + "/" + def.renderPlant + ".prefab");
            if (prefab != null) model = Object.Instantiate(prefab);
        }
        else if (def.renderAsset != null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.renderAsset);
            if (prefab != null) model = Object.Instantiate(prefab);
            if (model != null) foreach (var s in model.GetComponentsInChildren<MonoBehaviour>()) Object.DestroyImmediate(s);
        }
        else if (def.renderSeed >= 0)
        {
            model = new GameObject("seed");
            model.AddComponent<MeshFilter>().sharedMesh = SeedModels.MeshOf(def.renderSeed);
            model.AddComponent<MeshRenderer>().sharedMaterial = SeedModels.MaterialOf(def.renderSeed);
            model.transform.rotation = Quaternion.Euler(0f, 30f, def.renderSeed == 3 ? 10f : 55f);
        }
        if (model == null) return null;
        model.transform.position = Vector3.zero;
        var picture = RenderModel(model, 256, 256, Quaternion.Euler(22f, 35f, 0f), false);
        Object.DestroyImmediate(model);
        // A model with a cave material (the monk's books) comes out dark or empty here: then the icon that is
        // there already (the prayer book's was drawn by hand) is kept.
        int seen = 0;
        foreach (var c in picture.GetPixels32()) if (c.a > 25 && c.r + c.g + c.b > 30) seen++;
        if (seen < 256 * 256 / 50 && File.Exists(target))
            Debug.LogWarning("Icon of " + def.item + ": the picture came out (almost) empty, the old icon is kept");
        else
            File.WriteAllBytes(target, picture.EncodeToPNG());
        Object.DestroyImmediate(picture);
        AssetDatabase.ImportAsset(target);
        return ImportSprite(target);
    }

    // draws one model alone (perspective, a soft light) into a picture with a transparent background
    internal static Texture2D RenderModel(GameObject model, int width, int height, Quaternion view, bool ortho)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        SceneManager.MoveGameObjectToScene(model, scene);
        var camGo = new GameObject("cam");
        SceneManager.MoveGameObjectToScene(camGo, scene);
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.scene = scene;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.fieldOfView = 24f;
        var lightGo = new GameObject("light");
        SceneManager.MoveGameObjectToScene(lightGo, scene);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        lightGo.transform.rotation = Quaternion.Euler(40f, 140f, 0f);
        var fill = new GameObject("fill");
        SceneManager.MoveGameObjectToScene(fill, scene);
        var fillLight = fill.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 0.55f;
        fill.transform.rotation = Quaternion.Euler(20f, -60f, 0f);

        var renderers = model.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        float size = b.extents.magnitude;
        Vector3 dir = view * Vector3.back;
        camGo.transform.position = b.center - dir * size / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
        camGo.transform.LookAt(b.center);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = size * 20f;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        var active = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = active;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        EditorSceneManager.ClosePreviewScene(scene);
        return tex;
    }

    // ---------------- the stone axe ---------------- //

    static void MakeStoneAxe()
    {
        var stone = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Survival tools/Prefabs/Axe (1).prefab");
        var stoneFilter = stone != null ? stone.GetComponentInChildren<MeshFilter>() : null;
        if (stoneFilter == null) { Debug.LogWarning("Nature: no stone axe model"); return; }
        var stoneRenderer = stoneFilter.GetComponent<MeshRenderer>();
        Mesh newMesh = stoneFilter.sharedMesh;

        const string modelPath = "Assets/Resources/Axe_Model.prefab";
        var root = PrefabUtility.LoadPrefabContents(modelPath);
        try
        {
            var filter = root.GetComponentInChildren<MeshFilter>();
            var renderer = filter.GetComponent<MeshRenderer>();
            Mesh oldMesh = filter.sharedMesh;
            if (oldMesh != newMesh)
            {
                // the same grip (the handle's end where it was) and about 80% of the old axe's length
                Bounds ob = oldMesh.bounds, nb = newMesh.bounds;
                float s = 0.8f * ob.size.y / nb.size.y;
                bool flip = Mathf.Sign(HeadSide(oldMesh)) != Mathf.Sign(HeadSide(newMesh));
                Quaternion turn = flip ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                Vector3 newBottom = turn * new Vector3(nb.center.x, nb.min.y, nb.center.z) * s;
                Vector3 oldBottom = new Vector3(ob.center.x, ob.min.y, ob.center.z);
                filter.transform.localRotation = turn;
                filter.transform.localScale = Vector3.one * s;
                filter.transform.localPosition = oldBottom - newBottom;
                filter.sharedMesh = newMesh;
                renderer.sharedMaterials = stoneRenderer.sharedMaterials;
            }
            PrefabUtility.SaveAsPrefabAsset(root, modelPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // the item: "Stone Axe" with its own icon
        var sprite = CopyIcon("addons/148_t.png", "stoneaxe");
        const string itemPath = "Assets/Resources/Axe.prefab";
        var item = PrefabUtility.LoadPrefabContents(itemPath);
        try
        {
            var info = item.GetComponent<InventoryItem>();
            info.thisName = "Stone Axe";
            info.thisDescription = "A sharp stone tied to a stick.";
            info.thisFunctionality = "Chops trees (logs and branches) and hunts rabbits.";
            var image = item.GetComponent<UnityEngine.UI.Image>();
            if (image != null && sprite != null) image.sprite = sprite;
            PrefabUtility.SaveAsPrefabAsset(item, itemPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(item); }
    }

    // ---------------- hemp, iron ore and the raft (the last levels) ---------------- //

    const string RaftDir = "Assets/Island/Raft";

    public static void MakeLastLevelAssets()
    {
        Directory.CreateDirectory(RaftDir);
        var standard = Shader.Find("Standard");
        Material Mat(string name, Color c, float gloss, float metal = 0f)
        {
            string path = RaftDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(standard); AssetDatabase.CreateAsset(m, path); }
            m.color = c;
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", metal);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }
        Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = RaftDir + "/" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // hemp: a tall stalk with hand-shaped leaves
        var hemp = new GameObject("Hemp");
        hemp.AddComponent<MeshFilter>().sharedMesh = SaveMesh(HempMesh(), "Hemp mesh");
        var hr = hemp.AddComponent<MeshRenderer>();
        hr.sharedMaterial = Mat("Hemp", new Color(0.24f, 0.44f, 0.13f), 0.15f);
        hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var hp = hemp.AddComponent<ScenePlant>();
        hp.item = "hemp"; hp.displayName = "Hemp"; hp.regrowHours = 48f; hp.size = ScenePlant.PlantSize.Medium; hp.hideUnderSnow = true;
        PrefabUtility.SaveAsPrefabAsset(hemp, VegetationDir + "/Hemp.prefab");
        Object.DestroyImmediate(hemp);

        // iron ore: a rusty, heavy stone
        var rockSource = AssetDatabase.LoadAssetAtPath<GameObject>(LP + "Rocks/Rock 1.prefab");
        var rockFilter = rockSource != null ? rockSource.GetComponentInChildren<MeshFilter>() : null;
        if (rockFilter != null)
        {
            var ore = new GameObject("Iron Ore");
            ore.transform.localScale = Vector3.one * 0.3f;
            ore.AddComponent<MeshFilter>().sharedMesh = rockFilter.sharedMesh;
            ore.AddComponent<MeshRenderer>().sharedMaterial = Mat("Iron Ore", new Color(0.4f, 0.24f, 0.16f), 0.55f, 0.65f);
            var op = ore.AddComponent<ScenePlant>();
            op.item = "ironore"; op.displayName = "Iron Ore"; op.regrowHours = 0f; op.size = ScenePlant.PlantSize.Small; op.hideUnderSnow = false;
            PrefabUtility.SaveAsPrefabAsset(ore, VegetationDir + "/Iron Ore.prefab");
            Object.DestroyImmediate(ore);
        }

        // the raft: seven logs side by side, two beams across, rope, a mast with a sail
        var logSource = AssetDatabase.LoadAssetAtPath<GameObject>(LP + "Vegetation/Log.prefab");
        var logFilter = logSource != null ? logSource.GetComponentInChildren<MeshFilter>() : null;
        if (logFilter == null) return;
        Mesh logMesh = logFilter.sharedMesh;
        var logMat = logFilter.GetComponent<MeshRenderer>().sharedMaterial;
        Bounds lb = logMesh.bounds;
        var rope = Mat("Raft Rope", new Color(0.32f, 0.23f, 0.12f), 0.1f);
        var cloth = Mat("Raft Sail", new Color(0.9f, 0.84f, 0.7f), 0.05f);
        var raft = new GameObject("RaftModel");
        GameObject Part(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var g = new GameObject(name);
            g.transform.SetParent(raft.transform, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }
        float logLength = lb.size.x, logThick = Mathf.Max(lb.size.y, lb.size.z);
        float along = 4.2f / logLength, thick = 0.5f / logThick;
        for (int i = 0; i < 7; i++)
            Part("Log " + (i + 1), logMesh, logMat, new Vector3((i - 3) * 0.49f, 0f, (i % 2) * 0.12f + lb.center.x * along), new Vector3(0f, 90f, 0f),
                new Vector3(along * (0.94f + (i % 3) * 0.03f), thick, thick));
        float across = 3.7f / logLength;
        foreach (float z in new[] { -1.4f, 1.4f })
            Part("Beam", logMesh, logMat, new Vector3(-lb.center.x * across, 0.42f, z), Vector3.zero, new Vector3(across, thick * 0.6f, thick * 0.6f));
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        foreach (float z in new[] { -1.4f, 0f, 1.4f })
            Part("Rope", cube, rope, new Vector3(0f, 0.3f, z + 0.18f), Vector3.zero, new Vector3(3.5f, 0.07f, 0.07f));
        float mastScale = 4.6f / logLength;
        Part("Mast", logMesh, logMat, new Vector3(0f, 2.4f, 0.2f), new Vector3(0f, 0f, 90f), new Vector3(mastScale, thick * 0.55f, thick * 0.55f));
        Part("Yard", logMesh, logMat, new Vector3(-lb.center.x * (2.4f / logLength), 4.05f, 0.35f), Vector3.zero, new Vector3(2.4f / logLength, thick * 0.32f, thick * 0.32f));
        Part("Sail", SaveMesh(SailMesh(), "Sail mesh"), cloth, new Vector3(0f, 1.55f, 0.45f), Vector3.zero, Vector3.one);
        Part("Bundle", cube, rope, new Vector3(0.95f, 0.55f, -1.05f), new Vector3(0f, 20f, 0f), new Vector3(0.6f, 0.38f, 0.5f));
        var box = raft.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.5f, 0f);
        box.size = new Vector3(3.5f, 1.4f, 4.3f);
        raft.AddComponent<Raft>();
        PrefabUtility.SaveAsPrefabAsset(raft, "Assets/Resources/RaftModel.prefab");
        Object.DestroyImmediate(raft);
        AssetDatabase.SaveAssets();
    }

    // a hemp plant: a stalk and pairs of hand-shaped leaves (7 narrow leaflets each), both sides drawn
    static Mesh HempMesh()
    {
        var v = new List<Vector3>();
        var tri = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            tri.Add(s); tri.Add(s + 1); tri.Add(s + 2); tri.Add(s); tri.Add(s + 2); tri.Add(s + 3);
            s = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            tri.Add(s); tri.Add(s + 2); tri.Add(s + 1); tri.Add(s); tri.Add(s + 3); tri.Add(s + 2);
        }
        const float height = 1.75f, r = 0.022f;
        for (int k = 0; k < 6; k++)
        {
            float a0 = k / 6f * Mathf.PI * 2f, a1 = (k + 1) / 6f * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
            Quad(p0, p1, p1 * 0.6f + Vector3.up * height, p0 * 0.6f + Vector3.up * height);
        }
        float[] lengths = { 0.12f, 0.18f, 0.25f, 0.3f, 0.25f, 0.18f, 0.12f };
        for (int level = 0; level < 7; level++)
        {
            float y = 0.45f + level * 0.19f;
            float size = 1f - level * 0.09f;
            for (int side = 0; side < 2; side++)
            {
                float baseAngle = level * 90f + side * 180f;
                Vector3 root = new Vector3(0f, y, 0f);
                Vector3 dir0 = Quaternion.Euler(0f, baseAngle, 0f) * Vector3.forward;
                Vector3 stem = root + dir0 * 0.08f * size + Vector3.up * 0.03f;
                Quad(root, root + Vector3.up * 0.01f, stem + Vector3.up * 0.01f, stem);
                for (int f = 0; f < 7; f++)
                {
                    float spread = (f - 3) * 22f;
                    Vector3 dir = Quaternion.Euler(0f, baseAngle + spread, 0f) * Vector3.forward;
                    Vector3 sideDir = Vector3.Cross(Vector3.up, dir).normalized;
                    float L = lengths[f] * size * 1.2f;
                    Vector3 tip = stem + dir * L + Vector3.down * L * 0.25f;
                    Vector3 mid = stem + dir * L * 0.45f + Vector3.up * L * 0.04f;
                    float w = 0.022f * size * 1.2f;
                    Quad(stem, mid - sideDir * w, tip, mid + sideDir * w);
                }
            }
        }
        // the top: a tuft of small leaves
        for (int f = 0; f < 6; f++)
        {
            Vector3 dir = Quaternion.Euler(0f, f * 60f, 0f) * Vector3.forward;
            Vector3 sideDir = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 b = new Vector3(0f, height - 0.08f, 0f);
            Vector3 tip = b + dir * 0.09f + Vector3.up * 0.16f;
            Vector3 mid = b + dir * 0.05f + Vector3.up * 0.08f;
            Quad(b, mid - sideDir * 0.02f, tip, mid + sideDir * 0.02f);
        }
        var mesh = new Mesh { name = "Hemp" };
        mesh.SetVertices(v);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // a sail filled by the wind: a grid bulging forward, both sides drawn
    static Mesh SailMesh()
    {
        const int nx = 6, ny = 6;
        const float width = 2.1f, height = 2.4f;
        var v = new List<Vector3>();
        var tri = new List<int>();
        for (int side = 0; side < 2; side++)
        {
            int start = v.Count;
            for (int y = 0; y <= ny; y++)
                for (int x = 0; x <= nx; x++)
                {
                    float u = (float)x / nx, t = (float)y / ny;
                    float bulge = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(t * Mathf.PI) * 0.35f;
                    v.Add(new Vector3((u - 0.5f) * width * (1f - 0.12f * t), t * height, bulge));
                }
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    int i = start + y * (nx + 1) + x, j = i + nx + 1;
                    if (side == 0) { tri.Add(i); tri.Add(j); tri.Add(i + 1); tri.Add(i + 1); tri.Add(j); tri.Add(j + 1); }
                    else { tri.Add(i); tri.Add(i + 1); tri.Add(j); tri.Add(i + 1); tri.Add(j + 1); tri.Add(j); }
                }
        }
        var mesh = new Mesh { name = "Sail" };
        mesh.SetVertices(v);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------- the torch ---------------- //

    // The torch of the Survival tools pack: in the hand (Resources/torch_Model, held like the axe, with a flame and a
    // light: TorchLight) and in the inventory (Resources/torch, with TorchFuel: 6 game hours).
    public static void MakeTorch()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Survival tools/Prefabs/Torch.prefab");
        var sourceFilter = source != null ? source.GetComponentInChildren<MeshFilter>() : null;
        if (sourceFilter == null) { Debug.LogWarning("Nature: no torch model"); return; }
        Mesh torchMesh = sourceFilter.sharedMesh;
        Mesh oldAxe = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Survival tools/Axe/Axe.obj")) if (o is Mesh m) { oldAxe = m; break; }
        Bounds ob = oldAxe != null ? oldAxe.bounds : new Bounds(new Vector3(0f, 2f, 0f), new Vector3(1f, 19.56f, 4.7f));
        Bounds nb = torchMesh.bounds;
        float s = 0.62f * ob.size.y / nb.size.y;

        var root = new GameObject("torch_Model");
        root.layer = 9;
        root.transform.localScale = Vector3.one * 0.15f;
        root.transform.localEulerAngles = new Vector3(310f, 160f, 2f);
        var model = new GameObject("default");
        model.layer = 9;
        model.transform.SetParent(root.transform, false);
        model.transform.localScale = Vector3.one * s;
        Vector3 newBottom = new Vector3(nb.center.x, nb.min.y, nb.center.z) * s;
        Vector3 oldBottom = new Vector3(ob.center.x, ob.min.y, ob.center.z);
        model.transform.localPosition = oldBottom - newBottom;
        model.AddComponent<MeshFilter>().sharedMesh = torchMesh;
        var r = model.AddComponent<MeshRenderer>();
        r.sharedMaterials = sourceFilter.GetComponent<MeshRenderer>().sharedMaterials;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var flame = new GameObject("Flame point");
        flame.layer = 9;
        flame.transform.SetParent(root.transform, false);
        flame.transform.localPosition = model.transform.localPosition + new Vector3(nb.center.x, nb.max.y, nb.center.z) * s + Vector3.up * nb.size.y * s * 0.02f;
        root.AddComponent<TorchLight>().flamePoint = flame.transform;
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/torch_Model.prefab");
        Object.DestroyImmediate(root);

        // the icon: a picture of the torch
        var shown = Object.Instantiate(source);
        shown.transform.rotation = Quaternion.Euler(0f, 0f, -35f);
        var picture = RenderModel(shown, 256, 256, Quaternion.Euler(10f, 25f, 0f), false);
        Object.DestroyImmediate(shown);
        string iconPath = IconDir + "/torch.png";
        File.WriteAllBytes(iconPath, picture.EncodeToPNG());
        Object.DestroyImmediate(picture);
        AssetDatabase.ImportAsset(iconPath);
        var sprite = ImportSprite(iconPath);

        const string itemPath = "Assets/Resources/torch.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(itemPath) == null) AssetDatabase.CopyAsset("Assets/Resources/pinecone.prefab", itemPath);
        var item = PrefabUtility.LoadPrefabContents(itemPath);
        try
        {
            item.name = "torch";
            item.tag = "Untagged";
            var info = item.GetComponent<InventoryItem>();
            info.thisName = "Torch";
            info.thisDescription = "A stick with dry grass tied around it. It burns about 6 hours in the hand.";
            info.thisFunctionality = "Put it in the quick bar and take it in your hand: it lights the dark (the cave!).";
            info.isEquippable = true;
            info.isUseable = false;
            info.isConsumable = false;
            info.isTrashable = true;
            var image = item.GetComponent<UnityEngine.UI.Image>();
            if (image != null && sprite != null) image.sprite = sprite;
            if (item.GetComponent<TorchFuel>() == null) item.AddComponent<TorchFuel>();
            PrefabUtility.SaveAsPrefabAsset(item, itemPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(item); }
    }

    // which side (+z / -z) the axe head sticks out to: the top part of the mesh, compared with the handle
    static float HeadSide(Mesh mesh)
    {
        var b = mesh.bounds;
        float top = b.max.y - b.size.y * 0.25f;
        float sum = 0f; int n = 0;
        foreach (var v in mesh.vertices) if (v.y > top) { sum += v.z - b.center.z; n++; }
        return n > 0 ? sum / n : 1f;
    }

    // ---------------- pictures of the trees (far away billboards) ---------------- //

    [MenuItem("Tools/Nature/Remake The Pictures Of The Trees")]
    public static void MakeBillboards()
    {
        var bake = Shader.Find("Hidden/SurvivalBD/Billboard Bake");
        var leaf = Shader.Find("SurvivalBD/Tree Leaves");
        var bark = Shader.Find("SurvivalBD/Tree Bark");
        var billboardShader = Shader.Find("SurvivalBD/Tree Billboard");
        if (bake == null || leaf == null || bark == null || billboardShader == null) { Debug.LogWarning("Nature: tree shaders not found"); return; }
        for (int s = 0; s < TreeSpecies.Count; s++)
        {
            foreach (var v in Forest.VariantsOf(s))
            {
                var model = Object.Instantiate(v.prefab);
                model.transform.position = Vector3.zero;
                model.transform.rotation = Quaternion.identity;
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) if (mats[i] != null) mats[i] = Forest.MakeForestMaterial(mats[i], s, leaf, bark);
                    r.sharedMaterials = mats;
                }
                var renderers = model.GetComponentsInChildren<Renderer>();
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                float margin = b.size.y * 0.02f;
                float frameH = b.size.y + margin * 2f;
                float frameW = Mathf.Max(b.size.x, b.size.z) * 1.04f;
                float bottom = b.min.y - margin;
                int texH = 512;
                int texW = Mathf.Clamp(Mathf.RoundToInt(texH * frameW / frameH / 8f) * 8, 64, 1024);

                var scene = EditorSceneManager.NewPreviewScene();
                SceneManager.MoveGameObjectToScene(model, scene);
                var camGo = new GameObject("cam");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.scene = scene;
                cam.orthographic = true;
                cam.orthographicSize = frameH * 0.5f;
                cam.aspect = frameW / frameH;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camGo.transform.position = new Vector3(b.center.x, bottom + frameH * 0.5f, b.min.z - 50f);
                camGo.transform.rotation = Quaternion.identity;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 50f + b.size.z + 50f;
                var rt = new RenderTexture(texW, texH, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.RenderWithShader(bake, "RenderType");
                var active = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, texW, texH), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                EditorSceneManager.ClosePreviewScene(scene);

                Dilate(tex);
                string png = BillboardDir + "/" + v.name + ".png";
                File.WriteAllBytes(png, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(png);
                var importer = (TextureImporter)AssetImporter.GetAtPath(png);
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();

                string matPath = BillboardDir + "/" + v.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(billboardShader); AssetDatabase.CreateAsset(mat, matPath); }
                mat.shader = billboardShader;
                mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.SetFloat("_Deciduous", TreeSpecies.Get(s).deciduous ? 1f : 0f);
                mat.SetVector("_BBRatios", new Vector4(frameW / v.nativeHeight, frameH / v.nativeHeight, bottom / v.nativeHeight, 0f));
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }
        }
        AssetDatabase.SaveAssets();
    }

    // the see-through pixels get the colour of the leaves next to them (no dark edges when the picture is small)
    static void Dilate(Texture2D tex)
    {
        int w = tex.width, h = tex.height;
        var px = tex.GetPixels();
        var filled = new bool[px.Length];
        for (int i = 0; i < px.Length; i++) filled[i] = px[i].a > 0.5f;
        for (int pass = 0; pass < 6; pass++)
        {
            var next = (bool[])filled.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (filled[i]) continue;
                    Color sum = Color.clear; int n = 0;
                    if (x > 0 && filled[i - 1]) { sum += px[i - 1]; n++; }
                    if (x < w - 1 && filled[i + 1]) { sum += px[i + 1]; n++; }
                    if (y > 0 && filled[i - w]) { sum += px[i - w]; n++; }
                    if (y < h - 1 && filled[i + w]) { sum += px[i + w]; n++; }
                    if (n == 0) continue;
                    var c = sum / n;
                    px[i] = new Color(c.r, c.g, c.b, 0f);
                    next[i] = true;
                }
            filled = next;
        }
        tex.SetPixels(px);
        tex.Apply();
    }
}
