using UnityEngine;

namespace RailRider
{
    /// <summary>Small impact feedback: a puff of dust (or sparks off hard/item hits) plus a positional sound.</summary>
    static class Fx
    {
        static Material mat;

        static Material Mat()
        {
            if (mat != null) return mat;
            foreach (var n in new[] { "Sprites/Default", "Particles/Standard Unlit", "Legacy Shaders/Particles/Alpha Blended", "Unlit/Color" })
            {
                var sh = Shader.Find(n);
                if (sh != null) { mat = new Material(sh); return mat; }
            }
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
            Object.DestroyImmediate(tmp);
            return mat;
        }

        public static void Impact(Vector3 point, Vector3 normal, RifleAudio.Kind sound)
        {
            Burst(point, normal, new Color(0.62f, 0.56f, 0.46f, 0.7f), 14, 0.03f, 0.07f, 0.4f, 1.3f, 0.35f, 0.7f, false);
            if (sound != RifleAudio.Kind.Thud && sound != RifleAudio.Kind.Brick && sound != RifleAudio.Kind.Wood) Burst(point, normal, new Color(1f, 0.85f, 0.4f, 1f), 7, 0.006f, 0.012f, 2f, 5f, 0.08f, 0.18f, true);
            RifleAudio.PlayAt(sound, point, sound == RifleAudio.Kind.Thud ? 0.8f : 0.9f);
        }

        public static void Glass(Vector3 point, Vector3 dir)
        {
            Burst(point, -dir, new Color(0.55f, 0.95f, 0.65f, 0.8f), 26, 0.004f, 0.012f, 1f, 4f, 0.5f, 1.0f, true);
            RifleAudio.PlayAt(RifleAudio.Kind.Glass, point, 1f);
        }

        static void Burst(Vector3 p, Vector3 n, Color c, int count, float sMin, float sMax, float vMin, float vMax, float lMin, float lMax, bool sparks)
        {
            var go = new GameObject("RailRider_fx");
            go.transform.position = p + n * 0.01f;
            go.transform.rotation = Quaternion.LookRotation(n);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lMin, lMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(vMin, vMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sMin, sMax);
            main.startColor = c;
            main.gravityModifier = sparks ? 1.5f : -0.05f;
            main.maxParticles = 32;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = sparks ? 45f : 55f; sh.radius = 0.004f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat();
            ps.Play();
            Object.Destroy(go, lMax + 0.3f);
        }
    }
}
