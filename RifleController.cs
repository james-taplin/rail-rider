using DV;
using DV.CabControls;
using DV.Items;
using DV.UI;
using DV.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityModManagerNet;

namespace RailRider
{
    /// <summary>
    /// Authoritative mechanical state of one rifle. Three independent things:
    ///   handling  = held in hand / racked / loose (read from DV's ItemBase, never cached as truth),
    ///   aim       = wantRaised + aim01 (only meaningful while held),
    ///   mechanism = Cocked + cock stroke.
    /// Interrupted cocking rule: any interruption (drop, stash, rack) aborts the stroke, the lever snaps
    /// shut and the rifle is left UNCOCKED. Only a finished down+up stroke cocks it.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class RifleController : MonoBehaviour
    {
        enum Cock { Idle, Down, Up }

        ItemBase item;
        RifleRefs refs;
        ItemSaveData save;
        Quaternion leverRest;
        bool wiredUp;

        public bool Cocked { get; private set; }
        bool wantRaised;
        float aim01;
        Cock cock = Cock.Idle;
        float cockT;
        bool pendingCock;
        bool reraiseAfterCock; // cocked from the aimed pose: go straight back to aiming afterwards

        public bool IsRaisedOrRaising => wantRaised || aim01 > 0f;
        public Transform RifleTransform => transform;
        bool Held => item != null && item.IsGrabbed();

        void Awake()
        {
            refs = GetComponent<RifleRefs>();
            refs.ResolveByName();
            if (!refs.Valid) { Main.Warn("RifleRefs incomplete on " + name + "; controller disabled"); enabled = false; return; }
            leverRest = refs.lever.localRotation;
            save = GetComponent<ItemSaveData>();
            if (save != null) // only the real item has this; shelf/preview copies stay inert
            {
                save.ItemSaveDataRequested += d => { d["rrCocked"] = Cocked; return d; };
                save.ItemSaveDataLoaded += d => { Cocked = d["rrCocked"]?.Value<bool>() ?? false; };
            }
        }

        // DV adds its ItemBase implementation from the Item spec; wait a few frames for it.
        System.Collections.IEnumerator Start()
        {
            for (int i = 0; i < 10 && item == null && save != null; i++)
            {
                item = GetComponent<ItemBase>();
                if (item == null) yield return null;
            }
            if (item == null) { enabled = false; yield break; }
            item.Grabbed += OnGrabbed;
            item.Ungrabbed += OnUngrabbed;
            item.Used += OnUsed;
            if (item.SnappableItem != null) item.SnappableItem.ItemSnappingChanged += OnSnapChanged;
            wiredUp = true;
        }

        void OnDestroy()
        {
            if (!wiredUp || item == null) return;
            item.Grabbed -= OnGrabbed;
            item.Ungrabbed -= OnUngrabbed;
            item.Used -= OnUsed;
            if (item.SnappableItem != null) item.SnappableItem.ItemSnappingChanged -= OnSnapChanged;
        }

        void OnDisable() { if (wiredUp) AbortAll(); }

        // ---- handling events (each plays at most one rattle; RifleAudio also debounces) ----
        void OnGrabbed(ControlImplBase _)
        {
            AbortAll();
            RifleAudio.Play(this, RifleAudio.Kind.Equip);
            Main.Verbose("rifle equipped, cocked=" + Cocked);
        }

        void OnUngrabbed(ControlImplBase _)
        {
            AbortAll();
            RifleAudio.Play(this, RifleAudio.Kind.PutDown);
            Main.Verbose("rifle released, cocked=" + Cocked);
        }

        void OnSnapChanged(SnappableItem s, bool snapped, DV.CabControls.Spec.SnapPointTypes t)
        {
            AbortAll();
            if (snapped) RifleAudio.Play(this, RifleAudio.Kind.PutDown); // debounced against the Ungrabbed rattle
        }

        /// <summary>Cancels aim and any cocking stroke, restores the lever. Never touches Cocked.</summary>
        void AbortAll()
        {
            wantRaised = false; aim01 = 0f; pendingCock = false; reraiseAfterCock = false;
            if (cock != Cock.Idle) Main.Verbose("cocking aborted");
            cock = Cock.Idle; cockT = 0f;
            if (refs != null && refs.lever != null) refs.lever.localRotation = leverRest;
        }

        // ---- input: key polling only; the firing click is DV's own held-item Use, already gated by DV ----
        static bool InputAllowed()
        {
            if (UnityModManager.UI.Instance != null && UnityModManager.UI.Instance.Opened) return false;
            if (GUIUtility.keyboardControl != 0) return false;
            var app = SingletonBehaviour<AppUtil>.Instance;
            if (app != null && app.IsTimePaused) return false;
            var canvas = SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance;
            if (canvas != null && canvas.IsOn(CanvasController.ElementType.Blockers | CanvasController.ElementType.Hotbar)) return false;
            return true;
        }

        void Update()
        {
            if (!Held) return;
            if (Input.GetMouseButtonDown(0) && IsRaisedOrRaising) TryFire("click-poll");
            if (InputAllowed())
            {
                if (Input.GetKeyDown(Main.Key(Main.S.raiseLowerKey, KeyCode.Y)) && cock == Cock.Idle && !pendingCock)
                    wantRaised = !wantRaised;
                if (Input.GetKeyDown(Main.Key(Main.S.cockKey, KeyCode.U)) && !Cocked && cock == Cock.Idle && !pendingCock)
                {
                    if (aim01 > 0f || wantRaised) { reraiseAfterCock = true; wantRaised = false; pendingCock = true; } // simplest rule: cocking lowers the rifle first
                    else StartCock();
                }
            }

            aim01 = Mathf.MoveTowards(aim01, wantRaised ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, Main.S.aimTime));
            if (pendingCock && aim01 <= 0f) { pendingCock = false; StartCock(); }
            TickCock();
        }

        void StartCock()
        {
            cock = Cock.Down; cockT = 0f;
            RifleAudio.Play(this, RifleAudio.Kind.Cock);
        }

        void TickCock()
        {
            if (cock == Cock.Idle) return;
            float phase = Mathf.Max(0.05f, Main.S.cockPhaseTime);
            cockT += Time.deltaTime / phase;
            float open = cock == Cock.Down ? Mathf.Clamp01(cockT) : 1f - Mathf.Clamp01(cockT);
            refs.lever.localRotation = leverRest * Quaternion.AngleAxis(open * refs.leverOpenDegrees, refs.leverAxis);
            if (cockT < 1f) return;
            if (cock == Cock.Down) { cock = Cock.Up; cockT = 0f; return; }
            cock = Cock.Idle; cockT = 0f; Cocked = true; // only a completed stroke cocks
            if (reraiseAfterCock) { reraiseAfterCock = false; wantRaised = true; }
            refs.lever.localRotation = leverRest;
            Main.Verbose("rifle cocked");
        }

        // ---- pose: camera-relative, written after DV has positioned the held item ----
        float recoil;     // 1 right after a shot, decays to 0
        float cockShow;   // 0 = carry pose, 1 = cocking pose (lever visible)
        static Transform Cam => PlayerManager.PlayerCamera != null ? PlayerManager.PlayerCamera.transform : Camera.main?.transform;

        void LateUpdate()
        {
            if (Held)
            {
                var cam = Cam;
                if (cam != null)
                {
                    cockShow = Mathf.MoveTowards(cockShow, (cock != Cock.Idle || pendingCock) ? 1f : 0f, Time.deltaTime / 0.15f);
                    float c = Mathf.SmoothStep(0f, 1f, cockShow);
                    Vector3 hp = Vector3.Lerp(refs.holdLocalPos, refs.cockLocalPos, c);
                    Quaternion hr = Quaternion.Slerp(Quaternion.Euler(refs.holdLocalEuler), Quaternion.Euler(refs.cockLocalEuler), c);
                    Vector3 holdPos = cam.TransformPoint(hp);
                    Quaternion holdRot = cam.rotation * hr;

                    Vector3 pos = holdPos; Quaternion rot = holdRot;
                    if (aim01 > 0f)
                    {
                        Vector3 rearL = transform.InverseTransformPoint(refs.rearSight.position);
                        Vector3 frontL = transform.InverseTransformPoint(refs.frontSight.position);
                        Vector3 sightDirL = (frontL - rearL).normalized;
                        Quaternion aimRot = cam.rotation * Quaternion.Inverse(Quaternion.LookRotation(sightDirL, Vector3.up));
                        Vector3 aimPos = cam.position + cam.forward * Main.S.eyeRelief - aimRot * rearL;
                        float t = Mathf.SmoothStep(0f, 1f, aim01);
                        pos = Vector3.Lerp(holdPos, aimPos, t); rot = Quaternion.Slerp(holdRot, aimRot, t);
                    }
                    if (recoil > 0f)
                    {
                        recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime / 0.22f);
                        float k = recoil * recoil;
                        pos -= rot * Vector3.forward * (0.07f * k) - rot * Vector3.up * (0.0f);
                        rot = rot * Quaternion.Euler(-6f * k, 0f, 0f); // muzzle flips up
                    }
                    transform.SetPositionAndRotation(pos, rot);
                }
            }
            else cockShow = 0f;
            TrackMotion();
        }

        // ---- motion rattle: measure the rifle's own acceleration in game space ----
        Vector3 lastPos, lastVel; bool haveLast, haveVel; float calm;
        Vector3 safeVel;  // last sane rifle velocity (m/s, game space): the platform the BB inherits
        public Vector3 SafeWorldVelocity => safeVel;

        void TrackMotion()
        {
            float dt = Time.deltaTime;
            Vector3 p = transform.position;
            if (!haveLast || dt <= 1e-4f || dt > 0.1f) { lastPos = p; haveLast = true; haveVel = false; calm = 0.3f; return; }
            Vector3 v = (p - lastPos) / dt; lastPos = p;
            if (!haveVel) { lastVel = v; haveVel = true; return; }
            float a = (v - lastVel).magnitude / dt; lastVel = v;
            if (v.magnitude < 45f && a < 600f) safeVel = v;
            calm -= dt;
            // Ignore our own animations, and absurd values (world-origin shifts / teleports).
            bool animating = cock != Cock.Idle || pendingCock || (aim01 > 0f && aim01 < 1f) || (cockShow > 0f && cockShow < 1f);
            if (animating || a > 600f) { calm = 0.35f; return; }
            if (!Main.S.motionRattle || calm > 0f || RifleAudio.RecentlyHandled()) return;
            if (a > Main.S.shakeAccel)
            {
                float excess = a - Main.S.shakeAccel;
                RifleAudio.Play(this, RifleAudio.Kind.Shake, Mathf.Clamp(0.25f + excess / 40f, 0.25f, 1f), Random.Range(0.96f, 1.04f));
                Main.Verbose($"shake rattle a={a:0.0} m/s^2");
            }
        }

        // Loose rifle bumps (dropped, hit by something): rattle scaled by impact speed.
        void OnCollisionEnter(Collision c)
        {
            if (item == null || Held || item.IsSnapped) return;
            float v = c.relativeVelocity.magnitude;
            if (v < Main.S.impactSpeed) return;
            RifleAudio.Play(this, RifleAudio.Kind.Impact, Mathf.Clamp(v / 4f, 0.2f, 1f), Random.Range(0.9f, 1.1f));
        }

        // ---- firing ----
        bool CanFire => Held && wantRaised && aim01 >= 1f && cock == Cock.Idle && !pendingCock && Cocked && InputAllowed();

        // Two routes to the same TryFire: DV's held-item Use event, and a direct left-click poll as a fallback.
        // Cocked is consumed first inside TryFire, so both firing in one frame still gives one shot.
        void OnUsed() { Main.Log("Used event received"); TryFire("Used"); }

        float lastRefusalLog;
        void TryFire(string via)
        {
            if (!CanFire)
            {
                if (Held && wantRaised && aim01 >= 1f && cock == Cock.Idle && !pendingCock && !Cocked) RifleAudio.Play(this, RifleAudio.Kind.Dry);
                if (IsRaisedOrRaising && Time.unscaledTime - lastRefusalLog > 0.5f)
                {
                    lastRefusalLog = Time.unscaledTime;
                    Main.Log($"fire refused via {via}: held={Held} raised={wantRaised} aim01={aim01:0.00} cock={cock} pending={pendingCock} cocked={Cocked} input={InputAllowed()}");
                }
                return;
            }
            Cocked = false;
            recoil = 1f;
            RifleAudio.Play(this, RifleAudio.Kind.Fire);
            Ballistics.Fire(this, refs);
            Main.Log("shot fired via " + via);
        }
    }
}
