using UnityEngine;
using UnityEngine.UI;

// Invisible UI rectangle that still receives touches (draws nothing, so it costs nothing to render).
[RequireComponent(typeof(CanvasRenderer))]
public class TouchZoneGraphic : Graphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
    }
}
