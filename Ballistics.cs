using System.Collections.Generic;
using DV.CabControls;
using UnityEngine;

namespace RailRider
{
    /// <summary>Live BB / pellet: advances in real game time (so it has travel time), gravity + drag, SphereCast sweep per sub-step,
    /// inherits the shooter's motion. Projectile, drag, zero and impact tuning come from the rifle's RifleProfile.</summary>
    public static class Ballistics
    {
        const float Step = 0.01f, MaxTime = 4f;
        static int mask = -2;

        static int Mask()
        {
            if (mask != -2) return mask;
            int m = ~0;
            foreach (var n in new[] { "Player", "Controller", "Grabbed_Item", "Inventory", "Ignore Raycast",
                                      "Teleport_Destination", "Laser_Pointer_Target", "Camera_Dampening", "Water" })
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) m &= ~(1 << l);
            }
            return mask = m;
        }

        static readonly RaycastHit[] buf = new RaycastHit[24];

        // Fixed zero, like a real rifle's sights: the launch elevation above the sight line is solved ONCE (level rifle,
        // 2D flight with gravity + drag) for zeroRange and cached. Every shot then just applies that fixed angle to
        // wherever the sights are pointing, so the point of impact never "adjusts" with distance to the target.
        class Zero { public float alphaDeg = float.NaN, range, speed, drag, h, dx; }
        static readonly Dictionary<string, Zero> zeros = new Dictionary<string, Zero>();

        static float ZeroAlphaDeg(float h, float dx, RifleProfile pr)
        {
            float R = pr.ZeroRange, v0 = pr.MuzzleSpeed, k = pr.DragK;
            if (!zeros.TryGetValue(pr.Id, out var z)) zeros[pr.Id] = z = new Zero();
            if (!float.IsNaN(z.alphaDeg) && z.range == R && z.speed == v0 && z.drag == k && Mathf.Approximately(z.h, h) && Mathf.Approximately(z.dx, dx)) return z.alphaDeg;
            // frame: x along the sight line from the eye, y up; sight line is y = 0. Muzzle at (dx, -h).
            float alpha = Mathf.Atan2(h, R - dx);
            for (int it = 0; it < 12; it++)
            {
                float x = dx, y = -h, vx = v0 * Mathf.Cos(alpha), vy = v0 * Mathf.Sin(alpha), px = x, py = y;
                for (int s = 0; s < 2000 && x < R; s++)
                {
                    float sp = Mathf.Sqrt(vx * vx + vy * vy);
                    float ax = -k * sp * vx, ay = -9.81f - k * sp * vy;
                    px = x; py = y;
                    x += vx * Step + 0.5f * ax * Step * Step; y += vy * Step + 0.5f * ay * Step * Step;
                    vx += ax * Step; vy += ay * Step;
                }
                if (x > px) y = py + (y - py) * (R - px) / (x - px); // height exactly at R (a fast pellet covers ~3 m per step)
                alpha += -y / Mathf.Max(1f, R - dx); // landed low (y<0) => aim up
            }
            z.range = R; z.speed = v0; z.drag = k; z.h = h; z.dx = dx;
            z.alphaDeg = alpha * Mathf.Rad2Deg;
            Main.Log($"zero solved ({pr.Id}): {z.alphaDeg:0.000} deg above the sight line for {R:0} m at {v0:0} m/s (muzzle {h * 100f:0.0} cm below sights)");
            return z.alphaDeg;
        }

        public static void Fire(RifleController rc, RifleRefs r) => Fire(rc, r, RifleProfile.Get(r.profileId));

        public static void Fire(RifleController rc, RifleRefs r, RifleProfile pr)
        {
            Vector3 g = Physics.gravity, M = r.muzzle.position, C = r.chamber.position;
            ContactCallbacks.NotifyDischarged(M);
            float v = pr.MuzzleSpeed;

            Vector3 eye = r.rearSight.position;
            Vector3 sight = (r.frontSight.position - eye).normalized;
            Vector3 up = Vector3.ProjectOnPlane(rc.RifleTransform.up, sight).normalized;
            float h = Vector3.Dot(eye - M, up);              // muzzle below the sight line
            float dx = Vector3.Dot(M - eye, sight);          // muzzle ahead of the rear sight
            Vector3 left = Vector3.Cross(sight, up);         // positive rotation about this pitches up
            Vector3 dir = (Quaternion.AngleAxis(ZeroAlphaDeg(h, dx, pr), left) * sight).normalized;
            Main.Verbose($"launch {Vector3.Angle(dir, sight):0.00} deg above sight line, elevation {Mathf.Asin(dir.y) * Mathf.Rad2Deg:0.00} deg");

            // Bore check: if the barrel is buried in geometry the shot dies at the surface.
            var shot = new Shot { pos = M, start = M, rc = rc, drag = pr.DragK, mass = pr.ProjectileMass, boost = pr.PowerBoost,
                                  maxDv = pr.MaxDeltaV, maxMass = pr.MaxTargetMass, radius = pr.Radius };
            if (Cast(rc, C, M, shot.radius, out var boreHit)) { Impact(boreHit, M - C, 0f, 0f, shot); return; }

            // Launch a live projectile. It leaves at muzzle speed PLUS the shooter's own velocity (so a BB fired inside a
            // moving cab travels with the cab), then flies in real game time: 0.17 s to 15 m at ~90 m/s.
            Vector3 inherit = rc.SafeWorldVelocity;
            shot.vel = dir * v + inherit; shot.inherit = inherit;
            live.Add(shot);
            EnsureHost();
            Main.Verbose($"{pr.Id} shot away: muzzle {v:0} m/s, inherited {inherit.magnitude:0.0} m/s");
        }

        class Shot { public Vector3 pos, vel, inherit, start; public float age, drag, mass, boost, maxDv, maxMass, radius; public RifleController rc; }
        static readonly List<Shot> live = new List<Shot>();
        static GameObject host;

        class Host : MonoBehaviour { void Update() { Tick(Time.deltaTime); } }

        static void EnsureHost()
        {
            if (host != null) return;
            host = new GameObject("RailRider_BBs");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Host>();
        }

        static void Tick(float dt)
        {
            if (dt <= 0f || live.Count == 0) return;
            Vector3 g = Physics.gravity;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var s = live[i];
                float left = dt;
                bool done = false;
                while (left > 1e-5f && !done)
                {
                    float h = Mathf.Min(Step, left); left -= h;
                    Vector3 p0 = s.pos, v0 = s.vel;
                    Vector3 acc = g - s.drag * v0.magnitude * v0;
                    s.pos = p0 + v0 * h + 0.5f * acc * h * h;
                    s.vel = v0 + acc * h;
                    s.age += h;
                    if (Cast(s.rc, p0, s.pos, s.radius, out var hit))
                    {
                        if (!ContactCallbacks.OfferImpact(hit.collider, hit.point, (s.pos - p0).normalized))
                            Impact(hit, v0 - s.inherit, (hit.point - s.start).magnitude, s.age, s);
                        done = true;
                    }
                    else if (s.age > MaxTime || (s.vel - s.inherit).magnitude < 4f) { Main.Verbose($"shot spent after {s.age:0.00}s without a hit"); done = true; }
                }
                if (done) live.RemoveAt(i);
            }
        }

        static bool Cast(RifleController rc, Vector3 a, Vector3 b, float radius, out RaycastHit best)
        {
            best = default;
            Vector3 d = b - a; float len = d.magnitude;
            if (len < 1e-5f) return false;
            int n = Physics.SphereCastNonAlloc(a, radius, d / len, buf, len, Mask(), QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = buf[i];
                if (h.collider == null || (rc != null && h.collider.transform.IsChildOf(rc.RifleTransform))) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) continue; // started inside this collider
                if (h.distance < bestD) { bestD = h.distance; best = h; found = true; }
            }
            return found;
        }

        static string Path(Transform t)
        {
            string s = t.name; int n = 0;
            while (t.parent != null && n++ < 5) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        // Matched against the collider's hierarchy path and physics material name.
        static readonly string[] WoodWords = { "wood", "plank", "timber", "sleeper", "lumber", "pallet", "crate", "fence", "bench", "beam", "cabinet", "shelf", "desk", "log_", "_log", "logs", "stump", "barrel" };

        static RifleAudio.Kind ImpactSound(Collider collider)
        {
            if (collider.GetComponentInParent<CanTarget>() != null) return RifleAudio.Kind.Can;
            if (collider.GetComponentInParent<TrainCar>() != null) return RifleAudio.Kind.Loco;
            var material = collider.sharedMaterial;
            string surface = Path(collider.transform) + "/" + (material == null ? "" : material.name);
            if (surface.IndexOf("brick", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return RifleAudio.Kind.Brick;
            if (collider.GetType().Name.Contains("Terrain")) return RifleAudio.Kind.Thud;
            foreach (var w in WoodWords)
                if (surface.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0) return RifleAudio.Kind.Wood;
            Main.Verbose("impact sound tink (no surface match) for " + surface);
            return RifleAudio.Kind.Tink;
        }

        static void Impact(RaycastHit hit, Vector3 travel, float range, float age, Shot shot)
        {
            Vector3 dir = travel.normalized;
            var col = hit.collider;
            Rigidbody rb = col.attachedRigidbody;
            var itemBase = col.GetComponentInParent<ItemBase>();
            string what = col.name;

            if (itemBase != null)
            {
                // Items on shelves/tables may be kinematic; wake them unless held, racked or boxed.
                if (itemBase.IsGrabbed() || itemBase.IsSnapped || itemBase.InContainer != null) { Main.Verbose("hit (blocked item) " + what); return; }
                var gallery = itemBase.GetComponent<RatGallery>();
                if (gallery != null)
                {
                    if (gallery.TryShot(hit.point, dir)) return; // a rat or the reset disc took the shot (it plays its own sound)
                    Main.Verbose("hit rat gallery housing"); Fx.Impact(hit.point, hit.normal, RifleAudio.Kind.Tink); return;
                }
                var stag = itemBase.GetComponent<ScatterTag>(); if (stag != null) stag.Touch();
                var brk = itemBase.GetComponent<Breakable>();
                if (brk != null) { brk.Break(hit.point, dir); return; }
                rb = itemBase.ItemRigidbody ?? rb;
                if (rb != null && rb.isKinematic) { rb.isKinematic = false; rb.WakeUp(); }
            }
            else if (rb != null)
            {
                // Non-item bodies: never push rolling stock / machinery, only small free props.
                if (TrainCar.Resolve(rb.gameObject) != null || rb.isKinematic) rb = null;
            }

            if (rb == null || rb.mass > shot.maxMass) { Main.Verbose($"hit (static/heavy) {what} layer={LayerMask.LayerToName(col.gameObject.layer)} range={range:0.0}m t={age:0.000}s rb={(rb == null ? "none" : rb.mass.ToString("0.00") + "kg kin=" + rb.isKinematic)} item={(itemBase != null)} path={Path(col.transform)}"); Fx.Impact(hit.point, hit.normal, ImpactSound(col)); return; }
            Fx.Impact(hit.point, hit.normal, ImpactSound(col));
            // Real projectile momentum (mass * speed at impact, speed already reduced by drag) times the game boost,
            // capped so a very light prop is not launched. Heavier props get the same J, so move less.
            float speed = travel.magnitude;
            float j = Mathf.Min(shot.mass * speed * shot.boost, rb.mass * shot.maxDv);
            rb.AddForceAtPosition(dir * j, hit.point, ForceMode.Impulse);
            Main.Verbose($"hit {what} mass={rb.mass:0.00}kg range={range:0.0}m t={age:0.000}s bb={speed:0}m/s J={j:0.000}Ns dv~{j / rb.mass:0.00}m/s");
        }
    }
}
