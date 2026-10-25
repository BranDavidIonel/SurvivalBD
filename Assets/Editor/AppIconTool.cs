using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Nature > Make Game Icon (The Island)
// The game's icon (phone and PC): a picture of the island from the air (the sea, the forests, the mountain with its
// cross) with a warm light and a soft frame, saved in Assets/ART/Icons/AppIcon.png and set as the icon of all
// platforms (on Android also the round and the adaptive icons).
public static class AppIconTool
{
    const string IconPath = "Assets/ART/Icons/AppIcon.png";
    const string ForegroundPath = "Assets/ART/Icons/AppIconForeground.png";

    [MenuItem("Tools/Nature/Make Game Icon (The Island)")]
    public static void Make()
    {
        var ocean = Object.FindObjectOfType<Ocean>();
        Vector3 c = ocean != null ? ocean.transform.position : new Vector3(500f, 0f, 500f);
        var go = new GameObject("icon cam");
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.fieldOfView = 34f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 5f;
        cam.farClipPlane = 8000f;
        // from the south-west, high: the whole island in the middle, the sea around it
        go.transform.position = c + new Vector3(-1700f, 2150f, -2350f);
        go.transform.LookAt(c + new Vector3(40f, -40f, 60f));
        const int size = 1024, big = 2048;
        var rt = new RenderTexture(big, big, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var raw = new Texture2D(big, big, TextureFormat.RGB24, false);
        raw.ReadPixels(new Rect(0, 0, big, big), 0, 0);
        raw.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);

        // smaller (smooth), a little warmer and brighter, a soft dark edge
        var icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color s = (raw.GetPixel(x * 2, y * 2) + raw.GetPixel(x * 2 + 1, y * 2) + raw.GetPixel(x * 2, y * 2 + 1) + raw.GetPixel(x * 2 + 1, y * 2 + 1)) * 0.25f;
                s = new Color(Mathf.Pow(s.r, 0.92f) * 1.06f, Mathf.Pow(s.g, 0.92f) * 1.03f, Mathf.Pow(s.b, 0.92f) * 0.97f);
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float edge = Mathf.SmoothStep(0.36f, 0.72f, Mathf.Sqrt(dx * dx + dy * dy));
                s = Color.Lerp(s, s * 0.45f, edge);
                s.a = 1f;
                px[y * size + x] = s;
            }
        icon.SetPixels(px);
        icon.Apply();
        Object.DestroyImmediate(raw);
        Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
        File.WriteAllBytes(IconPath, icon.EncodeToPNG());
        Object.DestroyImmediate(icon);
        // an empty front layer for Android's adaptive icon (the picture is its background)
        var empty = new Texture2D(432, 432, TextureFormat.RGBA32, false);
        var clear = new Color[432 * 432];
        empty.SetPixels(clear);
        empty.Apply();
        File.WriteAllBytes(ForegroundPath, empty.EncodeToPNG());
        Object.DestroyImmediate(empty);
        foreach (var path in new[] { IconPath, ForegroundPath })
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = path == ForegroundPath;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);
        string docs = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Docs", "images");
        Directory.CreateDirectory(docs);
        File.Copy(IconPath, Path.Combine(docs, "app-icon.png"), true);

        // the icon of every platform
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { texture });
        foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
        {
            foreach (var kind in PlayerSettings.GetSupportedIconKindsForPlatform(group))
            {
                var icons = PlayerSettings.GetPlatformIcons(group, kind);
                foreach (var i in icons)
                {
                    if (i.maxLayerCount >= 2) i.SetTextures(texture, foreground); // adaptive: background, front
                    else i.SetTexture(texture);
                }
                PlayerSettings.SetPlatformIcons(group, kind, icons);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Game icon made: " + IconPath);
    }
}
