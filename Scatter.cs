using System.Collections.Generic;
using DV.CabControls;
using DV.InventorySystem;
using DV.Utils;
using UnityEngine;

namespace RailRider
{
    /// <summary>
    /// World scatter for the junk props. Items are created with BelongsToPlayer = false, which DV never saves, so
    /// they cannot bloat a save. Untouched ones are removed again when the player moves away; anything the player
    /// grabbed, racked, moved or shot stays as an ordinary loose item until the player goes away and the game unloads it.
    /// </summary>
    public class ScatterTag : MonoBehaviour
    {
        public bool touched;
        ItemBase item; Vector3 rest; float age; bool haveRest;

        void Start() { item = GetComponent<ItemBase>(); }
        public void Touch() { touched = true; }

        void Update()
        {
            if (touched) return;
            age += Time.deltaTime;
            if (item != null && (item.IsGrabbed() || item.IsSnapped)) { touched = true; return; }
            if (age > 6f && !haveRest) { rest = transform.position; haveRest = true; }
            else if (haveRest && (transform.position - rest).sqrMagnitude > 1f) touched = true; // moved after settling
        }
    }

    public static class Scatter
    {
        const float PopulateRange = 90f, DespawnRange = 260f, Cooldown = 120f;
        const int MaxAlive = 40;
        static readonly List<ScatterTag> alive = new List<ScatterTag>();
        static readonly Dictionary<StationController, float> populated = new Dictionary<StationController, float>(); // station -> time it was populated
        static readonly Dictionary<StationController, float> clearedAt = new Dictionary<StationController, float>();
        static float nextTick;

        public static void Tick()
        {
            if (!Main.S.scatter || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 3f;
            var cam = PlayerManager.PlayerCamera;
            if (cam == null || StationController.allStations == null || Time.timeScale <= 0f || Time.timeSinceLevelLoad < 30f) return;
            Vector3 pp = cam.transform.position;

            // despawn untouched scatter that is far away (also lets the station be populated again later)
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                var t = alive[i];
                if (t == null) { alive.RemoveAt(i); continue; }
                if (!t.touched && (t.transform.position - pp).sqrMagnitude > DespawnRange * DespawnRange) { Object.Destroy(t.gameObject); alive.RemoveAt(i); }
            }
            foreach (var st in new List<StationController>(populated.Keys))
                if (st == null || (st.transform.position - pp).sqrMagnitude > DespawnRange * DespawnRange) { populated.Remove(st); clearedAt[st] = Time.unscaledTime; }

            if (alive.Count >= MaxAlive) return;
            foreach (var st in StationController.allStations)
            {
                if (st == null || populated.ContainsKey(st)) continue;
                if ((st.transform.position - pp).sqrMagnitude > PopulateRange * PopulateRange) continue;
                if (clearedAt.TryGetValue(st, out var t0) && Time.unscaledTime - t0 < Cooldown) continue;
                populated[st] = Time.unscaledTime;
                Populate(st);
                break; // one station per tick
            }
        }

        static void Populate(StationController st)
        {
            Vector3 c = st.transform.position;
            int ground = Random.Range(3, 7), inBins = 0;

            // bins: look for anything bin-like near the station (names are unknown, so log what we find)
            var bins = new List<Collider>(); var seen = new HashSet<Transform>();
            foreach (var col in Physics.OverlapSphere(c, 50f, ~0, QueryTriggerInteraction.Collide))
            {
                if (col.attachedRigidbody != null) continue;
                var tr = col.transform; string n = tr.name.ToLowerInvariant();
                if (tr.parent != null) n += "/" + tr.parent.name.ToLowerInvariant();
                if (n.Contains("jobtrashbin") || n.Contains("job_trash_bin")) continue; // office paperwork bin, not a litter bin
                if ((n.Contains("skip") || n.Contains("dumpster") || n.Contains("trashcan") || n.Contains("trash_can") || n.Contains("rubbish") || n.Contains("garbage")) && seen.Add(tr)) bins.Add(col);
            }
            Main.Verbose($"scatter at {st.stationInfo?.Name}: {bins.Count} bin-like colliders" + (bins.Count > 0 ? " e.g. " + bins[0].name : ""));
            for (int i = 0; i < Mathf.Min(2, bins.Count); i++)
            {
                var b = bins[Random.Range(0, bins.Count)].bounds;
                Vector3 p = b.center + Vector3.up * (b.extents.y + 0.1f) + new Vector3(Random.Range(-1f, 1f) * b.extents.x * 0.4f, 0, Random.Range(-1f, 1f) * b.extents.z * 0.4f);
                if (SpawnOne(p)) inBins++;
            }

            int placed = 0;
            for (int a = 0; a < 30 && placed < ground; a++)
            {
                float ang = Random.value * 6.2832f, r = Random.Range(4f, 28f);
                Vector3 o = c + new Vector3(Mathf.Cos(ang) * r, 8f, Mathf.Sin(ang) * r);
                if (!Physics.Raycast(o, Vector3.down, out var hit, 20f, LayerMask.GetMask("Default", "Terrain"), QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.85f || Mathf.Abs(hit.point.y - c.y) > 4f) continue;
                if (SpawnOne(hit.point + Vector3.up * 0.05f)) placed++;
            }
            Main.Verbose($"scatter at {st.stationInfo?.Name}: {placed} on the ground, {inBins} at bins");
        }

        static bool SpawnOne(Vector3 pos)
        {
            if (alive.Count >= MaxAlive) return false;
            string name = Random.value < 0.3f ? PropFactory.BottleName : PropFactory.RandomCanName();
            var prefab = Resources.Load("custom_item_mod/" + name + "/prefab") as GameObject;
            if (prefab == null) return false;
            var go = Object.Instantiate(prefab, pos, Quaternion.Euler(0, Random.value * 360f, 0));
            var spec = go.GetComponent<InventoryItemSpec>();
            if (spec != null) spec.BelongsToPlayer = false;       // never saved
            if (WorldMover.OriginShiftParent != null) go.transform.SetParent(WorldMover.OriginShiftParent, true);
            var tag = go.AddComponent<ScatterTag>();
            alive.Add(tag);
            return true;
        }

        /// <summary>Removes every loose Rail-Rider item. Held, racked, inventory, container and lost-and-found items are left alone.</summary>
        public static int ClearLoose(out int kept)
        {
            kept = 0; int removed = 0;
            var names = new HashSet<string>(PropFactory.Names) { RifleFactory.ItemName, RackFactory.ItemName };
            var inv = SingletonBehaviour<Inventory>.Instance;
            var storage = SingletonBehaviour<StorageController>.Instance;
            foreach (var item in Object.FindObjectsOfType<ItemBase>())
            {
                var spec = item.InventorySpecs;
                if (spec == null || spec.ItemPrefabName == null || !spec.ItemPrefabName.StartsWith("custom_item_mod/")) continue;
                string n = spec.ItemPrefabName.Substring("custom_item_mod/".Length);
                n = n.Substring(0, n.LastIndexOf('/'));
                if (!names.Contains(n)) continue;
                bool safe = item.IsGrabbed() || item.IsSnapped || item.InContainer != null
                    || (inv != null && inv.Contains(item.gameObject, includeDropped: false))
                    || (storage != null && storage.StorageLostAndFound.ContainsItem(item));
                if (safe) { kept++; continue; }
                Object.Destroy(item.gameObject);
                removed++;
            }
            alive.RemoveAll(t => t == null);
            Main.Log($"clear loose: removed {removed}, kept {kept} (held/racked/inventory/containers)");
            return removed;
        }
    }
}
