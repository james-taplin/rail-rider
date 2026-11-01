# Reactive gallery sound candidates

The first set was rejected as too hollow and wooden. The subsequent return was also rejected as too rattly. This revision adds a shared, short, synthesised inharmonic steel-housing resonance to every sound while retaining bright attacks. Each return now has three distinct mechanical contacts with varied pitches within each take, rather than seven. The synthetic enclosure response is an interpretation, not a recording of a real tub. The generator uses no external samples. All files are 44.1 kHz mono 16-bit PCM, generated reproducibly by `generate_sounds.py`; they remain review candidates.

- `rat_fall_01.wav`–`rat_fall_04.wav`: 0.26 s. A sharp plate ting followed by a short mechanical chhk; four timbre/timing variants.
- `reset_hit.wav`: 0.20 s. A related ting and immediately following chhk, as requested; play on every reset hit, even if all rats are standing.
- `reset_pole_contact.wav`: 0.11 s. A quieter steel tick for when the reset paddle meets the support rod; play at the actual contact time, independently of rat state.
- `rat_rise_01.wav`–`rat_rise_04.wav`: 0.16 s each. Each has three differently pitched metallic mechanism contacts; start one per fallen rat at the beginning of its Up clip, staggering layers by a few milliseconds. Do not play for rats already standing.
- `gallery_sound_audition.wav`: 5.5 s comparison: one fall, an empty reset, a reset with one rat, then a reset with four rats. The comparison has a pole-contact tick roughly 0.21 s after each initial reset strike; the interactive viewer should follow the actual animation's contact timing.

Each rise has a 0.19 full-scale peak; the contact tick peaks at 0.16. The conservative sum of their individual absolute peaks is 0.92. A test with 6 ms spacing measures 0.0158 RMS for one rise and 0.0333 RMS for four over the same 0.30 s window; pole tick plus four rises peaked at 0.247 in that test. The comparison audition peaks at 0.58. No compressor is baked into these WAV files.

These timbral changes are a response to the feedback, not proof of a satisfactory sound. The candidates have not been subjectively auditioned through speakers or checked in the game. Listen to the audition or viewer and adjust by ear during review.
