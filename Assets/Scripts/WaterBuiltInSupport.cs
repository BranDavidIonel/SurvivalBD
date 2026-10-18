using System.Collections.Generic;
using Bitgem.VFX.StylisedWater;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// The stylised water shader reads the scene depth (shore colour / foam) and the scene colour (refraction).
// URP provides both textures automatically; the Built-in Render Pipeline used by this project does not,
// so this sets them up on every camera that can see water. Nothing needs to be added to the scene.
public static class WaterBuiltInSupport
{
    const string BufferName = "Water: copy opaque texture";
    const CameraEvent BufferEvent = CameraEvent.BeforeForwardAlpha;

    static readonly int OpaqueTextureId = Shader.PropertyToID("_CameraOpaqueTexture");
    static readonly HashSet<Camera> preparedCameras = new HashSet<Camera>();

    static int waterLayers;
    static bool waterLayersDirty = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Camera.onPreCull -= OnPreCull;
        Camera.onPreCull += OnPreCull;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    static void EditorInit()
    {
        Init();
        UnityEditor.EditorApplication.hierarchyChanged -= MarkDirty;
        UnityEditor.EditorApplication.hierarchyChanged += MarkDirty;
    }
#endif

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => MarkDirty();
    static void OnSceneUnloaded(Scene scene) => MarkDirty();
    static void MarkDirty() => waterLayersDirty = true;

    static void OnPreCull(Camera cam)
    {
        // only needed for the Built-in pipeline, URP already does this
        if (GraphicsSettings.currentRenderPipeline != null) return;
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
        if ((cam.cullingMask & GetWaterLayers()) == 0) return;

        cam.depthTextureMode |= DepthTextureMode.Depth;

        if (preparedCameras.Contains(cam)) return;
        preparedCameras.RemoveWhere(c => c == null);
        preparedCameras.Add(cam);

        // after a script reload the camera can still have the buffer from before
        foreach (var existing in cam.GetCommandBuffers(BufferEvent))
        {
            if (existing.name == BufferName) return;
        }

        // copy what is on screen after the opaque objects are drawn, so the water can show it through itself
        var buffer = new CommandBuffer { name = BufferName };
        var format = cam.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        buffer.GetTemporaryRT(OpaqueTextureId, -1, -1, 0, FilterMode.Bilinear, format);
        buffer.Blit(BuiltinRenderTextureType.CurrentActive, OpaqueTextureId);
        cam.AddCommandBuffer(BufferEvent, buffer);
    }

    static int GetWaterLayers()
    {
        if (waterLayersDirty)
        {
            waterLayersDirty = false;
            waterLayers = 0;
            foreach (var water in Object.FindObjectsOfType<WaterVolumeBase>(true))
            {
                waterLayers |= 1 << water.gameObject.layer;
            }
        }
        return waterLayers;
    }
}
