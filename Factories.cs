using System;
using DV.CabControls.Spec;
using DV.Customization.Gadgets;
using DV.Customization.Gadgets.Implementations;
using DV.Interaction;
using DV.Items.Snapping;
using HarmonyLib;
using UnityEngine;
using custom_item_components;
using custom_item_mod;

namespace RailRider
{
    /// <summary>
    /// Crude code-built stand-ins for the real models. Custom Item Mod normally wants an asset bundle;
    /// here the "provided prefab" is assembled at runtime and handed to its public CustomItem constructor.
    /// Final models replace Placeholders.Rifle()/Rack() (or supply a prefab with RifleRefs) - gameplay only
    /// reads RifleRefs / the rack snap child, never mesh names or dimensions.
    /// </summary>
    static class Placeholders
    {
        static Transform holder;

        public static Transform Holder()
        {
            if (holder != null) return holder;
            var h = new GameObject("RailRider_PrefabHolder");
            h.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(h);
            return holder = h.transform;
        }

        public static Transform Cube(Transform parent, string name, Vector3 pos, Vector3 size, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().material.color = c;
            return go.transform;
        }

        public static Transform Empty(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        /// <summary>Inventory art: Models\rail_rider_inventory.png (transparent render). Dropped/locked variant = same image greyed and faded.</summary>
        public static bool TryIcons(out Sprite standard, out Sprite dropped)
        {
            standard = dropped = null;
            try
            {
                string p = System.IO.Path.Combine(Main.Mod.Path, "Models", "rail_rider_inventory.png");
                if (!System.IO.File.Exists(p)) return false;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(System.IO.File.ReadAllBytes(p));
                var grey = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                var px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++) { float y = px[i].r * 0.3f + px[i].g * 0.59f + px[i].b * 0.11f; px[i] = new Color(y, y, y, px[i].a * 0.4f); }
                grey.SetPixels(px); grey.Apply();
                standard = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                dropped = Sprite.Create(grey, new Rect(0, 0, grey.width, grey.height), new Vector2(0.5f, 0.5f));
                return true;
            }
            catch (Exception e) { Main.Warn("inventory icon load failed: " + e.Message); return false; }
        }

        public static bool TryIconFile(string relPath, out Sprite standard, out Sprite dropped)
        {
            standard = dropped = null;
            try
            {
                string p = System.IO.Path.Combine(Main.Mod.Path, "Models", relPath);
                if (!System.IO.File.Exists(p)) return false;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(System.IO.File.ReadAllBytes(p));
                var grey = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                var px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++) { float y = px[i].r * 0.3f + px[i].g * 0.59f + px[i].b * 0.11f; px[i] = new Color(y, y, y, px[i].a * 0.4f); }
                grey.SetPixels(px); grey.Apply();
                standard = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                dropped = Sprite.Create(grey, new Rect(0, 0, grey.width, grey.height), new Vector2(0.5f, 0.5f));
                return true;
            }
            catch (Exception e) { Main.Warn("icon load failed (" + relPath + "): " + e.Message); return false; }
        }

        public static Sprite Icon(Color c)
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var px = new Color[64 * 64];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }
    }

    static class RifleFactory
    {
        public const string ItemName = "Rail-Rider";

        public static CustomItem Build()
        {
            var root = new GameObject("RailRiderRifle");
            root.transform.SetParent(Placeholders.Holder(), false);

            // Muzzle is +Z, up is +Y. Prefer the supplied GLB (Models\); fall back to crude cubes.
            var refs = root.AddComponent<RifleRefs>();
            string mdir = System.IO.Path.Combine(Main.Mod.Path, "Models");
            Vector3 rear, front, muzzle, chamber, dock, pivot, boxCenter, boxSize;
            if (GltfLoader.TryLoad(System.IO.Path.Combine(mdir, "rail_rider_rifle.glb"), root.transform, "Stock"))
            {
                // Codex model: reference points are named empties inside the file (rifle units, +Z muzzle).
                refs.lever = root.transform.Find("Lever");
                refs.ResolveByName();
                rear = refs.rearSight.localPosition; front = refs.frontSight.localPosition;
                muzzle = refs.muzzle.localPosition; chamber = refs.chamber.localPosition;
                dock = refs.rackDock != null ? refs.rackDock.localPosition : Vector3.zero;
                refs.leverOpenDegrees = -70f;
                // Leave the marked stock face in view at the standard non-VR carry/cock angles.
                refs.holdLocalPos = new Vector3(0.13f, -0.12f, 0.82f);
                refs.cockLocalPos = new Vector3(0.10f, -0.09f, 0.80f);
                boxCenter = new Vector3(0, -0.01f, 0.017f); boxSize = new Vector3(0.07f, 0.14f, 0.96f);
                // Empties already exist in the model; do not recreate them below.
                var a0 = refs.rackDock.gameObject.AddComponent<SnapPointAnchor>();
                a0.ForceSetType(SnapPointTypes.LongCylinder);
                goto Finish;
            }
            else if (GlbLoader.TryLoad(System.IO.Path.Combine(mdir, "rail_rider_rifle_placeholder.glb"),
                                  System.IO.Path.Combine(mdir, "rail_rider_brand_plate.png"), root.transform))
            {
                // Reference points read off the supplied model (source axes X fwd, Y lateral, Z up), remapped.
                rear = GlbLoader.Remap(new Vector3(0.05f, 0f, 0.041f));    // top of rear sight leaf
                front = GlbLoader.Remap(new Vector3(0.61f, 0f, 0.039f));   // top of front blade
                muzzle = GlbLoader.Remap(new Vector3(0.62f, 0f, 0.018f));
                chamber = GlbLoader.Remap(new Vector3(0.0f, 0f, 0.018f));
                dock = GlbLoader.Remap(new Vector3(0.15f, 0f, 0f));
                pivot = GlbLoader.Remap(new Vector3(-0.02f, 0f, -0.026f)); // top of lever, under receiver
                boxCenter = new Vector3(0, 0, 0.155f); boxSize = new Vector3(0.07f, 0.15f, 0.95f);
                var lv = root.transform.Find("lever");
                var lever = Placeholders.Empty(root.transform, "Lever", pivot);
                if (lv != null) lv.SetParent(lever, true);
                refs.lever = lever;
                refs.leverOpenDegrees = -70f; // negative: tip swings forward and down
            }
            else
            {
                var wood = new Color(0.45f, 0.28f, 0.14f); var steel = new Color(0.15f, 0.15f, 0.17f);
                Placeholders.Cube(root.transform, "Body", new Vector3(0, 0, 0), new Vector3(0.03f, 0.05f, 0.30f), steel);
                Placeholders.Cube(root.transform, "Barrel", new Vector3(0, 0.02f, 0.40f), new Vector3(0.015f, 0.015f, 0.50f), steel);
                Placeholders.Cube(root.transform, "Stock", new Vector3(0, -0.02f, -0.25f), new Vector3(0.03f, 0.07f, 0.25f), wood);
                Placeholders.Cube(root.transform, "RearSightL", new Vector3(-0.007f, 0.036f, -0.02f), new Vector3(0.006f, 0.022f, 0.012f), steel);
                Placeholders.Cube(root.transform, "RearSightR", new Vector3(0.007f, 0.036f, -0.02f), new Vector3(0.006f, 0.022f, 0.012f), steel);
                Placeholders.Cube(root.transform, "FrontPost", new Vector3(0, 0.034f, 0.62f), new Vector3(0.004f, 0.014f, 0.004f), steel);
                var lever = Placeholders.Empty(root.transform, "Lever", new Vector3(0, -0.03f, 0.02f));
                Placeholders.Cube(lever, "LeverBar", new Vector3(0, -0.035f, 0.0f), new Vector3(0.01f, 0.07f, 0.012f), steel);
                refs.lever = lever;
                rear = new Vector3(0, 0.040f, -0.02f); front = new Vector3(0, 0.0415f, 0.62f);
                muzzle = new Vector3(0, 0.02f, 0.65f); chamber = new Vector3(0, 0.02f, 0.10f); dock = new Vector3(0, 0, 0.12f);
                boxCenter = new Vector3(0, 0, 0.15f); boxSize = new Vector3(0.07f, 0.14f, 1.05f);
            }
            refs.rearSight = Placeholders.Empty(root.transform, "RearSight", rear);
            refs.frontSight = Placeholders.Empty(root.transform, "FrontSight", front);
            refs.muzzle = Placeholders.Empty(root.transform, "Muzzle", muzzle);
            refs.chamber = Placeholders.Empty(root.transform, "Chamber", chamber);
            refs.rackDock = Placeholders.Empty(root.transform, "RackDock", dock);
            var anchor = refs.rackDock.gameObject.AddComponent<SnapPointAnchor>();
            anchor.ForceSetType(SnapPointTypes.LongCylinder);

        Finish:
            root.AddComponent<RifleHoldAnchor>();
            root.AddComponent<RifleController>();
            var box = root.AddComponent<BoxCollider>();
            box.center = boxCenter; box.size = boxSize;

            var info = new CustomItemInfo
            {
                Name = ItemName,
                Description = "Lever-action BB rifle for plinking cans and bottles. Sights only; the lever must be cocked for every shot.",
                Amount = 5, Price = 1500,
                ShelfBounds = new Vector3(0.35f, 0.3f, 0.2f),
                ShelfScale = new Vector3(0.4f, 0.4f, 0.4f),
            };
            Sprite iconStd, iconDrop;
            if (!Placeholders.TryIcons(out iconStd, out iconDrop)) { iconStd = Placeholders.Icon(new Color(0.45f, 0.28f, 0.14f)); iconDrop = Placeholders.Icon(new Color(0.3f, 0.3f, 0.3f)); }
            var ci = new CustomItem(info, root, iconStd, iconDrop);

            var spec = ci.ItemPrefab.GetComponent<Item>();
            spec.itemUseApproach = ItemUseApproach.OneShot;          // click = one Use, not a held press
            spec.allowedSnapPointTypes = SnapPointTypes.LongCylinder; // lets the native snap system rack it
            spec.rigidbodyMass = 1.5f;
            return ci;
        }
    }

    // Source-side proxy: what the code-built prefab carries; Custom Item Mod swaps it for GadgetWithSnapPoint.
    public class RackProxy : custom_item_components.GadgetBase { }

    static class RackFactory
    {
        public const string ItemName = "Rail-Rider Rifle Rack";

        public static void RegisterGadgetMapping()
            => CustomGadgetBaseMap.RegisterGadgetImplementation(typeof(RackProxy), typeof(GadgetWithSnapPoint), Apply);

        public static CustomItem Build()
        {
            var root = new GameObject("RailRiderRack");
            root.transform.SetParent(Placeholders.Holder(), false);
            var wood = new Color(0.35f, 0.22f, 0.12f);

            // Gadget local frame: wall contact plane at z=0, body toward -Z, rifle lies along X.
            var gadget = Placeholders.Empty(root.transform, "RackGadget", Vector3.zero);
            string rackPath = System.IO.Path.Combine(Main.Mod.Path, "Models", "rail_rider_rack.glb");
            if (!GltfLoader.TryLoad(rackPath, gadget))
            {
                Placeholders.Cube(gadget, "Plate", new Vector3(0, 0, -0.005f), new Vector3(0.70f, 0.10f, 0.01f), wood);
                Placeholders.Cube(gadget, "HookL", new Vector3(-0.25f, 0.015f, -0.03f), new Vector3(0.03f, 0.05f, 0.04f), wood);
                Placeholders.Cube(gadget, "HookR", new Vector3(0.25f, 0.015f, -0.03f), new Vector3(0.03f, 0.05f, 0.04f), wood);
            }

            var proxy = gadget.gameObject.AddComponent<RackProxy>();
            proxy.boundsCenter = new Vector3(0, 0, -0.06f);
            proxy.boundsSize = new Vector3(0.70f, 0.12f, 0.12f);
            proxy.removalMethod = custom_item_components.GadgetBase.GadgetRemovalMethod.Any;
            proxy.requiredMountPoints = 0; // free placement on any loco surface DV accepts for gadgets; no per-loco coordinates

            var src = root.AddComponent<custom_item_components.GadgetItem>();
            src.gadget = proxy;

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0, -0.03f); box.size = new Vector3(0.70f, 0.12f, 0.08f);

            var info = new CustomItemInfo
            {
                Name = ItemName,
                Description = "Two-hook rack that holds one Rail-Rider in a locomotive cab. Remove the rifle before removing the rack.",
                Amount = 5, Price = 600,
                ShelfBounds = new Vector3(0.35f, 0.3f, 0.2f),
                ShelfScale = new Vector3(0.5f, 0.5f, 0.5f),
            };
            Sprite iStd, iDrop;
            if (!Placeholders.TryIconFile("icons/icon_rack.png", out iStd, out iDrop)) { iStd = Placeholders.Icon(new Color(0.35f, 0.22f, 0.12f)); iDrop = Placeholders.Icon(new Color(0.3f, 0.3f, 0.3f)); }
            return new CustomItem(info, root, iStd, iDrop);
        }

        // Called by Custom Item Mod after it has created the real GadgetWithSnapPoint on the gadget object.
        static void Apply(custom_item_components.GadgetBase source, ref DV.Customization.Gadgets.GadgetBase target)
        {
            var gws = target as GadgetWithSnapPoint;
            if (gws == null) { Main.Warn("rack target is not GadgetWithSnapPoint"); return; }

            // DV removes a gadget by raycasting for a collider under the GadgetBase. The item's own box lives on the
            // prefab root, not on the placed gadget, and the snap volume below is a trigger, so give the gadget a thin
            // solid plate: an empty rack can then be taken off the wall (GadgetWithSnapPoint refuses while it holds a rifle).
            if (gws.GetComponent<Collider>() == null)
            {
                var plate = gws.gameObject.AddComponent<BoxCollider>();
                plate.center = new Vector3(0, 0, -0.012f); plate.size = new Vector3(0.70f, 0.10f, 0.028f);
            }
            AccessTools.Field(typeof(DV.Customization.Gadgets.GadgetBase), "highlightMeshes")?.SetValue(gws, gws.GetComponentsInChildren<MeshFilter>());

            // Muzzle along gadget -X presents the carved stock face to the cab.
            var snapGo = new GameObject("RackSnap");
            snapGo.transform.SetParent(gws.transform, false);
            snapGo.transform.localPosition = new Vector3(-0.0675f, 0.045f, -0.048f);
            snapGo.transform.localRotation = Quaternion.Euler(0, -90, 0);
            snapGo.layer = gws.gameObject.layer;

            var col = snapGo.AddComponent<BoxCollider>();
            col.isTrigger = true; col.size = new Vector3(0.15f, 0.12f, 0.9f);

            var snap = snapGo.AddComponent<SnapPointGadget>(); // RequireComponent adds ItemUseTarget
            AccessTools.Field(typeof(ItemSnapPointBase), "snapPointType").SetValue(snap, SnapPointTypes.LongCylinder);
            snap.gadgetBase = gws;
            snapGo.GetComponent<ItemUseTarget>().targetColliders = new Collider[] { col };
            gws.snapPoint = snap;
        }
    }
}
