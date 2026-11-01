"""Six BB-on-wood impact takes -> ../../Audio/Wood_01..06.wav (48 kHz mono 16-bit). Pure Python, no dependencies.
A dull wooden 'tock': a tiny click, a heavily damped low body thump and two short, quickly dying resonances.
Nothing rings, which is what separates wood from pottery (long bright ring) and brick (dry high crack)."""
import math, random, struct, wave
from pathlib import Path

RATE = 48000
OUT = Path(__file__).resolve().parents[2] / "Audio"

def take(seed):
    rng = random.Random(1938 + seed * 77)
    n = int(RATE * 0.16)
    s = [0.0] * n
    f0 = rng.uniform(330, 620)            # main board resonance
    body = rng.uniform(95, 170)           # low thump of the whole plank
    modes = [  # (freq, decay seconds, amplitude)
        (f0,                 rng.uniform(.012, .022), 0.80),
        (f0 * rng.uniform(1.9, 2.5), rng.uniform(.007, .012), 0.40),
        (f0 * rng.uniform(3.4, 4.6), rng.uniform(.003, .006), 0.20),
        (body,               rng.uniform(.020, .034), 0.55),
    ]
    lp = 0.0
    for i in range(n):
        t = i / RATE
        v = 0.0
        for f, d, a in modes:
            v += a * math.exp(-t / d) * math.sin(2 * math.pi * f * t)
        # contact click: short low-passed noise (softer than a hard surface)
        if t < 0.004:
            lp += 0.45 * (rng.uniform(-1, 1) - lp)
            v += 0.9 * lp * math.exp(-t / 0.0012)
        s[i] = v
    peak = max(abs(x) for x in s) or 1.0
    gain = rng.uniform(0.50, 0.62) / peak
    # tiny fade-out so the clip never ends on a step
    fade = int(RATE * 0.006)
    for i in range(fade):
        s[n - 1 - i] *= i / fade
    return [int(max(-1, min(1, x * gain)) * 32767) for x in s]

def main():
    OUT.mkdir(exist_ok=True)
    for k in range(1, 7):
        pcm = take(k)
        with wave.open(str(OUT / f"Wood_{k:02d}.wav"), "wb") as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
            w.writeframes(struct.pack("<%dh" % len(pcm), *pcm))
        print("wrote", f"Wood_{k:02d}.wav", len(pcm) / RATE, "s")

if __name__ == "__main__":
    main()
