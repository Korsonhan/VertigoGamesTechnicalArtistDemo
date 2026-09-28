using System;
using System.Collections.Generic;
using UnityEngine;

namespace VertigoDemo.WeaponVFX
{
    /// <summary>
    /// Procedural wind ribbons spiralling around the weapon's barrel axis (local Z, muzzle at +Z).
    /// Every ribbon goes into one mesh, so they cost a single draw call. Per-ribbon variation reaches
    /// the Wind Ribbon shader through vertex colour (R = seed, G = speed, B = brightness); UV.x runs
    /// from muzzle to tail and UV.y across the ribbon. Normals hold the outward direction for flutter.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WindRibbonMesh : MonoBehaviour
    {
        [Serializable]
        public struct Ribbon
        {
            [Tooltip("Local Z at the muzzle end (x) and at the tail (y).")]
            public Vector2 zRange;
            [Tooltip("Angle around the barrel axis at the start, in degrees: 90 is above, 180 faces the default camera.")]
            public float startAngle;
            [Tooltip("Turns around the axis over the ribbon's length. Negative spins the other way.")]
            public float turns;
            [Tooltip("Distance from the axis at the start (x) and at the tail (y).")]
            public Vector2 radius;
            [Tooltip("Height of the axis the ribbon orbits, at the start (x) and at the tail (y).")]
            public Vector2 axisHeight;
            public float width;
            [Range(0f, 2f)] public float brightness;
            [Range(0.5f, 1.5f)] public float speed;
        }

        [SerializeField] Ribbon[] ribbons = Array.Empty<Ribbon>();
        [SerializeField, Range(8, 128)] int segments = 64;
        [Tooltip("Squashes the orbit front to back: the rifle is much thinner than it is tall.")]
        [SerializeField, Range(0.3f, 1.5f)] float depthScale = 0.8f;

        Mesh mesh;
        bool dirty;

        public void Configure(Ribbon[] definitions, int segmentCount, float depth)
        {
            ribbons = definitions;
            segments = segmentCount;
            depthScale = depth;
            Rebuild();
        }

        void OnEnable() => Rebuild();

        // Rebuilding inside OnValidate is not allowed to touch other components, so defer it.
        void OnValidate() => dirty = true;

        void Update()
        {
            if (dirty)
                Rebuild();
        }

        void OnDestroy()
        {
            if (mesh == null)
                return;
            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);
        }

        public void Rebuild()
        {
            dirty = false;
            if (mesh == null)
                mesh = new Mesh { name = "WindRibbons (generated)", hideFlags = HideFlags.DontSave };

            int vertexCount = ribbons.Length * (segments + 1) * 2;
            var vertices = new List<Vector3>(vertexCount);
            var normals = new List<Vector3>(vertexCount);
            var uvs = new List<Vector2>(vertexCount);
            var colors = new List<Color>(vertexCount);
            var triangles = new List<int>(ribbons.Length * segments * 6);

            for (int i = 0; i < ribbons.Length; i++)
                AppendRibbon(ribbons[i], i, vertices, normals, uvs, colors, triangles);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void AppendRibbon(in Ribbon ribbon, int index, List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<Color> colors, List<int> triangles)
        {
            float seed = Mathf.Repeat(index * 0.618034f + 0.13f, 1f);
            var color = new Color(seed, Mathf.InverseLerp(0.5f, 1.5f, ribbon.speed), ribbon.brightness, 1f);
            int first = vertices.Count;
            const float step = 0.005f;

            for (int s = 0; s <= segments; s++)
            {
                float t = s / (float)segments;
                Vector3 position = Point(ribbon, t);
                Vector3 tangent = (Point(ribbon, Mathf.Min(1f, t + step)) - Point(ribbon, Mathf.Max(0f, t - step))).normalized;
                var axis = new Vector3(0f, Mathf.Lerp(ribbon.axisHeight.x, ribbon.axisHeight.y, t), position.z);
                Vector3 outward = (position - axis).normalized;
                // Lying on the orbit's surface, the ribbon turns edge-on as it wraps, which reads as a twist.
                Vector3 across = Vector3.Cross(tangent, outward).normalized;
                // Clamp: sin(PI) comes out a hair below zero in float, and a fractional power of that is NaN.
                float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.7f);
                float halfWidth = 0.5f * ribbon.width * taper * (0.55f + 0.45f * t);

                vertices.Add(position - across * halfWidth);
                vertices.Add(position + across * halfWidth);
                normals.Add(outward);
                normals.Add(outward);
                uvs.Add(new Vector2(t, 0f));
                uvs.Add(new Vector2(t, 1f));
                colors.Add(color);
                colors.Add(color);

                if (s == segments)
                    continue;
                int a = first + s * 2;
                triangles.Add(a);
                triangles.Add(a + 2);
                triangles.Add(a + 1);
                triangles.Add(a + 1);
                triangles.Add(a + 2);
                triangles.Add(a + 3);
            }
        }

        Vector3 Point(in Ribbon ribbon, float t)
        {
            float angle = (ribbon.startAngle + 360f * ribbon.turns * t) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(ribbon.radius.x, ribbon.radius.y, t * t * (3f - 2f * t));
            float height = Mathf.Lerp(ribbon.axisHeight.x, ribbon.axisHeight.y, t);
            float z = Mathf.Lerp(ribbon.zRange.x, ribbon.zRange.y, t);
            return new Vector3(Mathf.Cos(angle) * radius * depthScale, height + Mathf.Sin(angle) * radius, z);
        }
    }
}
