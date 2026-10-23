using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Puts the night shadow (Assets/Resources/NightShadow.prefab) into the game scene, so it is easy to find in the
// Hierarchy as "Night Shadow" and its settings (like Seconds Until Attack) can be changed in the Inspector.
// It runs by itself when the game scene is open in the editor and has no shadow yet: save the scene to keep it.
// Menu SurvivalBD > Night Shadow selects it.
[InitializeOnLoad]
static class NightShadowSceneSetup
{
    const string PrefabPath = "Assets/Resources/NightShadow.prefab";

    static NightShadowSceneSetup()
    {
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += AddIfMissing;
        EditorApplication.delayCall += AddIfMissing;
    }

    [MenuItem("SurvivalBD/Night Shadow")]
    static void Select()
    {
        AddIfMissing();
        var shadow = Object.FindObjectOfType<NightShadow>();
        Object target = shadow != null ? shadow.gameObject : AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Selection.activeObject = target;
        if (target != null) EditorGUIUtility.PingObject(target);
    }

    static void AddIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var dayNight = Object.FindObjectOfType<DayNightSystem>(); // only the game scene has one
        if (dayNight == null) return;
        if (Object.FindObjectOfType<NightShadow>() != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;

        var scene = dayNight.gameObject.scene;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.name = "Night Shadow";
        go.transform.SetAsLastSibling();
        Undo.RegisterCreatedObjectUndo(go, "Add Night Shadow");
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Night Shadow added to the scene: select it in the Hierarchy to change Seconds Until Attack. Save the scene to keep it.");
    }
}
