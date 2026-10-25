using UnityEngine;

// A candle of the old monk in the cave. They burnt out long ago (the cave is pitch dark); "lit" makes one burn
// with a small warm flickering light.
public class CaveCandle : MonoBehaviour
{
    public bool lit;
    public float intensity = 1.6f;
    public float range = 7f;

    Light candleLight;
    Transform flame;
    float seed;

    void Start()
    {
        seed = Random.Range(0f, 100f);
        flame = transform.Find("Flame");
        candleLight = GetComponentInChildren<Light>();
        if (candleLight == null)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.35f;
            candleLight = go.AddComponent<Light>();
            candleLight.type = LightType.Point;
            candleLight.color = new Color(1f, 0.7f, 0.35f);
            candleLight.shadows = LightShadows.None;
        }
        candleLight.range = range;
    }

    void Update()
    {
        if (flame != null && flame.gameObject.activeSelf != lit) flame.gameObject.SetActive(lit);
        if (candleLight == null) return;
        candleLight.enabled = lit;
        if (!lit) return;
        float t = Time.time * 7f + seed;
        candleLight.intensity = intensity * (0.82f + 0.18f * Mathf.PerlinNoise(t, seed));
    }
}
