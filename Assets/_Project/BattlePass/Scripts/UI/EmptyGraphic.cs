using UnityEngine;
using UnityEngine.UI;

namespace VertigoDemo.UI
{
    /// <summary>
    /// Invisible raycast target. Unlike a transparent Image it submits no geometry,
    /// so it adds hit area without adding overdraw.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class EmptyGraphic : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }
}
