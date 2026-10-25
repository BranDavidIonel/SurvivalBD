using System.Collections.Generic;
using UnityEngine;

// Hay from the grass of the meadows: looking down at grassy ground (not the dark forest floor, the sand, rock or
// snow) the player can "Pick grass (hay)" anywhere (SelectionManager). A spot that was picked needs a game day to
// grow back. This way hay for the fire is easy to find, without thousands of grass objects in the scene.
public static class GrassPicking
{
    public const float Reach = 4.5f;
    public const float RegrowHours = 24f;

    static readonly List<Vector3> picked = new List<Vector3>(); // x, z, clock hour
    static float nextCheck;
    static bool lastResult;
    static Vector3 lastSpot;
    static TerrainData cachedData;
    static int grassLayer = -1, floorLayer = -1, sandLayer = -1, rockLayer = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        picked.Clear();
        cachedData = null;
        nextCheck = 0f;
    }

    static float Clock => Vegetation.Instance != null ? Vegetation.Instance.Clock : Time.time / 15f;

    public static bool CanPick(Ray ray, out Vector3 spot)
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 0.1f;
            lastResult = Check(ray, out lastSpot);
        }
        spot = lastSpot;
        return lastResult;
    }

    static bool Check(Ray ray, out Vector3 spot)
    {
        spot = Vector3.zero;
        if (!Physics.Raycast(ray, out RaycastHit hit, Reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        if (!(hit.collider is TerrainCollider)) return false;
        var terrain = hit.collider.GetComponent<Terrain>();
        if (terrain == null) return false;
        var weather = WeatherSystem.Instance;
        if (weather != null && weather.SnowCover > 0.5f) return false;
        if (Ocean.Instance != null && hit.point.y < Ocean.Instance.Level + 0.5f) return false;

        var data = terrain.terrainData;
        if (data != cachedData) FindLayers(data);
        if (grassLayer < 0) return false;
        Vector3 local = hit.point - terrain.transform.position;
        int ar = data.alphamapResolution;
        int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * ar), 0, ar - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * ar), 0, ar - 1);
        var a = data.GetAlphamaps(x, z, 1, 1);
        float grass = a[0, 0, grassLayer];
        float shade = floorLayer >= 0 ? a[0, 0, floorLayer] : 0f;
        float sand = sandLayer >= 0 ? a[0, 0, sandLayer] : 0f;
        float rock = rockLayer >= 0 ? a[0, 0, rockLayer] : 0f;
        if (grass < 0.45f || shade > 0.35f || sand > 0.4f || rock > 0.5f) return false;

        float now = Clock;
        foreach (var p in picked)
        {
            float dx = p.x - hit.point.x, dz = p.y - hit.point.z;
            if (dx * dx + dz * dz < 3f * 3f && now - p.z < RegrowHours) return false;
        }
        spot = hit.point;
        return true;
    }

    static void FindLayers(TerrainData data)
    {
        cachedData = data;
        grassLayer = floorLayer = sandLayer = rockLayer = -1;
        var layers = data.terrainLayers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            string name = layers[i].name.ToLowerInvariant();
            if (grassLayer < 0 && name.Contains("grass")) grassLayer = i;
            if (name.Contains("forestfloor")) floorLayer = i;
            if (name.Contains("sand")) sandLayer = i;
            if (rockLayer < 0 && name.Contains("rock")) rockLayer = i;
        }
    }

    public static void Pick(Vector3 spot)
    {
        var inventory = InventorySystem.Instance;
        if (inventory == null) return;
        if (!inventory.CheckSlotsAvailable(1)) { inventory.ShowMessage("Inventory is full"); return; }
        inventory.AddToIventory("hay");
        picked.Add(new Vector3(spot.x, spot.z, Clock));
        if (picked.Count > 300) picked.RemoveAt(0);
        lastResult = false;
        nextCheck = 0f;
    }
}
