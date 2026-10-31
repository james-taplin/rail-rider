using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RailRider
{
    /// <summary>
    /// Minimal runtime loader for the placeholder GLB (one mesh per named node, baked positions, COLOR_0).
    /// The supplied file is X-forward / Y-lateral / Z-up (not glTF Y-up), so positions are remapped to
    /// Unity as (-y, z, x): muzzle toward +Z, up +Y. Each node becomes a child named after the node and is
    /// flat-coloured with its average vertex colour. Replace the file in Models\ to swap the art.
    /// </summary>
    static class GlbLoader
    {
        static Material baseMat;

        static Material NewMat(Color c)
        {
            if (baseMat == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMat = tmp.GetComponent<Renderer>().sharedMaterial;
                UnityEngine.Object.DestroyImmediate(tmp);
            }
            var m = new Material(baseMat) { color = c };
            return m;
        }

        public static Vector3 Remap(Vector3 p) => new Vector3(-p.y, p.z, p.x);

        public static bool TryLoad(string glbPath, string platePngPath, Transform parent)
        {
            try
            {
                if (!File.Exists(glbPath)) return false;
                byte[] d = File.ReadAllBytes(glbPath);
                int jl = BitConverter.ToInt32(d, 12);
                var j = JObject.Parse(Encoding.UTF8.GetString(d, 20, jl));
                int bin = 20 + jl + 8;

                foreach (var node in (JArray)j["nodes"])
                {
                    if (node["mesh"] == null) continue;
                    string name = (string)node["name"];
                    var prim = j["meshes"][(int)node["mesh"]]["primitives"][0];
                    var pos = ReadVec(j, d, bin, (int)prim["attributes"]["POSITION"], 3);
                    var col = ReadVec(j, d, bin, (int)prim["attributes"]["COLOR_0"], 4);
                    var idx = ReadIdx(j, d, bin, (int)prim["indices"]);

                    Color avg = Color.gray; // vertex colours are 8-bit
                    if (col.Length > 0)
                    {
                        Vector4 s = Vector4.zero;
                        foreach (var c in col) s += c;
                        s /= col.Length * 255f;
                        avg = new Color(s.x, s.y, s.z, 1f);
                    }

                    // Flat shading: three unique vertices per triangle. Reflection (RH->LH) keeps index order.
                    var verts = new Vector3[idx.Length];
                    var tris = new int[idx.Length];
                    for (int i = 0; i < idx.Length; i++) { verts[i] = Remap(new Vector3(pos[idx[i]].x, pos[idx[i]].y, pos[idx[i]].z)); tris[i] = i; }
                    var mesh = new Mesh { name = name };
                    mesh.vertices = verts; mesh.triangles = tris;
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();

                    var go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = NewMat(avg);

                    if (name == "brand_plate" && File.Exists(platePngPath)) AddPlate(go.transform, mesh.bounds, platePngPath);
                }
                return true;
            }
            catch (Exception e) { Main.Warn("GLB load failed, using cube placeholder: " + e.Message); return false; }
        }

        // Textured quads just outside both thin faces (box thickness is along Unity X).
        static void AddPlate(Transform parent, Bounds b, string png)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            tex.LoadImage(File.ReadAllBytes(png));
            var mat = NewMat(Color.white); mat.mainTexture = tex;
            for (int side = -1; side <= -1; side += 2) // near (left) side only: the side the player sees
            {
                Vector3 n = new Vector3(side, 0, 0), r = new Vector3(0, 0, side), up = Vector3.up;
                Vector3 c = b.center + n * (b.extents.x + 0.0005f);
                Vector3 hw = r * b.extents.z, hh = up * b.extents.y;
                var m = new Mesh { name = "plate" + side };
                m.vertices = new[] { c - hw - hh, c - hw + hh, c + hw + hh, c + hw - hh };
                m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
                m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                m.RecalculateNormals(); m.RecalculateBounds();
                var go = new GameObject("brand_plate_face" + side);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        static (int off, int count, int comp, int type) Acc(JObject j, int i)
        {
            var a = j["accessors"][i]; var bv = j["buffers"] == null ? null : j["bufferViews"][(int)a["bufferView"]];
            int n = (string)a["type"] == "VEC3" ? 3 : (string)a["type"] == "VEC4" ? 4 : 1;
            return (((int?)bv["byteOffset"] ?? 0) + ((int?)a["byteOffset"] ?? 0), (int)a["count"], n, (int)a["componentType"]);
        }

        static Vector4[] ReadVec(JObject j, byte[] d, int bin, int acc, int comps)
        {
            var (off, count, n, type) = Acc(j, acc);
            var r = new Vector4[count];
            for (int k = 0; k < count; k++)
            {
                var v = Vector4.zero;
                for (int c = 0; c < n; c++)
                    v[c] = type == 5126 ? BitConverter.ToSingle(d, bin + off + (k * n + c) * 4) : d[bin + off + k * n + c]; // float or ubyte
                r[k] = v;
            }
            return r;
        }

        static int[] ReadIdx(JObject j, byte[] d, int bin, int acc)
        {
            var (off, count, _, type) = Acc(j, acc);
            var r = new int[count];
            for (int k = 0; k < count; k++)
                r[k] = type == 5125 ? (int)BitConverter.ToUInt32(d, bin + off + k * 4)
                     : type == 5123 ? BitConverter.ToUInt16(d, bin + off + k * 2) : d[bin + off + k];
            return r;
        }
    }
}
