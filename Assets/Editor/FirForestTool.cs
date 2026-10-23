using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Trees > Add Fir Forest: a "Fir_forest" group with 12 pines (like Birch_forest), where the Scene view
// is looking. Every pine is its own object: move, turn, delete or copy it (Ctrl+D), and set its Height and
// "Grows Up To" in its ScenePine. In the game they become living pines (PineForest).
public static class FirForestTool
{
    const string Menu = "Tools/Trees/Add Fir Forest (12 pines)";
    static readonly string[] Kinds = { "PineForest/Pine 1", "PineForest/Pine 3", "PineForest/Pine 5" };

    [MenuItem(Menu)]
    static void AddAtSceneView()
    {
        var view = SceneView.lastActiveSceneView;
        Vector3 at = view != null ? view.pivot : Vector3.zero;
        var group = Create(at, 12, Random.Range(1, 100000));
        if (group != null && view != null) view.LookAt(group.transform.position + Vector3.up * 15f, view.rotation, 120f);
    }

    // A Fir_forest group of `count` pines around `centre` (on the ground: not in water, on steep slopes or on
    // top of other things). seed: the same number gives the same pines.
    public static GameObject Create(Vector3 centre, int count, int seed)
    {
        var variants = PineForest.Variants;
        if (variants.Count == 0) { Debug.LogWarning("No pine models in Resources/PineForest"); return null; }

        var manager = Object.FindObjectOfType<EnviromentManager>();
        Transform parent = manager != null && manager.allTrees != null ? manager.allTrees.transform : null;

        var group = new GameObject(UniqueName(parent, "Fir_forest"));
        Undo.RegisterCreatedObjectUndo(group, "Add Fir Forest");
        if (parent != null) group.transform.SetParent(parent, true);
        group.transform.position = new Vector3(centre.x, PineForest.GroundY(centre), centre.z);

        var rng = new System.Random(seed);
        var placed = new List<Vector3>();
        const float spacing = 6.5f, radius = 26f;
        for (int attempt = 0; attempt < count * 40 && placed.Count < count; attempt++)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float r = radius * Mathf.Sqrt((float)rng.NextDouble());
            Vector3 p = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            int kind = rng.Next(0, variants.Count);
            double age = rng.NextDouble(), size = rng.NextDouble(), full = rng.NextDouble(), turn = rng.NextDouble();

            bool tooClose = false;
            foreach (var q in placed) if ((new Vector2(q.x - p.x, q.z - p.z)).sqrMagnitude < spacing * spacing) tooClose = true;
            if (tooClose || !PineForest.IsGoodGround(p, -8f, true, group.transform)) continue; // (its own pines: the spacing decides)

            // a few young pines, most of them grown
            float growsUpTo = Mathf.Round(Mathf.Lerp(30f, 40f, (float)full));
            float height = age < 0.25 ? Mathf.Lerp(4f, 14f, (float)size) : Mathf.Lerp(18f, growsUpTo - 1f, (float)size);
            height = Mathf.Round(height * 10f) / 10f;

            var v = variants[kind];
            var prefab = Resources.Load<GameObject>(Kinds[Mathf.Min(kind, Kinds.Length - 1)]);
            var pine = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            pine.name = "Fir " + (placed.Count + 1);
            float k = height / v.nativeHeight;
            pine.transform.localScale = v.prefabScale * k;
            pine.transform.position = new Vector3(p.x, PineForest.GroundY(p) - v.baseOffset * k - 0.15f, p.z);
            pine.transform.rotation = Quaternion.Euler(0f, (float)turn * 360f, 0f);
            var settings = Undo.AddComponent<ScenePine>(pine);
            settings.height = height;
            settings.growsUpTo = growsUpTo;
            placed.Add(p);
        }

        EditorSceneManager.MarkSceneDirty(group.scene);
        Selection.activeGameObject = group;
        Debug.Log("Fir_forest: " + placed.Count + " pines at " + group.transform.position + " (save the scene to keep them)");
        return group;
    }

    static string UniqueName(Transform parent, string name)
    {
        var used = new HashSet<string>();
        if (parent != null) foreach (Transform child in parent) used.Add(child.name);
        else foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects()) used.Add(root.name);
        if (!used.Contains(name)) return name;
        for (int i = 1; ; i++)
        {
            string candidate = name + " (" + i + ")";
            if (!used.Contains(candidate)) return candidate;
        }
    }
}
