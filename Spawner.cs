using DV.InventorySystem;
using UnityEngine;

namespace RailRider
{
    /// <summary>Debug helper: F8 drops a Rail-Rider at your feet (in front of you), Shift+F8 a rack. Toggle in the mod panel.</summary>
    public class Spawner : MonoBehaviour
    {
        public static void Create()
        {
            var go = new GameObject("RailRider_Spawner");
            DontDestroyOnLoad(go);
            go.AddComponent<Spawner>();
        }

        void Update()
        {
            Scatter.Tick();
            if (!Main.S.debugSpawn) return;
            if (Input.GetKeyDown(KeyCode.F7)) { SpawnProps(); return; }
            if (!Input.GetKeyDown(Main.Key(Main.S.spawnKey, KeyCode.F8))) return;
            bool rack = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var cam = PlayerManager.PlayerCamera;
            if (cam == null) return;
            if (!rack) SpawnTargets(); // F8 also lays out a plinking line-up: 6 random cans + 2 bottles
            var prefab = Resources.Load("custom_item_mod/" + (rack ? RackFactory.ItemName : RifleFactory.ItemName) + "/prefab") as GameObject;
            if (prefab == null) { Main.Warn("spawn: item prefab not registered (is Custom Item Mod loaded, and are you in a game?)"); return; }
            Vector3 p = cam.transform.position + Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized * 0.8f;
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point + Vector3.up * 0.1f;
            var go = Instantiate(prefab, p, Quaternion.identity);
            var spec = go.GetComponent<InventoryItemSpec>();
            if (spec != null) spec.BelongsToPlayer = true;
            var ib = go.GetComponent<DV.CabControls.ItemBase>();
            if (ib != null) DV.Utils.SingletonBehaviour<StorageController>.Instance.AddItemToWorldStorage(ib);
            Main.Log("spawned " + go.name + " at " + p);
        }

        // F7: the six cans and the bottle in a row, 1.2 m ahead, for a quick plinking line-up.
        static void Parent(GameObject go) { if (WorldMover.OriginShiftParent != null) go.transform.SetParent(WorldMover.OriginShiftParent, true); }

        static void SpawnProps()
        {
            var cam = PlayerManager.PlayerCamera;
            if (cam == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 origin = cam.transform.position + fwd * 1.2f - right * 0.15f * (PropFactory.Names.Count - 1) / 2f;
            for (int i = 0; i < PropFactory.Names.Count; i++)
            {
                var prefab = Resources.Load("custom_item_mod/" + PropFactory.Names[i] + "/prefab") as GameObject;
                if (prefab == null) { Main.Warn("spawn: no prefab for " + PropFactory.Names[i]); continue; }
                Vector3 p = origin + right * 0.15f * i;
                if (Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point + Vector3.up * 0.02f;
                var go = Instantiate(prefab, p, Quaternion.identity);
                var spec = go.GetComponent<InventoryItemSpec>();
                if (spec != null) spec.BelongsToPlayer = false;
                Parent(go);
            }
            Main.Log("spawned prop row");
        }

        static void SpawnTargets()
        {
            var cam = PlayerManager.PlayerCamera;
            var canNames = PropFactory.Names.FindAll(n => n.StartsWith("Can"));
            var bottle = PropFactory.Names.Find(n => !n.StartsWith("Can"));
            if (canNames.Count == 0) return;
            var pick = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 6; i++) pick.Add(canNames[Random.Range(0, canNames.Count)]);
            if (bottle != null) { pick.Add(bottle); pick.Add(bottle); }
            // shuffle so the bottles land among the cans
            for (int i = pick.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var t = pick[i]; pick[i] = pick[j]; pick[j] = t; }
            Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 origin = cam.transform.position + fwd * 1.5f - right * 0.14f * (pick.Count - 1) / 2f;
            for (int i = 0; i < pick.Count; i++)
            {
                var prefab = Resources.Load("custom_item_mod/" + pick[i] + "/prefab") as GameObject;
                if (prefab == null) continue;
                Vector3 p = origin + right * 0.14f * i;
                if (Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point + Vector3.up * 0.02f;
                var go = Instantiate(prefab, p, Quaternion.identity);
                var spec = go.GetComponent<InventoryItemSpec>();
                if (spec != null) spec.BelongsToPlayer = false; // junk: never saved
                Parent(go);
            }
            Main.Log("spawned targets: " + string.Join(", ", pick));
        }
    }
}
