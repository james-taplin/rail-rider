using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RailRider
{
    /// <summary>
    /// Runtime loader for a standard glTF 2.0 binary (+Y up, muzzle +Z, right-handed): node TRS, meshes with
    /// normals/UVs, factor + embedded-texture materials. Converted to Unity by mirroring X and reversing winding.
    /// Every node becomes a child named after the node, so gameplay finds Lever / RearSight / ... by name.
    /// </summary>
    static class GltfLoader
    {
        static Material baseMat;

        /// <summary>Animation clips of the most recent successful TryLoad (null when the file has none). Targets are node names.</summary>
        public static List<GltfClip> LastClips;

        static Material NewMat(JToken m, Texture2D tex, Texture2D normalTex)
        {
            if (baseMat == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMat = tmp.GetComponent<Renderer>().sharedMaterial;
                UnityEngine.Object.DestroyImmediate(tmp);
            }
            var mat = new Material(baseMat);
            var pbr = m?["pbrMetallicRoughness"];
            var f = pbr?["baseColorFactor"] as JArray;
            if (f != null) mat.color = new Color((float)f[0], (float)f[1], (float)f[2], 1f);
            if (tex != null) { mat.mainTexture = tex; mat.color = Color.white; }
            float rough = (float?)pbr?["roughnessFactor"] ?? 0.5f, metal = (float?)pbr?["metallicFactor"] ?? 0f;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 1f - rough);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metal);
            if (normalTex != null)
            {
                if (mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", normalTex);
                    if (mat.HasProperty("_BumpScale"))
                        mat.SetFloat("_BumpScale", (float?)m?["normalTexture"]?["scale"] ?? 1f);
                    mat.EnableKeyword("_NORMALMAP");
                }
                else Main.Warn("glTF normal map unsupported by shader " + mat.shader.name);
            }
            return mat;
        }

        public static bool TryLoad(string path, Transform parent, string mirrorEngravingOnMesh = null)
        {
            try
            {
                LastClips = null;
                if (!File.Exists(path)) return false;
                byte[] d = File.ReadAllBytes(path);
                int jl = BitConverter.ToInt32(d, 12);
                var j = JObject.Parse(Encoding.UTF8.GetString(d, 20, jl));
                int bin = 20 + jl + 8;

                var textures = new Dictionary<int, Texture2D>();
                Texture2D Tex(int imageIdx, bool normalMap = false)
                {
                    // Colour and normal uses of an image need different colour space/packing.
                    int key = normalMap ? ~imageIdx : imageIdx;
                    if (textures.TryGetValue(key, out var t)) return t;
                    var bv = j["bufferViews"][(int)j["images"][imageIdx]["bufferView"]];
                    int off = bin + ((int?)bv["byteOffset"] ?? 0), len = (int)bv["byteLength"];
                    var bytes = new byte[len]; Buffer.BlockCopy(d, off, bytes, 0, len);
                    t = new Texture2D(2, 2, TextureFormat.RGBA32, true, normalMap);
                    t.LoadImage(bytes);
                    if (normalMap)
                    {
                        // Runtime LoadImage bypasses Unity's normal-map importer. The Windows
                        // built-in shader expects AG (DXT5nm) or RG-or-AG, not ordinary RGB.
                        // (1, Y, 1, X) supports both unpackers. Retain green: UV conversion
                        // below restores Blender's UV direction and tangents are rebuilt there.
                        var pixels = t.GetPixels32();
                        for (int k = 0; k < pixels.Length; k++)
                        {
                            var p = pixels[k];
                            pixels[k] = new Color32(255, p.g, 255, p.r);
                        }
                        t.SetPixels32(pixels);
                        t.Apply(true, false);
                    }
                    if (t.width % 4 == 0 && t.height % 4 == 0)
                    {
                        // Block-compress (about 4:1) and drop the CPU copy; the repacked normal map suits DXT5nm.
                        t.Compress(true);
                        t.Apply(false, true);
                    }
                    t.anisoLevel = 4;
                    return textures[key] = t;
                }

                var mats = new Dictionary<int, Material>();
                Material Mat(int i)
                {
                    if (mats.TryGetValue(i, out var m)) return m;
                    var jm = j["materials"][i];
                    var ti = jm["pbrMetallicRoughness"]?["baseColorTexture"]?["index"];
                    Texture2D tex = ti != null ? Tex((int)j["textures"][(int)ti]["source"]) : null;
                    var ni = jm["normalTexture"]?["index"];
                    Texture2D normalTex = ni != null ? Tex((int)j["textures"][(int)ni]["source"], true) : null;
                    return mats[i] = NewMat(jm, tex, normalTex);
                }

                float frontY = 0f; // blade tip height: the rear leaf is cut flush with it
                foreach (var nd in (JArray)j["nodes"])
                    if ((string)nd["name"] == "FrontSight" && nd["translation"] is JArray ft) frontY = (float)ft[1];

                var nodeArr = (JArray)j["nodes"];
                var gos = new GameObject[nodeArr.Count];
                for (int ni = 0; ni < nodeArr.Count; ni++)
                {
                    var node = nodeArr[ni];
                    string name = (string)node["name"];
                    var go = new GameObject(name);
                    gos[ni] = go;
                    go.transform.SetParent(parent, false);
                    if (node["translation"] is JArray t)
                        go.transform.localPosition = new Vector3(-(float)t[0], (float)t[1], (float)t[2]);
                    if (node["rotation"] is JArray q)
                        go.transform.localRotation = new Quaternion((float)q[0], -(float)q[1], -(float)q[2], (float)q[3]);
                    if (node["mesh"] == null) continue;

                    var prim = j["meshes"][(int)node["mesh"]]["primitives"][0];
                    var at = prim["attributes"];
                    var pos = ReadVec(j, d, bin, (int)at["POSITION"]);
                    var nor = at["NORMAL"] != null ? ReadVec(j, d, bin, (int)at["NORMAL"]) : null;
                    var uv = at["TEXCOORD_0"] != null ? ReadVec(j, d, bin, (int)at["TEXCOORD_0"]) : null;
                    int[] idx = prim["indices"] != null ? ReadIdx(j, d, bin, (int)prim["indices"]) : null;
                    if (idx == null) { idx = new int[pos.Length]; for (int i = 0; i < idx.Length; i++) idx[i] = i; }

                    // Marked stock face is on the model's -X (rifle right); the player sees the left side. Swap the
                    // texture's marked/clean halves for this mesh and mirror u so the text still reads correctly.
                    float nodeY = node["translation"] is JArray nt ? (float)nt[1] : 0f;
                    float shift = 0f;
                    if (uv != null && name == mirrorEngravingOnMesh)
                    {
                        float sn = 0, sp = 0; int cn = 0, cp = 0;
                        for (int i = 0; i < pos.Length; i++) { if (pos[i].x < -0.003f) { sn += uv[i].y; cn++; } else if (pos[i].x > 0.003f) { sp += uv[i].y; cp++; } }
                        if (cn > 0 && cp > 0) shift = sn / cn - sp / cp;
                    }

                    var verts = new Vector3[pos.Length];
                    var norms = new Vector3[pos.Length];
                    var uvs = new Vector2[pos.Length];
                    for (int i = 0; i < pos.Length; i++)
                    {
                        verts[i] = new Vector3(-pos[i].x, pos[i].y, pos[i].z);
                        if (name == "Rear sight leaf" && frontY > 0f && verts[i].y > frontY) verts[i].y = frontY;
                        if (frontY > 0f)
                        {
                            // Rear notch: top flush with the blade tip, base sunk into the receiver so nothing floats.
                            float wy = verts[i].y + nodeY;
                            if (name == "Rear notch - open U")
                            {
                                if (wy > frontY) verts[i].y = frontY - nodeY;
                                else if (wy < 0.0345f) verts[i].y = 0.0300f - nodeY;
                            }
                            else if (name == "Rear sight spring" && wy < 0.0330f) verts[i].y = 0.0305f - nodeY;
                        }
                        norms[i] = nor != null ? new Vector3(-nor[i].x, nor[i].y, nor[i].z) : Vector3.up;
                        if (uv != null)
                        {
                            float u = uv[i].x, v = uv[i].y;
                            if (shift != 0f)
                            {
                                if (pos[i].x > 0.003f) { v += shift; u = 0.64f - u; } // clean face becomes the marked, mirrored-read face
                                else if (pos[i].x < -0.003f) v -= shift;
                            }
                            uvs[i] = new Vector2(u, 1f - v); // glTF v is top-down
                        }
                    }
                    var tris = new int[idx.Length];
                    for (int i = 0; i < idx.Length; i += 3) { tris[i] = idx[i]; tris[i + 1] = idx[i + 2]; tris[i + 2] = idx[i + 1]; } // mirror => reverse winding
                    var mesh = new Mesh { name = name, indexFormat = verts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                    mesh.vertices = verts; mesh.normals = norms; mesh.triangles = tris;
                    if (uv != null)
                    {
                        mesh.uv = uvs;
                        // Derive the tangent frame after coordinate/UV conversion for relief lighting.
                        mesh.RecalculateTangents();
                    }
                    mesh.RecalculateBounds();
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = Mat((int?)prim["material"] ?? 0);
                }
                // Hierarchy (flat files have none): children keep their local transform under their parent node.
                for (int ni = 0; ni < nodeArr.Count; ni++)
                    if (nodeArr[ni]["children"] is JArray kids)
                        foreach (var k in kids) gos[(int)k].transform.SetParent(gos[ni].transform, false);
                LastClips = ParseClips(j, d, bin);
                return true;
            }
            catch (Exception e) { Main.Warn("glTF load failed: " + e); return false; }
        }

        static List<GltfClip> ParseClips(JObject j, byte[] d, int bin)
        {
            if (!(j["animations"] is JArray anims) || anims.Count == 0) return null;
            var nodes = (JArray)j["nodes"];
            var list = new List<GltfClip>();
            foreach (var a in anims)
            {
                var clip = new GltfClip { name = (string)a["name"] };
                var samplers = (JArray)a["samplers"];
                foreach (var c in (JArray)a["channels"])
                {
                    string path = (string)c["target"]["path"];
                    if (path != "rotation" && path != "translation" && path != "scale") continue;
                    var s = samplers[(int)c["sampler"]];
                    if (((string)s["interpolation"] ?? "LINEAR") != "LINEAR") Main.Warn("glTF clip " + clip.name + ": only LINEAR interpolation is played");
                    float[] times = ReadFloats(j, d, bin, (int)s["input"], 1);
                    int comps = path == "rotation" ? 4 : 3;
                    float[] v = ReadFloats(j, d, bin, (int)s["output"], comps);
                    var ch = new GltfChannel { target = (string)nodes[(int)c["target"]["node"]]["name"], path = path, times = times, vals = new Vector4[times.Length] };
                    for (int i = 0; i < times.Length; i++)
                    {
                        if (path == "rotation") ch.vals[i] = new Vector4(v[i * 4], -v[i * 4 + 1], -v[i * 4 + 2], v[i * 4 + 3]); // same X-mirror conversion as node rotations
                        else if (path == "translation") ch.vals[i] = new Vector4(-v[i * 3], v[i * 3 + 1], v[i * 3 + 2], 0f);
                        else ch.vals[i] = new Vector4(v[i * 3], v[i * 3 + 1], v[i * 3 + 2], 0f);
                    }
                    if (times.Length > 0) clip.duration = Mathf.Max(clip.duration, times[times.Length - 1]);
                    clip.channels.Add(ch);
                }
                list.Add(clip);
            }
            return list;
        }

        static float[] ReadFloats(JObject j, byte[] d, int bin, int acc, int n)
        {
            var (off, count, _, _) = Acc(j, acc);
            int stride = Stride(j, acc, n * 4);
            var r = new float[count * n];
            for (int k = 0; k < count; k++)
                for (int c = 0; c < n; c++) r[k * n + c] = BitConverter.ToSingle(d, bin + off + k * stride + c * 4);
            return r;
        }

        static int Stride(JObject j, int i, int dflt)
            => (int?)j["bufferViews"][(int)j["accessors"][i]["bufferView"]]["byteStride"] ?? dflt;

        static (int off, int count, int n, int type) Acc(JObject j, int i)
        {
            var a = j["accessors"][i]; var bv = j["bufferViews"][(int)a["bufferView"]];
            string ty = (string)a["type"];
            return (((int?)bv["byteOffset"] ?? 0) + ((int?)a["byteOffset"] ?? 0), (int)a["count"], ty == "VEC3" ? 3 : ty == "VEC2" ? 2 : ty == "VEC4" ? 4 : 1, (int)a["componentType"]);
        }

        static Vector3[] ReadVec(JObject j, byte[] d, int bin, int acc)
        {
            var (off, count, n, _) = Acc(j, acc);
            int stride = Stride(j, acc, n * 4);
            var r = new Vector3[count];
            for (int k = 0; k < count; k++)
            {
                int o = bin + off + k * stride;
                r[k] = new Vector3(BitConverter.ToSingle(d, o), BitConverter.ToSingle(d, o + 4), n > 2 ? BitConverter.ToSingle(d, o + 8) : 0f);
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
    public class GltfChannel { public string target, path; public float[] times; public Vector4[] vals; }

    /// <summary>One glTF animation clip as plain data (shared by every instance); Apply resolves nodes by name per instance.</summary>
    public class GltfClip
    {
        public string name; public float duration;
        public readonly List<GltfChannel> channels = new List<GltfChannel>();

        public void Apply(IDictionary<string, Transform> nodes, float t)
        {
            foreach (var c in channels)
            {
                if (!nodes.TryGetValue(c.target, out var tr) || tr == null || c.times.Length == 0) continue;
                int n = c.times.Length; Vector4 v;
                if (t <= c.times[0]) v = c.vals[0];
                else if (t >= c.times[n - 1]) v = c.vals[n - 1];
                else
                {
                    int i = 0; while (i < n - 2 && t >= c.times[i + 1]) i++;
                    float u = (t - c.times[i]) / Mathf.Max(1e-6f, c.times[i + 1] - c.times[i]);
                    if (c.path == "rotation")
                    {
                        var q = Quaternion.Slerp(new Quaternion(c.vals[i].x, c.vals[i].y, c.vals[i].z, c.vals[i].w), new Quaternion(c.vals[i + 1].x, c.vals[i + 1].y, c.vals[i + 1].z, c.vals[i + 1].w), u);
                        tr.localRotation = q; continue;
                    }
                    v = Vector4.Lerp(c.vals[i], c.vals[i + 1], u);
                }
                if (c.path == "rotation") tr.localRotation = new Quaternion(v.x, v.y, v.z, v.w);
                else if (c.path == "translation") tr.localPosition = new Vector3(v.x, v.y, v.z);
                else tr.localScale = new Vector3(v.x, v.y, v.z);
            }
        }
    }
}
