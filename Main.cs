using System;
using System.Reflection;
using DV.Customization.Gadgets.Implementations;
using DV.Items;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using custom_item_mod;

namespace RailRider
{
    public class Settings : UnityModManager.ModSettings
    {
        // Key names are UnityEngine.KeyCode names. Defaults are guesses that avoid DV's stock bindings
        // (NOT verified against the player's Rewired map) - rebind in the UMM panel if they clash.
        public string raiseLowerKey = "Y";
        public string cockKey = "U";
        public float muzzleSpeed = 90f;     // m/s, virtual BB
        public float zeroRange = 15f;       // m, fixed zero: the sight line and the bullet path cross here
        public float bbMass = 0.00035f;     // kg, 4.5 mm steel BB
        public float dragK = 0.0129f;       // 1/m, quadratic air drag (rho*Cd*A/2m for a 4.5 mm steel BB)
        public float powerBoost = 60f;       // game multiplier on BB momentum (1 = realistic, far too weak to topple a can)
        public float maxDeltaV = 3.5f;      // m/s cap on the speed a hit can give a light prop
        public float maxTargetMass = 5f;    // kg, heavier bodies are only blocked, never pushed
        public float aimTime = 0.25f;
        public float cockPhaseTime = 0.30f; // seconds per lever stroke (down, then up)
        public float eyeRelief = 0.14f;     // m between camera and rear sight when raised
        public float shakeAccel = 22f;      // m/s^2 of rifle acceleration (game space) above which it rattles
        public float impactSpeed = 1.0f;    // m/s relative collision speed above which a loose rifle rattles
        public bool motionRattle = true;
        public bool scatter = true;         // random cans/bottles near stations (never saved, removed again when you leave)
        public bool debugSpawn = true;      // F8 = spawn a Rail-Rider at your feet, Shift+F8 = rack
        public string spawnKey = "F8";
        public bool verbose = false;
        public override void Save(UnityModManager.ModEntry e) => Save(this, e);
    }

    public static class Main
    {
        public static UnityModManager.ModEntry Mod;
        public static Settings S;
        static Harmony harmony;

        static bool Load(UnityModManager.ModEntry entry)
        {
            Mod = entry;
            S = UnityModManager.ModSettings.Load<Settings>(entry);
            entry.OnGUI = _ => DrawGui();
            entry.OnSaveGUI = e => S.Save(e);
            entry.OnUnload = _ => { harmony?.UnpatchAll(entry.Info.Id); return true; };
            try
            {
                harmony = new Harmony(entry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                // Inject our code-built items into Custom Item Mod's list just before it finalises the shop.
                harmony.Patch(AccessTools.Method(typeof(ItemModsFinder), "FinalizeItems"),
                    prefix: new HarmonyMethod(typeof(Main), nameof(BeforeFinalize)));
                RackFactory.RegisterGadgetMapping();
                Spawner.Create();
            }
            catch (Exception ex)
            {
                entry.Logger.LogException("RailRider failed to load", ex);
                harmony?.UnpatchAll(entry.Info.Id);
                return false;
            }
            Log("loaded");
            return true;
        }

        static void BeforeFinalize()
        {
            try
            {
                ItemModsFinder.CustomItems.Add(RifleFactory.Build());
                ItemModsFinder.CustomItems.Add(RackFactory.Build());
                foreach (var p in PropFactory.Build()) ItemModsFinder.CustomItems.Add(p);
            }
            catch (Exception ex) { Mod.Logger.LogException("RailRider item injection failed", ex); }
        }

        static bool confirmClear; static string clearResult;

        static void DrawGui()
        {
            GUILayout.Label("Keys are UnityEngine.KeyCode names (e.g. Y, U, Mouse2, F7).");
            GUILayout.BeginHorizontal(); GUILayout.Label("Raise/lower rifle", GUILayout.Width(140)); S.raiseLowerKey = GUILayout.TextField(S.raiseLowerKey, GUILayout.Width(120)); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("Cock (lever)", GUILayout.Width(140)); S.cockKey = GUILayout.TextField(S.cockKey, GUILayout.Width(120)); GUILayout.EndHorizontal();
            S.powerBoost = Slider("Impact power boost (x real momentum)", S.powerBoost, 1f, 150f);
            S.maxDeltaV = Slider("Max speed given to a prop (m/s)", S.maxDeltaV, 0.5f, 6f);
            S.muzzleSpeed = Slider("Muzzle speed (m/s)", S.muzzleSpeed, 30f, 200f);
            S.zeroRange = Slider("Zero range (m)", S.zeroRange, 5f, 60f);
            S.motionRattle = GUILayout.Toggle(S.motionRattle, "Rattle when shaken / turned quickly");
            S.shakeAccel = Slider("Shake threshold (m/s^2)", S.shakeAccel, 5f, 80f);
            S.scatter = GUILayout.Toggle(S.scatter, "Scatter cans/bottles near stations (not saved; removed when you leave)");
            GUILayout.Space(6);
            if (!confirmClear)
            {
                if (GUILayout.Button("Clear all loose Rail-Rider items...", GUILayout.Width(300))) confirmClear = true;
            }
            else
            {
                GUILayout.Label("Deletes every loose rifle, rack, can and bottle in the world. Racked rifles, attached racks, held items and inventory are NOT touched.");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Yes, clear them", GUILayout.Width(150)))
                {
                    int kept; int n = Scatter.ClearLoose(out kept);
                    clearResult = n + " removed, " + kept + " kept (held / racked / inventory)";
                    confirmClear = false;
                }
                if (GUILayout.Button("Cancel", GUILayout.Width(100))) confirmClear = false;
                GUILayout.EndHorizontal();
            }
            if (clearResult != null) GUILayout.Label("Last clear: " + clearResult);
            GUILayout.Space(6);
            S.debugSpawn = GUILayout.Toggle(S.debugSpawn, "Debug spawn key (F8 rifle, Shift+F8 rack)");
            S.verbose = GUILayout.Toggle(S.verbose, "Verbose log (hits, state changes)");
        }

        static float Slider(string label, float v, float a, float b)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {v:0.00}", GUILayout.Width(220));
            v = GUILayout.HorizontalSlider(v, a, b, GUILayout.Width(200));
            GUILayout.EndHorizontal();
            return v;
        }

        public static void Log(string m) => Mod.Logger.Log(m);
        public static void Verbose(string m) { if (S.verbose) Mod.Logger.Log(m); }
        public static void Warn(string m) => Mod.Logger.Warning(m);

        public static KeyCode Key(string name, KeyCode fallback)
            => Enum.TryParse(name, true, out KeyCode k) ? k : fallback;
    }

    // While the rifle is raised, a click must fire it, not snap it into a rack under the crosshair.
    [HarmonyPatch(typeof(SnappableItem), nameof(SnappableItem.IsUseCompatible))]
    static class SnapWhileAimedPatch
    {
        static void Postfix(SnappableItem __instance, ref bool __result)
        {
            if (__result && __instance.TryGetComponent<RifleController>(out var rc) && rc.IsRaisedOrRaising) __result = false;
        }
    }
}
