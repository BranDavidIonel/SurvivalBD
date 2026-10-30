using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Nature > 6. Make The Spear Item
// The spear's inventory item, Assets/Resources/spear.prefab (taken in the hand from the quick bar, like the torch), and
// its icon, Assets/ART/Icons/spear.png, drawn from the model the game makes in code (Spear.BuildModel). The spear in
// the hand, flying and stuck in the ground is made in code too (SpearHeld, ThrownSpear): no other prefab is needed.
// Running it again remakes both. From a script: SpearAssetsTool.MakeSpearItem().
public static class SpearAssetsTool
{
    const string IconDir = "Assets/ART/Icons";
    public const string ItemPath = "Assets/Resources/spear.prefab";
    const string Template = "Assets/Resources/pinecone.prefab"; // an inventory item (picture, drag and drop, info)

    [MenuItem("Tools/Nature/6. Make The Spear Item")]
    public static void MakeSpearItem()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Template) == null) { Debug.LogWarning("Spear: no " + Template); return; }
        Directory.CreateDirectory(IconDir);
        var sprite = MakeIcon();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath) == null) AssetDatabase.CopyAsset(Template, ItemPath);
        var item = PrefabUtility.LoadPrefabContents(ItemPath);
        try
        {
            item.name = Spear.Item;
            item.tag = "Untagged";
            var info = item.GetComponent<InventoryItem>();
            info.thisName = Spear.Name;
            info.thisDescription = "A straight shaft with a sharp stone point tied on with cord.";
            info.thisFunctionality = "Put it in the quick bar and take it in your hand: the + shows where it goes. Click (the Throw button) to throw it at a rabbit 10-15 m away, then take it back from where it landed.";
            info.isEquippable = true;   // into the quick bar, then in the hand
            info.isUseable = false;
            info.isConsumable = false;
            info.healthEffect = info.caloriesEffect = info.hydrationEffect = 0f;
            info.isTrashable = true;
            foreach (var weapon in item.GetComponents<Weapon>()) Object.DestroyImmediate(weapon, true); // (thrown, not swung)
            var image = item.GetComponent<UnityEngine.UI.Image>();
            if (image != null && sprite != null) image.sprite = sprite;
            PrefabUtility.SaveAsPrefabAsset(item, ItemPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(item); }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Spear: item " + ItemPath + " and icon " + IconDir + "/spear.png made");
    }

    // the spear across the picture, its point at the top right, seen a little from above
    static Sprite MakeIcon()
    {
        string target = IconDir + "/" + Spear.Item + ".png";
        Quaternion view = Quaternion.Euler(-18f, 25f, 0f);
        Vector3 forward = view * Vector3.back;  // where the picture's camera looks (NatureAssetsTool.RenderModel)
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 up = Vector3.Cross(forward, right);
        var model = Spear.BuildModel("Spear (icon)");
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation((right + up).normalized, forward));
        var picture = NatureAssetsTool.RenderModel(model, 256, 256, view, false);
        if (model != null) Object.DestroyImmediate(model);
        File.WriteAllBytes(target, picture.EncodeToPNG());
        Object.DestroyImmediate(picture);
        AssetDatabase.ImportAsset(target);
        return NatureAssetsTool.ImportSprite(target);
    }
}
