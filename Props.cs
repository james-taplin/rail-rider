using System.Collections.Generic;
using DV.CabControls.Spec;
using DV.InventorySystem;
using UnityEngine;
using custom_item_mod;

namespace RailRider
{
    sealed class CanTarget : MonoBehaviour { }

    /// <summary>Plinking targets: six generic cans (lighter than DV's 0.3 kg mug) and one green glass bottle that shatters.</summary>
    static class PropFactory
    {
        public static readonly List<string> Names = new List<string>();
        public const string BottleName = "Green Glass Bottle";
        public static string RandomCanName() { return cans[Random.Range(0, cans.Length)].name; }

        struct CanType { public string name; public Color body, band; public float mass; }
        static readonly CanType[] cans =
        {
            new CanType { name = "Can - Red",    body = new Color(0.72f, 0.09f, 0.09f), band = new Color(0.95f, 0.95f, 0.95f), mass = 0.12f },
            new CanType { name = "Can - Green",  body = new Color(0.12f, 0.55f, 0.20f), band = new Color(0.90f, 0.95f, 0.30f), mass = 0.12f },
            new CanType { name = "Can - Orange", body = new Color(0.90f, 0.48f, 0.08f), band = new Color(0.98f, 0.98f, 0.98f), mass = 0.14f },
            new CanType { name = "Can - Blue",   body = new Color(0.10f, 0.28f, 0.72f), band = new Color(0.80f, 0.90f, 1.00f), mass = 0.14f },
            new CanType { name = "Can - Silver", body = new Color(0.74f, 0.76f, 0.80f), band = new Color(0.45f, 0.47f, 0.50f), mass = 0.10f },
            new CanType { name = "Can - Black",  body = new Color(0.07f, 0.07f, 0.08f), band = new Color(0.85f, 0.70f, 0.20f), mass = 0.16f },
        };

        public static List<CustomItem> Build()
        {
            Names.Clear();
            var list = new List<CustomItem>();
            foreach (var c in cans) list.Add(BuildCan(c));
            list.Add(BuildBottle());
            return list;
        }

        static void Metal(Transform t, float metallic, float gloss)
        {
            var m = t.GetComponent<Renderer>().material;
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", gloss);
        }

        static CustomItem Finish(GameObject root, string name, string desc, int price, float mass, Color icon, float shelfScale, string iconFile)
        {
            var info = new CustomItemInfo
            {
                Name = name, Description = desc, Amount = 20, Price = price,
                ShelfBounds = new Vector3(0.15f, 0.2f, 0.1f),
                ShelfScale = new Vector3(shelfScale, shelfScale, shelfScale),
            };
            Sprite iStd, iDrop;
            if (!Placeholders.TryIconFile("icons/" + iconFile, out iStd, out iDrop)) { iStd = Placeholders.Icon(icon); iDrop = Placeholders.Icon(new Color(0.3f, 0.3f, 0.3f)); }
            var ci = new CustomItem(info, root, iStd, iDrop);
            var invSpec = ci.ItemPrefab.GetComponent<InventoryItemSpec>();
            if (invSpec != null) invSpec.ImmuneToDumpster = false; // cans and bottles can be binned in a dumpster like any other junk
            var spec = ci.ItemPrefab.GetComponent<Item>();
            spec.rigidbodyMass = mass;
            spec.itemUseApproach = ItemUseApproach.None;
            Names.Add(name);
            return ci;
        }

        static CustomItem BuildCan(CanType c)
        {
            var root = new GameObject("RailRider_" + c.name.Replace(" ", ""));
            root.transform.SetParent(Placeholders.Holder(), false);
            // 330 ml can: 66 mm diameter, 116 mm tall, origin at the base.
            var body = Cyl(root.transform, "Body", new Vector3(0, 0.058f, 0), 0.066f, 0.116f, c.body); Metal(body, 0.8f, 0.6f);
            var band = Cyl(root.transform, "Band", new Vector3(0, 0.060f, 0), 0.0668f, 0.040f, c.band); Metal(band, 0.5f, 0.5f);
            Metal(Cyl(root.transform, "Top", new Vector3(0, 0.1175f, 0), 0.058f, 0.003f, new Color(0.8f, 0.8f, 0.82f)), 0.9f, 0.7f);
            Metal(Cyl(root.transform, "Base", new Vector3(0, 0.0015f, 0), 0.058f, 0.003f, new Color(0.7f, 0.7f, 0.72f)), 0.9f, 0.6f);
            root.AddComponent<CanTarget>();
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 1; col.radius = 0.033f; col.height = 0.12f; col.center = new Vector3(0, 0.06f, 0);
            return Finish(root, c.name, "Empty generic can. Light, loud and entirely unbranded.", 5, c.mass, c.body, 1.2f, "icon_can_" + c.name.Substring(6).ToLowerInvariant() + ".png");
        }

        static CustomItem BuildBottle()
        {
            var root = new GameObject("RailRider_GreenBottle");
            root.transform.SetParent(Placeholders.Holder(), false);
            var glass = new Color(0.10f, 0.55f, 0.22f, 0.42f);
            Glass(Cyl(root.transform, "Body", new Vector3(0, 0.080f, 0), 0.062f, 0.160f, glass), glass);
            var sh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(sh.GetComponent<Collider>());
            sh.name = "Shoulder"; sh.transform.SetParent(root.transform, false);
            sh.transform.localPosition = new Vector3(0, 0.160f, 0); sh.transform.localScale = new Vector3(0.062f, 0.050f, 0.062f);
            Glass(sh.transform, glass);
            Glass(Cyl(root.transform, "Neck", new Vector3(0, 0.205f, 0), 0.026f, 0.090f, glass), glass);
            Glass(Cyl(root.transform, "Lip", new Vector3(0, 0.252f, 0), 0.031f, 0.010f, glass), glass);
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 1; col.radius = 0.031f; col.height = 0.26f; col.center = new Vector3(0, 0.13f, 0);
            root.AddComponent<Breakable>();
            return Finish(root, "Green Glass Bottle", "Empty green glass bottle, no label. Shatters.", 8, 0.35f, new Color(0.1f, 0.55f, 0.22f), 1.0f, "icon_bottle_green.png");
        }

        static Transform Cyl(Transform parent, string name, Vector3 pos, float diameter, float height, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // unit cylinder: height 2, diameter 1
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = new Vector3(diameter, height / 2f, diameter);
            go.GetComponent<Renderer>().material.color = c;
            return go.transform;
        }

        // Standard-shader "fade/transparent" setup, done by hand because the editor's mode switch is not available.
        static void Glass(Transform t, Color c)
        {
            var m = t.GetComponent<Renderer>().material;
            m.color = c;
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            m.SetFloat("_Metallic", 0f); m.SetFloat("_Glossiness", 0.9f);
        }
    }

    /// <summary>Glass bottle: shatters on a BB hit or a hard knock (not while held, not just after spawning).</summary>
    public class Breakable : MonoBehaviour
    {
        public float breakSpeed = 4.5f;
        float born; bool broken;

        void Start() { born = Time.time; }

        void OnCollisionEnter(Collision c)
        {
            if (broken || Time.time - born < 3f) return;
            if (c.relativeVelocity.magnitude < breakSpeed) return;
            var item = GetComponent<DV.CabControls.ItemBase>();
            if (item != null && item.IsGrabbed()) return;
            Break(c.contactCount > 0 ? c.GetContact(0).point : transform.position, c.relativeVelocity.normalized);
        }

        public void Break(Vector3 point, Vector3 dir)
        {
            if (broken) return;
            broken = true;
            Fx.Glass(point, dir);
            Destroy(gameObject);
            Main.Verbose("bottle shattered");
        }
    }
}
