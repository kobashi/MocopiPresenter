using System.Collections.Generic;
using UnityEngine;

namespace Guidance
{
    /// <summary>
    /// 形を計算で作る道具箱。角を面取りした箱、面取りした多角柱（円柱・六角柱・円すい台）、輪を作れる。
    /// 同じ寸法の形は1つだけ作って使い回す。
    /// </summary>
    public static class MeshKit
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>
        /// 多角形を1枚ずつ足していく。各面は平らに見えるよう、頂点を面ごとに分ける。
        /// </summary>
        private sealed class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            /// <summary>
            /// 凸な多角形を足す。表裏は、原点から見て外側が表になるよう自動でそろえる。
            /// </summary>
            public void Add(params Vector3[] points)
            {
                Vector3 normal = Vector3.zero;
                Vector3 center = Vector3.zero;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 a = points[i];
                    Vector3 b = points[(i + 1) % points.Length];
                    normal += Vector3.Cross(a, b);
                    center += a;
                }

                if (normal.sqrMagnitude < 1e-12f)
                {
                    return;
                }

                normal.Normalize();
                // Unity では、辺の外積の向きがそのまま表側になる。内側を向いていたら並びを逆にする
                if (Vector3.Dot(normal, center) < 0f)
                {
                    System.Array.Reverse(points);
                    normal = -normal;
                }

                int start = this.Vertices.Count;
                Vector3 absolute = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                foreach (Vector3 point in points)
                {
                    this.Vertices.Add(point);
                    this.Normals.Add(normal);
                    // 面の向きに合わせて平面に投影した模様の座標（1m で1周）
                    this.Uvs.Add(absolute.y >= absolute.x && absolute.y >= absolute.z ? new Vector2(point.x, point.z)
                        : absolute.x >= absolute.z ? new Vector2(point.z, point.y) : new Vector2(point.x, point.y));
                }

                for (int i = 1; i < points.Length - 1; i++)
                {
                    this.Triangles.Add(start);
                    this.Triangles.Add(start + i);
                    this.Triangles.Add(start + i + 1);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(this.Vertices);
                mesh.SetNormals(this.Normals);
                mesh.SetUVs(0, this.Uvs);
                mesh.SetTriangles(this.Triangles, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }

        /// <summary>
        /// 角と辺を面取りした箱（中心が原点）。
        /// </summary>
        public static Mesh ChamferBox(Vector3 size, float bevel)
        {
            Vector3 h = size * 0.5f;
            float b = Mathf.Clamp(bevel, 0.0005f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.9f);
            string key = "Box_" + size.x.ToString("0.000") + "_" + size.y.ToString("0.000") + "_" + size.z.ToString("0.000") + "_" + b.ToString("0.000");
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null)
            {
                return cached;
            }

            // 各かどの近くに、X面・Y面・Z面それぞれの上の点を1つずつ置く
            Vector3 Px(float x, float y, float z) => new Vector3(x * h.x, y * (h.y - b), z * (h.z - b));
            Vector3 Py(float x, float y, float z) => new Vector3(x * (h.x - b), y * h.y, z * (h.z - b));
            Vector3 Pz(float x, float y, float z) => new Vector3(x * (h.x - b), y * (h.y - b), z * h.z);

            var builder = new Builder();
            foreach (float s in new[] { -1f, 1f })
            {
                builder.Add(Px(s, -1, -1), Px(s, 1, -1), Px(s, 1, 1), Px(s, -1, 1));
                builder.Add(Py(-1, s, -1), Py(1, s, -1), Py(1, s, 1), Py(-1, s, 1));
                builder.Add(Pz(-1, -1, s), Pz(1, -1, s), Pz(1, 1, s), Pz(-1, 1, s));
                foreach (float t in new[] { -1f, 1f })
                {
                    builder.Add(Py(-1, s, t), Py(1, s, t), Pz(1, s, t), Pz(-1, s, t));
                    builder.Add(Px(s, -1, t), Px(s, 1, t), Pz(s, 1, t), Pz(s, -1, t));
                    builder.Add(Px(s, t, -1), Px(s, t, 1), Py(s, t, 1), Py(s, t, -1));
                    foreach (float u in new[] { -1f, 1f })
                    {
                        builder.Add(Px(s, t, u), Py(s, t, u), Pz(s, t, u));
                    }
                }
            }

            return Cache[key] = builder.ToMesh(key);
        }

        /// <summary>
        /// 上下の縁を面取りした多角柱（中心が原点、軸は Y）。sides を増やすと円柱になる。
        /// topScale を 1 より小さくすると上がすぼまり、円すい台になる。
        /// </summary>
        public static Mesh Prism(int sides, float radius, float height, float bevel, float topScale = 1f)
        {
            float b = Mathf.Clamp(bevel, 0f, Mathf.Min(radius, height * 0.5f) * 0.9f);
            string key = "Prism_" + sides + "_" + radius.ToString("0.000") + "_" + height.ToString("0.000") + "_" + b.ToString("0.000") + "_" + topScale.ToString("0.00");
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null)
            {
                return cached;
            }

            Vector3[] Loop(float r, float y)
            {
                var points = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float angle = (i + 0.5f) * Mathf.PI * 2f / sides;
                    points[i] = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                }

                return points;
            }

            float half = height * 0.5f;
            float top = radius * topScale;
            var loops = new List<Vector3[]>
            {
                Loop(radius - b, -half),
                Loop(radius, -half + b),
                Loop(top, half - b),
                Loop(Mathf.Max(top - b, 0.0005f), half),
            };

            var builder = new Builder();
            builder.Add((Vector3[])loops[0].Clone());
            builder.Add((Vector3[])loops[3].Clone());
            for (int level = 0; level < 3; level++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    builder.Add(loops[level][i], loops[level][j], loops[level + 1][j], loops[level + 1][i]);
                }
            }

            return Cache[key] = builder.ToMesh(key);
        }

        /// <summary>
        /// 輪（ドーナツ形。中心が原点、Y 軸のまわり）。
        /// </summary>
        public static Mesh Torus(float radius, float tube, int segments = 64, int sides = 10)
        {
            string key = "Torus_" + radius.ToString("0.000") + "_" + tube.ToString("0.000") + "_" + segments + "_" + sides;
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null)
            {
                return cached;
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float major = i * Mathf.PI * 2f / segments;
                var outward = new Vector3(Mathf.Cos(major), 0f, Mathf.Sin(major));
                for (int j = 0; j <= sides; j++)
                {
                    float minor = j * Mathf.PI * 2f / sides;
                    Vector3 normal = outward * Mathf.Cos(minor) + Vector3.up * Mathf.Sin(minor);
                    vertices.Add(outward * radius + normal * tube);
                    normals.Add(normal);
                }
            }

            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j;
                    int c = a + sides + 1;
                    triangles.AddRange(new[] { a, a + 1, c, a + 1, c + 1, c });
                }
            }

            // 表裏が逆なら全部ひっくり返す
            Vector3 face = Vector3.Cross(vertices[triangles[1]] - vertices[triangles[0]], vertices[triangles[2]] - vertices[triangles[0]]);
            if (Vector3.Dot(face, normals[triangles[0]]) < 0f)
            {
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                }
            }

            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return Cache[key] = mesh;
        }
    }
}
