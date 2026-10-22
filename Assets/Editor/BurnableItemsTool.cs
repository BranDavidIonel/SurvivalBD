using System.Linq;
using UnityEditor;
using UnityEngine;

// Marks the wood items as "Burnable" (a tag): only items with this tag (or listed in CookingData) can be put
// on a campfire. How long each one burns is in CookingData > Fuels.
// Runs by itself once (when the tag does not exist yet); Tools > Campfire > Mark Wood As Burnable runs it again.
[InitializeOnLoad]
public static class BurnableItemsTool
{
    // inventory item prefabs (Assets/Resources) that burn
    static readonly string[] WoodItems = { "stick", "plank", "log" };

    static BurnableItemsTool()
    {
        EditorApplication.delayCall += () =>
        {
            if (!UnityEditorInternal.InternalEditorUtility.tags.Contains(CookingData.BurnableTag)) MarkWood();
        };
    }

    [MenuItem("Tools/Campfire/Mark Wood As Burnable")]
    public static void MarkWood()
    {
        AddTag(CookingData.BurnableTag);
        int marked = 0;
        foreach (var name in WoodItems)
        {
            string path = "Assets/Resources/" + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            if (prefab.tag == CookingData.BurnableTag) { marked++; continue; }
            var root = PrefabUtility.LoadPrefabContents(path);
            root.tag = CookingData.BurnableTag;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            marked++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Campfire: " + marked + " wood items are tagged \"" + CookingData.BurnableTag + "\" (burn times: CookingData > Fuels).");
    }

    static void AddTag(string tag)
    {
        if (UnityEditorInternal.InternalEditorUtility.tags.Contains(tag)) return;
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
        if (asset == null) return;
        var manager = new SerializedObject(asset);
        var tags = manager.FindProperty("tags");
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        manager.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }
}
