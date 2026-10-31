using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RailRider
{
    /// <summary>
    /// Four replaceable sound events. Drop 16-bit PCM WAVs named after the Kind into the mod's Audio folder
    /// (Cock.wav, Equip.wav, PutDown.wav, Fire.wav) to override the TEMPORARY synthesised clips below.
    /// </summary>
    public static class RifleAudio
    {
        public enum Kind { Cock, Equip, PutDown, Fire, Shake, Impact, Dry, Tink, Thud, Glass, Can, Loco, Brick }

        static readonly Dictionary<Kind, AudioClip[]> clips = new Dictionary<Kind, AudioClip[]>();
        static readonly Dictionary<Kind, float> last = new Dictionary<Kind, float>();
        static readonly Dictionary<Kind, int> lastTake = new Dictionary<Kind, int>();
        static float lastHandling = -10f;

        public static void Play(RifleController rc, Kind k, float volume = 1f, float pitch = 1f)
        {
            float now = Time.unscaledTime;
            // One action = one sound: handling rattles (equip/put-down) share a window so overlapping
            // callbacks (Ungrabbed + Snapped, Unsnapped + Grabbed) cannot double up.
            if (k == Kind.Equip || k == Kind.PutDown)
            {
                if (now - lastHandling < 0.4f) return;
                lastHandling = now;
            }
            else if (last.TryGetValue(k, out var t) && now - t < (k == Kind.Shake ? 0.11f : k == Kind.Impact ? 0.18f : 0.1f)) return;
            last[k] = now;

            var src = rc.GetComponent<AudioSource>();
            if (src == null)
            {
                src = rc.gameObject.AddComponent<AudioSource>();
                src.spatialBlend = 1f; src.minDistance = 1f; src.maxDistance = 20f; src.playOnAwake = false;
            }
            float basePitch = k == Kind.Equip ? 1.15f : k == Kind.PutDown ? 1.08f : k == Kind.Impact ? 1.1f : 1f;
            src.pitch = basePitch * pitch * UnityEngine.Random.Range(0.95f, 1.05f);
            src.PlayOneShot(Clip(k), (k == Kind.Fire ? 1.0f : k == Kind.Dry ? 0.5f : 0.8f) * Mathf.Clamp01(volume));
        }

        /// <summary>True shortly after a handling/cock/fire sound, so motion rattles do not pile on top.</summary>
        public static bool RecentlyHandled()
        {
            float now = Time.unscaledTime;
            if (now - lastHandling < 0.5f) return true;
            foreach (var k in new[] { Kind.Cock, Kind.Fire })
                if (last.TryGetValue(k, out var t) && now - t < 0.5f) return true;
            return false;
        }

        static AudioClip Clip(Kind k)
        {
            if (!clips.TryGetValue(k, out var pool) || pool[0] == null)
            {
                var wav = LoadWav(k, k.ToString());
                pool = wav != null ? new[] { wav } : LoadTakes(k);
                if (pool.Length == 0)
                {
                    if (k == Kind.Can || k == Kind.Loco || k == Kind.Brick) return Clip(Kind.Tink);
                    else if (k == Kind.Tink || k == Kind.Thud || k == Kind.Glass) { pool = new AudioClip[3]; for (int i = 0; i < 3; i++) pool[i] = Synth(k, i); }
                    else if (k == Kind.Cock || k == Kind.Fire || k == Kind.Dry) pool = new[] { Synth(k, 0) };
                    else
                    {
                        pool = new AudioClip[6];
                        for (int i = 0; i < pool.Length; i++) pool[i] = Rattle(k, i);
                    }
                }
                clips[k] = pool;
            }
            if (pool.Length == 1) return pool[0];
            int index;
            if (lastTake.TryGetValue(k, out int previous) && previous < pool.Length)
            {
                index = UnityEngine.Random.Range(0, pool.Length - 1);
                if (index >= previous) index++;
            }
            else index = UnityEngine.Random.Range(0, pool.Length);
            lastTake[k] = index;
            return pool[index];
        }

        static AudioClip[] LoadTakes(Kind k)
        {
            string directory = Path.Combine(Main.Mod.Path, "Audio");
            if (!Directory.Exists(directory)) return Array.Empty<AudioClip>();
            var takes = new List<AudioClip>();
            foreach (var file in Directory.GetFiles(directory, k + "_??.wav"))
            {
                var clip = LoadWav(k, Path.GetFileNameWithoutExtension(file));
                if (clip != null) takes.Add(clip);
            }
            return takes.ToArray();
        }

        static AudioClip LoadWav(Kind k, string filename)
        {
            try
            {
                string p = Path.Combine(Main.Mod.Path, "Audio", filename + ".wav");
                if (!File.Exists(p)) return null;
                byte[] b = File.ReadAllBytes(p);
                int ch = BitConverter.ToInt16(b, 22), rate = BitConverter.ToInt32(b, 24), bits = BitConverter.ToInt16(b, 34);
                if (bits != 16) { Main.Warn(p + ": only 16-bit PCM supported"); return null; }
                int pos = 12;
                while (pos + 8 < b.Length && System.Text.Encoding.ASCII.GetString(b, pos, 4) != "data") pos += 8 + BitConverter.ToInt32(b, pos + 4);
                int len = BitConverter.ToInt32(b, pos + 4), n = len / 2;
                var f = new float[n];
                for (int i = 0; i < n; i++) f[i] = BitConverter.ToInt16(b, pos + 8 + i * 2) / 32768f;
                var clip = AudioClip.Create("RailRider_" + k, n / ch, ch, rate, false);
                clip.SetData(f, 0);
                Main.Log("loaded audio override " + p);
                return clip;
            }
            catch (Exception e) { Main.Warn("audio load failed for " + k + ": " + e.Message); return null; }
        }

        // TEMPORARY synthesised rattles: steel BBs in a steel tube. Each BB hit is a very short broadband click
        // (noise, not a tone) that also excites two inharmonic tube modes, so the result is a hollow metallic
        // clatter, not a pitched fizz. Hit gaps grow as the BBs settle. Shake = dense strike + settling tail.
        // Cock/Fire/Dry use Synth() below.
        static AudioClip Rattle(Kind k, int variant)
        {
            if (k == Kind.Shake) return ShakeBurst(variant);
            const int rate = 32000;
            var rng = new System.Random((int)k * 104729 + variant * 7907 + 13);
            int hits; float gap0, growth, tube1, tube2, tubeAmp, thud = 0f;
            switch (k)
            {
                case Kind.Shake:  hits = 38 + rng.Next(0, 14); gap0 = 0.0020f; growth = 0.040f; tube1 = 1150; tube2 = 2430; tubeAmp = 0.32f; break;
                case Kind.Impact: hits = 14 + rng.Next(0, 8);  gap0 = 0.0060f; growth = 0.150f; tube1 = 950;  tube2 = 2070; tubeAmp = 0.34f; thud = 0.5f; break;
                case Kind.Equip:  hits = 16 + rng.Next(0, 8);  gap0 = 0.0090f; growth = 0.090f; tube1 = 1020; tube2 = 2210; tubeAmp = 0.30f; break;
                default:          hits = 10 + rng.Next(0, 6);  gap0 = 0.0120f; growth = 0.120f; tube1 = 840;  tube2 = 1830; tubeAmp = 0.30f; thud = 0.35f; break; // PutDown
            }
            float t = 0.002f, total = t;
            var times = new float[hits];
            for (int n = 0; n < hits; n++)
            {
                times[n] = t;
                t += gap0 * Mathf.Exp(growth * n) * (0.4f + 1.2f * (float)rng.NextDouble());
            }
            total = t + 0.06f;
            var d = new float[(int)(rate * total)];
            for (int n = 0; n < hits; n++)
            {
                int s0 = (int)(times[n] * rate);
                float a = (0.2f + 0.8f * Mathf.Pow((float)rng.NextDouble(), 2f)) * (1f - 0.55f * n / hits);
                if (k == Kind.Shake && n < 5) a = Mathf.Max(a, 0.7f);        // the strike
                // broadband click
                int cl = (int)(0.0025f * rate);
                for (int i = 0; i < cl && s0 + i < d.Length; i++)
                    d[s0 + i] += a * ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-i / (0.0005f * rate));
                // tube ring: two inharmonic modes, slightly detuned per hit
                float f1 = tube1 * (1f + 0.05f * ((float)rng.NextDouble() - 0.5f)), f2 = tube2 * (1f + 0.05f * ((float)rng.NextDouble() - 0.5f));
                int rl = (int)(0.045f * rate);
                for (int i = 0; i < rl && s0 + i < d.Length; i++)
                {
                    float tt = (float)i / rate, env = Mathf.Exp(-tt / 0.010f);
                    d[s0 + i] += a * tubeAmp * env * (Mathf.Sin(2f * Mathf.PI * f1 * tt) + 0.6f * Mathf.Sin(2f * Mathf.PI * f2 * tt));
                }
            }
            if (thud > 0f)
                for (int i = 0; i < (int)(rate * 0.03f); i++)
                {
                    float tt = (float)i / rate;
                    d[i] += thud * Mathf.Exp(-tt * 90f) * Mathf.Sin(2f * Mathf.PI * (140f + 40f * variant) * tt);
                }
            float peak = 0f; foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0f) for (int i = 0; i < d.Length; i++) d[i] *= 0.9f / peak;
            var clip = AudioClip.Create("RailRider_temp_" + k + variant, d.Length, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        // Shake: dense, sharp, bright steel-on-steel clatter. Noise-dominated clicks (differentiated noise = bright),
        // a tiny random-pitch metallic zing per hit (so no repeating pitch = nothing musical), a very short unpitched
        // body knock. No sustained resonances, which is what made the earlier version sound like a kalimba / chimes.
        static AudioClip ShakeBurst(int variant)
        {
            const int rate = 32000;
            var rng = new System.Random(555 + variant * 4801);
            int hits = 60 + rng.Next(0, 28);
            var times = new float[hits]; float t = 0.001f;
            for (int n = 0; n < hits; n++) { times[n] = t; t += 0.0014f * Mathf.Exp(0.028f * n) * (0.4f + 1.2f * (float)rng.NextDouble()); }
            var d = new float[(int)(rate * (t + 0.03f))];
            double prev = 0;
            for (int n = 0; n < hits; n++)
            {
                int s0 = (int)(times[n] * rate);
                float a = (0.25f + 0.75f * Mathf.Pow((float)rng.NextDouble(), 2f)) * (1f - 0.5f * n / hits);
                if (n < 6) a = Mathf.Max(a, 0.8f);
                int cl = (int)(0.0016f * rate);
                for (int i = 0; i < cl && s0 + i < d.Length; i++)
                {
                    double nz = rng.NextDouble() * 2 - 1;
                    d[s0 + i] += a * 1.4f * (float)(nz - prev) * Mathf.Exp(-i / (0.00028f * rate)); prev = nz;
                }
                float f = 3800f + 5200f * (float)rng.NextDouble();
                int rl = (int)(0.012f * rate);
                for (int i = 0; i < rl && s0 + i < d.Length; i++)
                {
                    float tt = (float)i / rate;
                    d[s0 + i] += a * 0.30f * Mathf.Exp(-tt / 0.0022f) * Mathf.Sin(2f * Mathf.PI * f * tt);
                }
                int bl = (int)(0.008f * rate);
                for (int i = 0; i < bl && s0 + i < d.Length; i++)
                {
                    float tt = (float)i / rate;
                    d[s0 + i] += a * 0.10f * Mathf.Exp(-tt / 0.003f) * Mathf.Sin(2f * Mathf.PI * (600f + 400f * (float)rng.NextDouble()) * tt);
                }
            }
            float peak = 0f; foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0f) for (int i = 0; i < d.Length; i++) d[i] *= 0.9f / peak;
            var clip = AudioClip.Create("RailRider_temp_Shake" + variant, d.Length, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        // TEMPORARY placeholder audio (clearly not final): noise-click "BB rattle" and a soft low thump.
        static AudioClip Synth(Kind k, int variant)
        {
            const int rate = 22050;
            var rng = new System.Random((int)k * 7919 + 1 + variant * 31);
            float dur = k == Kind.Glass ? 0.55f : k == Kind.Fire ? 0.30f : k == Kind.Tink ? 0.14f : k == Kind.Thud ? 0.10f : k == Kind.Dry ? 0.05f : k == Kind.Cock ? 0.45f : 0.35f;
            var d = new float[(int)(rate * dur)];
            if (k == Kind.Dry)
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / rate;
                    d[i] = Mathf.Exp(-t * 180f) * 0.8f * ((float)rng.NextDouble() * 2f - 1f) + Mathf.Exp(-t * 90f) * 0.5f * Mathf.Sin(2f * Mathf.PI * 1800f * t);
                }
            }
            else if (k == Kind.Fire)
            {
                // soft "whumph": air puff (low-passed noise), piston thump, a little spring twang, a faint crack at the start
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / rate;
                    lp += 0.14f * (((float)rng.NextDouble() * 2f - 1f) - lp);
                    float v = 0.95f * lp * 5f * Mathf.Exp(-t * 12f)
                            + 0.75f * Mathf.Sin(2f * Mathf.PI * 68f * t) * Mathf.Exp(-t * 16f)
                            + 0.22f * Mathf.Sin(2f * Mathf.PI * 610f * t + 2.5f * Mathf.Sin(2f * Mathf.PI * 41f * t)) * Mathf.Exp(-t * 20f);
                    if (i < 70) v += 0.5f * ((float)rng.NextDouble() * 2f - 1f) * (1f - i / 70f);
                    d[i] = v;
                }
            }
            else if (k == Kind.Glass)
            {
                // crash (bright noise burst) + many random high glass pings that thin out
                double prev = 0;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / rate; double n = rng.NextDouble() * 2 - 1;
                    d[i] = 0.9f * (float)(n - prev) * Mathf.Exp(-t * 14f); prev = n;
                }
                for (int g = 0; g < 28; g++)
                {
                    float t0 = 0.5f * Mathf.Pow((float)rng.NextDouble(), 1.6f), f = 2600f + 6200f * (float)rng.NextDouble(), tau = 0.004f + 0.012f * (float)rng.NextDouble();
                    int s0 = (int)(t0 * rate);
                    for (int i = 0; i < (int)(tau * 6f * rate) && s0 + i < d.Length; i++)
                        d[s0 + i] += 0.35f * Mathf.Exp(-(i / (float)rate) / tau) * Mathf.Sin(2f * Mathf.PI * f * (i / (float)rate));
                }
            }
            else if (k == Kind.Tink)
            {
                // BB on metal/can: click + two short inharmonic pings, pitch varies per variant
                float f1 = 2600f + 500f * variant, f2 = f1 * 1.53f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / rate;
                    d[i] = 0.7f * ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 900f)
                         + 0.6f * Mathf.Sin(2f * Mathf.PI * f1 * t) * Mathf.Exp(-t * 45f)
                         + 0.35f * Mathf.Sin(2f * Mathf.PI * f2 * t) * Mathf.Exp(-t * 70f);
                }
            }
            else if (k == Kind.Thud)
            {
                // BB into ground/soft: short low-passed thwap
                float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / rate;
                    lp += 0.2f * (((float)rng.NextDouble() * 2f - 1f) - lp);
                    d[i] = 3.5f * lp * Mathf.Exp(-t * 55f) + 0.5f * Mathf.Sin(2f * Mathf.PI * (190f - 20f * variant) * t) * Mathf.Exp(-t * 60f);
                }
            }
            else
            {
                int clicks = k == Kind.Cock ? 14 : 9;
                for (int c = 0; c < clicks; c++)
                {
                    int s = rng.Next(0, d.Length - 400); float amp = 0.25f + 0.5f * (float)rng.NextDouble();
                    for (int i = 0; i < 400; i++) d[s + i] += amp * Mathf.Exp(-i / 45f) * ((float)rng.NextDouble() * 2f - 1f);
                }
            }
            if (k == Kind.Fire || k == Kind.Tink || k == Kind.Thud || k == Kind.Glass)
            {
                float pk = 0f; foreach (var v in d) pk = Mathf.Max(pk, Mathf.Abs(v));
                if (pk > 0f) for (int i = 0; i < d.Length; i++) d[i] *= 0.95f / pk;
            }
            var clip = AudioClip.Create("RailRider_temp_" + k + variant, d.Length, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>One-shot 3D sound at a world point (impact sounds).</summary>
        public static void PlayAt(Kind k, Vector3 pos, float volume = 1f)
        {
            var go = new GameObject("RailRider_sfx");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 1f; src.minDistance = 2f; src.maxDistance = 60f; src.playOnAwake = false;
            src.pitch = UnityEngine.Random.Range(0.93f, 1.08f);
            var clip = Clip(k);
            src.PlayOneShot(clip, Mathf.Clamp01(volume));
            UnityEngine.Object.Destroy(go, clip.length + 0.5f);
        }
    }
}
