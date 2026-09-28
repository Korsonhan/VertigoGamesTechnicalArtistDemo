using System;
using UnityEngine;
using UnityEngine.UI;

namespace VertigoDemo.UI
{
    /// <summary>Must match the FLAG_* defines in UIFx.shader.</summary>
    [Flags]
    public enum UIFxFlags
    {
        None = 0,
        Shine = 1,
        Pulse = 2,
        Additive = 4,
        Bob = 8,
        GoldCycle = 16,
    }

    /// <summary>
    /// Writes per-element parameters for the UIFx shader into UV1/UV2, so every element can share
    /// one material (and batch) while animating independently on the GPU. Parameters only touch
    /// the mesh when they change; the looping effects themselves run in the shader.
    /// The canvas needs TexCoord1 and TexCoord2 in its additional shader channels.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class UIFxMeshEffect : BaseMeshEffect
    {
        [Tooltip("Rect the shine and gradients are laid out in. Share one between the parts of a card so the sweep lines up across them.")]
        [SerializeField] RectTransform effectSpace;
        [SerializeField] UIFxFlags flags;
        [SerializeField, Range(0f, 1f)] float phase;
        [SerializeField, Range(0f, 1f)] float idle;
        [SerializeField, Range(0f, 1f)] float saturation = 1f;
        [SerializeField, Range(0f, 2f)] float brightness = 1f;
        [SerializeField, Range(0f, 1f)] float flash;

        public UIFxFlags Flags { get => flags; set { if (flags != value) { flags = value; Refresh(); } } }
        public float Phase { get => phase; set => Set(ref phase, value); }
        public float Idle { get => idle; set => Set(ref idle, value); }
        public float Saturation { get => saturation; set => Set(ref saturation, value); }
        public float Brightness { get => brightness; set => Set(ref brightness, value); }
        public float Flash { get => flash; set => Set(ref flash, value); }

        public void Configure(RectTransform space, UIFxFlags effectFlags)
        {
            effectSpace = space;
            flags = effectFlags;
            Refresh();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            var space = effectSpace != null ? effectSpace : (RectTransform)transform;
            Rect rect = space.rect;
            Matrix4x4 toSpace = space.worldToLocalMatrix * transform.localToWorldMatrix;
            float invWidth = rect.width > 0f ? 1f / rect.width : 0f;
            float invHeight = rect.height > 0f ? 1f / rect.height : 0f;
            // Encoded so that zero means "unchanged" (see UIFx.shader).
            var parameters = new Vector4(1f - saturation, brightness - 1f, flash, (float)flags);

            var vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Vector3 local = toSpace.MultiplyPoint3x4(vertex.position);
                vertex.uv1 = new Vector4((local.x - rect.xMin) * invWidth, (local.y - rect.yMin) * invHeight, phase, idle);
                vertex.uv2 = parameters;
                vh.SetUIVertex(vertex, i);
            }
        }

        void Set(ref float field, float value)
        {
            if (field == value)
                return;
            field = value;
            Refresh();
        }

        void Refresh()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }
    }
}
