using System;
using System.Collections.Generic;
using UnityEngine;

namespace VertigoDemo.WeaponVFX
{
    /// <summary>
    /// Procedural wind ribbons flowing along the rifle, from the muzzle (local +Z) towards the stock.
    /// Each ribbon follows a smooth path through a few control points, and every ribbon goes into one
    /// mesh, so they cost a single draw call. Per-ribbon variation reaches the Wind Ribbon shader
    /// through vertex colour (R = seed, G = speed, B = brightness); UV.x runs along the ribbon by arc
    /// length and UV.y across it. Normals hold the direction the ribbon flutters in.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WindRibbonMesh : MonoBehaviour
    {
        [Serializable]
        public struct Ribbon
        {
            [Tooltip("Control points from the muzzle end to the tail, in the rifle's local space (muzzle at +Z).")]
            public Vector3[] path;
            [Tooltip("Roll around the path at the start (x) and at the tail (y), in degrees: 0 lies flat and is seen edge-on from the side, 90 stands up and faces the side view.")]
            public Vector2 roll;
            public float width;
            [Range(0f, 2f)] public float brightness;
            [Range(0.5f, 1.5f)] public float speed;
        }

        [SerializeField] Ribbon[] ribbons = Array.Empty<Ribbon>();
        [SerializeField, Range(8, 128)] int segments = 48;

        Mesh mesh;
        bool dirty;

        public void Configure(Ribbon[] definitions, int segmentCount)
        {
            ribbons = definitions;
            segments = segmentCount;
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
            {
                if (ribbons[i].path != null && ribbons[i].path.Length >= 2)
                    AppendRibbon(ribbons[i], i, vertices, normals, uvs, colors, triangles);
            }

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

            // Sample the path and measure it, so the flow scrolls at an even speed along the ribbon.
            var points = new Vector3[segments + 1];
            var distances = new float[segments + 1];
            for (int s = 0; s <= segments; s++)
            {
                points[s] = PathPoint(ribbon.path, s / (float)segments);
                if (s > 0)
                    distances[s] = distances[s - 1] + Vector3.Distance(points[s - 1], points[s]);
            }
            float length = Mathf.Max(distances[segments], 1e-4f);

            for (int s = 0; s <= segments; s++)
            {
                float t = s / (float)segments;
                Vector3 tangent = (points[Mathf.Min(s + 1, segments)] - points[Mathf.Max(s - 1, 0)]).normalized;
                Vector3 flat = Vector3.Cross(tangent, Vector3.up);
                flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.right;
                // Rolling the width around the path lets a ribbon turn from a sheet into a thin line.
                Vector3 across = Quaternion.AngleAxis(Mathf.Lerp(ribbon.roll.x, ribbon.roll.y, t), tangent) * flat;
                Vector3 facing = Vector3.Cross(across, tangent);
                // Flutter half sideways and half off the face, so it reads from every view angle.
                Vector3 flutter = (across + facing).normalized;
                // Thin at both ends. Clamp: sin(PI) comes out a hair below zero in float, and a fractional power of that is NaN.
                float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.8f);
                float halfWidth = 0.5f * ribbon.width * taper;
                float u = distances[s] / length;

                vertices.Add(points[s] - across * halfWidth);
                vertices.Add(points[s] + across * halfWidth);
                normals.Add(flutter);
                normals.Add(flutter);
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
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

        // Catmull-Rom spline through the control points, t from 0 (first point) to 1 (last point).
        static Vector3 PathPoint(Vector3[] path, float t)
        {
            int spans = path.Length - 1;
            float scaled = Mathf.Clamp01(t) * spans;
            int i = Mathf.Min((int)scaled, spans - 1);
            float f = scaled - i;
            Vector3 p0 = path[Mathf.Max(i - 1, 0)];
            Vector3 p1 = path[i];
            Vector3 p2 = path[i + 1];
            Vector3 p3 = path[Mathf.Min(i + 2, spans)];
            return 0.5f * (2f * p1 + (p2 - p0) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (f * f) + (3f * p1 - p0 - 3f * p2 + p3) * (f * f * f));
        }
    }
}
