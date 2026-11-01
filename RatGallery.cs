using System.Collections;
using System.Collections.Generic;
using DV;
using DV.CabControls;
using DV.Items;
using Newtonsoft.Json.Linq;
using UnityEngine;
using custom_item_mod;

namespace RailRider
{
    /// <summary>
    /// Four-rat resetting gallery. Each standing rat falls when a BB passes through its silhouette and stays down;
    /// the round disc stands every fallen rat back up. Hits are resolved geometrically: the BB hit on the housing is
    /// continued along its flight direction to the rat / disc planes and tested against their silhouettes, so the
    /// housing keeps one simple solid collider and nothing is pushed around when a target is hit.
    /// </summary>
    public class RatGallery : MonoBehaviour
    {
        const int Rats = 4;
        const float RatPlaneZ = 0.056f, DiscPlaneZ = 0.091f, DiscY = 0.124f, DiscRadius = 0.023f;
        const float RatHalfW = 0.053f, RatY = 0.125f, RatHalfH = 0.024f;   // silhouette plus a few mm of forgiveness
        const float ResetContact = 0.2083f, ResetLength = 0.5417f;          // seconds, from the animation

        readonly Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();
        readonly Transform[] pivots = new Transform[Rats];
        readonly bool[] fallen = new bool[Rats];
        readonly float[] busyUntil = new float[Rats];
        readonly List<Active> active = new List<Active>();
        float resetUntil;
        ItemSaveData save;

        class Active { public GltfClip clip; public float t; }

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) nodes[t.name] = t;
            for (int i = 0; i < Rats; i++) nodes.TryGetValue("Rat_0" + (i + 1) + "_Pivot", out pivots[i]);
            if (GalleryFactory.Clips == null || pivots[0] == null) { enabled = false; return; }
            save = GetComponent<ItemSaveData>();
            if (save != null) // only the real item has this; shelf/preview copies stay inert
            {
                save.ItemSaveDataRequested += d => { int m = 0; for (int i = 0; i < Rats; i++) if (fallen[i]) m |= 1 << i; d["rrRats"] = m; return d; };
                save.ItemSaveDataLoaded += d =>
                {
                    int m = d["rrRats"]?.Value<int>() ?? 0;
                    for (int i = 0; i < Rats; i++) { fallen[i] = (m & (1 << i)) != 0; Pose(Clip("Rat_0" + (i + 1) + (fallen[i] ? "_Down" : "_Up")), 1e3f); }
                };
            }
        }

        static GltfClip Clip(string name)
        {
            foreach (var c in GalleryFactory.Clips) if (c.name == name) return c;
            return null;
        }

        void Pose(GltfClip clip, float t) { if (clip != null) clip.Apply(nodes, t); }

        void Play(string clipName)
        {
            var clip = Clip(clipName);
            if (clip == null) return;
            // a new clip replaces anything still moving the same nodes
            active.RemoveAll(a => a.clip.channels.Count > 0 && clip.channels.Count > 0 && a.clip.channels[0].target == clip.channels[0].target);
            active.Add(new Active { clip = clip, t = 0f });
        }

        void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var a = active[i];
                a.t += Time.deltaTime;
                a.clip.Apply(nodes, a.t);
                if (a.t >= a.clip.duration) active.RemoveAt(i);
            }
        }

        /// <summary>Called with a BB hit on this item. Returns true when a rat or the disc took the shot.</summary>
        public bool TryShot(Vector3 worldPoint, Vector3 worldDir)
        {
            if (!enabled) return false;
            Vector3 p = transform.InverseTransformPoint(worldPoint);
            Vector3 d = transform.InverseTransformDirection(worldDir).normalized;
            if (d.z > -0.05f) return false;                                   // not travelling into the open front
            if (Mathf.Abs(p.x) > 0.244f || p.y < 0.040f || p.y > 0.196f) return false; // hit the lip, roof or an end cap

            Vector3 atDisc = Project(p, d, DiscPlaneZ);
            if (atDisc.sqrMagnitude < 1e8f && new Vector2(atDisc.x, atDisc.y - DiscY).sqrMagnitude <= DiscRadius * DiscRadius) { HitDisc(); return true; }
            for (int i = 0; i < Rats; i++)
            {
                if (fallen[i] || Time.time < busyUntil[i] || pivots[i] == null) continue;
                Vector3 at = Project(p, d, RatPlaneZ);
                if (Mathf.Abs(at.x - pivots[i].localPosition.x) <= RatHalfW && Mathf.Abs(at.y - RatY) <= RatHalfH) { HitRat(i); return true; }
            }
            return false;
        }

        static Vector3 Project(Vector3 p, Vector3 d, float z)
        {
            float t = (z - p.z) / d.z;
            return t < -0.001f ? new Vector3(1e6f, 1e6f, 1e6f) : p + d * Mathf.Max(0f, t);
        }

        void HitRat(int i)
        {
            fallen[i] = true; busyUntil[i] = Time.time + 0.6f;
            Play("Rat_0" + (i + 1) + "_Down");
            RifleAudio.PlayAt(RifleAudio.Kind.RatFall, pivots[i].position, 1f);
            Main.Verbose("rat " + (i + 1) + " down");
        }

        void HitDisc()
        {
            Vector3 discPos = transform.TransformPoint(new Vector3(0f, DiscY, DiscPlaneZ));
            if (Time.time < resetUntil) return; // paddle still swinging: the disc just rings
            resetUntil = Time.time + ResetLength;
            Play("Reset_Hit");
            RifleAudio.PlayAt(RifleAudio.Kind.RatReset, discPos, 1f);
            StartCoroutine(ResetRoutine(discPos));
            Main.Verbose("reset disc hit, fallen=" + FallenCount());
        }

        int FallenCount() { int n = 0; for (int i = 0; i < Rats; i++) if (fallen[i]) n++; return n; }

        IEnumerator ResetRoutine(Vector3 discPos)
        {
            yield return new WaitForSeconds(ResetContact);                    // the paddle meets the support rod
            RifleAudio.PlayAt(RifleAudio.Kind.RatTick, discPos, 0.8f);
            int n = 0;
            for (int i = 0; i < Rats; i++)
            {
                if (!fallen[i]) continue;
                StartCoroutine(Rise(i, n++ * 0.018f));
            }
        }

        IEnumerator Rise(int i, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            fallen[i] = false; busyUntil[i] = Time.time + 0.6f;
            Play("Rat_0" + (i + 1) + "_Up");
            if (pivots[i] != null) RifleAudio.PlayAt(RifleAudio.Kind.RatRise, pivots[i].position, 0.9f);
        }
    }

    /// <summary>The shop item for the gallery: the Codex model, one solid housing collider, the RatGallery behaviour.</summary>
    static class GalleryFactory
    {
        public const string ItemName = "Rail-Rider Rat Gallery";
        public static List<GltfClip> Clips;

        public static CustomItem Build()
        {
            var root = new GameObject("RailRiderRatGallery");
            root.transform.SetParent(Placeholders.Holder(), false);
            string path = System.IO.Path.Combine(Main.Mod.Path, "Models", "rail_rider_rat_gallery.glb");
            if (GltfLoader.TryLoad(path, root.transform)) { Clips = GltfLoader.LastClips; root.AddComponent<RatGallery>(); }
            else
            {
                Main.Warn("rat gallery model missing; using a plain box (targets will not react)");
                Placeholders.Cube(root.transform, "Body", new Vector3(0, 0.1025f, 0), new Vector3(0.5f, 0.205f, 0.205f), new Color(0.2f, 0.2f, 0.22f));
            }
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.1025f, 0); box.size = new Vector3(0.506f, 0.205f, 0.208f);

            var info = new CustomItemInfo
            {
                Name = ItemName,
                Description = "Four steel rats and a reset disc in a steel housing. Shoot a rat and it drops; shoot the disc and they all stand back up.",
                Amount = 5, Price = 900,
                ShelfBounds = new Vector3(0.6f, 0.3f, 0.3f),
                ShelfScale = new Vector3(0.5f, 0.5f, 0.5f),
            };
            Sprite iStd, iDrop;
            if (!Placeholders.TryIconFile("icons/icon_rat_gallery.png", out iStd, out iDrop)) { iStd = Placeholders.Icon(new Color(0.55f, 0.57f, 0.6f)); iDrop = Placeholders.Icon(new Color(0.3f, 0.3f, 0.3f)); }
            var ci = new CustomItem(info, root, iStd, iDrop);
            var spec = ci.ItemPrefab.GetComponent<DV.CabControls.Spec.Item>();
            spec.itemUseApproach = DV.CabControls.Spec.ItemUseApproach.None;
            spec.rigidbodyMass = 6f; // heavier than maxTargetMass: a shot never shoves the housing
            return ci;
        }
    }
}
