using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Mobile: test the phone controls in the editor and build the game for Android.
public static class MobileSetupTool
{
    const string ForceMenu = "Tools/Mobile/Force Touch Controls In Game View";
    const string ApkPath = "Builds/Android/SurvivalBD.apk";

    [MenuItem(ForceMenu, false, 1)]
    static void ToggleForceTouch()
    {
        bool value = !EditorPrefs.GetBool(GameInput.ForceTouchPrefKey, false);
        EditorPrefs.SetBool(GameInput.ForceTouchPrefKey, value);
        Debug.Log(value
            ? "Touch controls ON in the Game view (they also show automatically in the Simulator view). Restart Play mode."
            : "Touch controls OFF in the Game view (the Simulator view still shows them). Restart Play mode.");
    }

    [MenuItem(ForceMenu, true)]
    static bool ToggleForceTouchValidate()
    {
        Menu.SetChecked(ForceMenu, EditorPrefs.GetBool(GameInput.ForceTouchPrefKey, false));
        return true;
    }

    [MenuItem("Tools/Mobile/Apply Android Settings", false, 20)]
    public static void ApplyAndroidSettings()
    {
        // landscape only (the game is played with the phone held sideways)
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // 64-bit build (Redmi 9 and every recent phone), faster scripts than Mono
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel24)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

        // the app's id on the phone (instead of com.DefaultCompany.SurvivalBD)
        if (PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android).Contains("DefaultCompany"))
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.BranDavidIonel.SurvivalBD");

        // Pink lake on Vulkan phones: with "Apply display rotation during rendering" on, UnityCG.cginc needs
        // UNITY_DISPLAY_ORIENTATION_PRETRANSFORM, which the water Shader Graph (Built-in target) never declares,
        // so its Vulkan version does not compile. Android then rotates the picture itself (a tiny cost).
        PlayerSettings.vulkanEnablePreTransform = false;

        AssetDatabase.SaveAssets();
        FixShinyTerrainOnAndroid();
        Debug.Log("Android settings applied: landscape, IL2CPP, ARM64, Android 7.0+, no Vulkan pre-rotation, id " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
    }

    // On Android the terrain colour textures become ASTC, and Unity treats every ASTC texture as having an
    // alpha channel. The built-in terrain shader then takes the smoothness from that (fully opaque) alpha
    // instead of the layer's Smoothness, so the ground turns into a mirror of the sky (bright, hazy look).
    // Importing those textures as ETC2 RGB (no alpha) for Android only keeps the look the same as on PC.
    [MenuItem("Tools/Mobile/Fix Shiny Terrain On Android", false, 21)]
    public static int FixShinyTerrainOnAndroid()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:TerrainLayer"))
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(guid));
            if (layer == null || layer.diffuseTexture == null) continue;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(layer.diffuseTexture)) as TextureImporter;
            if (importer == null) continue;
            // a colour texture that really stores smoothness in its alpha keeps it (PC uses it too)
            if (importer.DoesSourceTextureHaveAlpha() && importer.alphaSource != TextureImporterAlphaSource.None) continue;

            var android = importer.GetPlatformTextureSettings("Android");
            if (android.overridden && android.format == TextureImporterFormat.ETC2_RGB4) continue;
            android.overridden = true;
            android.format = TextureImporterFormat.ETC2_RGB4;
            android.maxTextureSize = importer.GetDefaultPlatformTextureSettings().maxTextureSize;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            changed++;
        }
        if (changed > 0) Debug.Log("Terrain colour textures set to ETC2 RGB for Android: " + changed);
        return changed;
    }

    [MenuItem("Tools/Mobile/Build APK", false, 40)]
    static void BuildApk() => Build(false);

    [MenuItem("Tools/Mobile/Build And Run On Connected Phone", false, 41)]
    static void BuildAndRun() => Build(true);

    static void Build(bool run)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            EditorUtility.DisplayDialog("Android Build Support is missing",
                "To build for a phone, install the Android module for Unity 2021.3.21f1 " +
                "(Android Build Support + OpenJDK + Android SDK & NDK Tools) from Unity Hub.", "OK");
            return;
        }

        // the build uses the scenes saved on disk
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        ApplyAndroidSettings();
        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None,
        };
        EditorUserBuildSettings.buildAppBundle = false;

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("APK built: " + Path.GetFullPath(ApkPath));
            if (!run) EditorUtility.RevealInFinder(ApkPath);
        }
        else
        {
            Debug.LogError("Android build failed: " + report.summary.result + " (see the errors above in the Console)");
        }
    }
}
