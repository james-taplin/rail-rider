"""Original sharp-steel gallery sound candidates. Requires Python 3 + NumPy.

This version replaces the rejected hollow/wooden sketch: no samples are used.
The sound design combines sharp inharmonic steel attacks with a short shared
metal housing resonance and sparse mechanical return clicks. PCM 16-bit, mono.
"""
from pathlib import Path
import wave
import numpy as np

HERE = Path(__file__).resolve().parent
SR = 44100

def steel_contact(seed, duration, fmin=2500, fmax=10500, modes=24,
                  decay_min=.005, decay_max=.035, attack=.00016, noise=.12):
    """Excited thin-metal modes with irregular spacing and tiny bright impact."""
    rng = np.random.default_rng(seed)
    t = np.arange(round(duration*SR), dtype=np.float64)/SR
    # Inharmonic modal frequencies avoid a single tuned-pitch/electronic ping.
    frequencies = np.linspace(fmin, fmax, modes) * rng.uniform(.965, 1.035, modes)
    phases = rng.uniform(-np.pi, np.pi, modes)
    weights = rng.uniform(.5, 1, modes) / (frequencies/fmin)**.45
    decays = rng.uniform(decay_min, decay_max, modes)
    signal = np.zeros_like(t)
    for f, phase, amp, tau in zip(frequencies, phases, weights, decays):
        signal += amp*np.sin(2*np.pi*f*t+phase)*np.exp(-t/tau)
    signal *= (1-np.exp(-t/attack))
    # High-frequency, sub-millisecond pick/contact on the struck steel.
    white = rng.normal(size=len(t))
    bright = white - np.convolve(white, np.ones(11)/11, mode='same')
    signal += noise * bright * np.exp(-t/.0008)
    return signal / max(np.max(np.abs(signal)), 1e-12)

def metal_scrape(seed, duration):
    """A brief rough mechanical scrape made of irregular, tiny metal ticks."""
    rng = np.random.default_rng(seed)
    t = np.arange(round(duration*SR), dtype=np.float64)/SR
    white = rng.normal(size=len(t))
    bright = white - np.convolve(white, np.ones(7)/7, mode='same')
    envelope = (1-np.exp(-t/.0015))*np.exp(-t/.012)
    out = bright*envelope*.08
    for j, at in enumerate(sorted(rng.uniform(.001, duration-.006, 9))):
        pulse = steel_contact(seed+31+j, .012, 3300, 12200, 12, .0015, .004, noise=.22)
        add_at(out, pulse, at, rng.uniform(.05,.18)*(1-at/duration))
    return out / max(np.max(np.abs(out)), 1e-12)

def add_at(dst, src, seconds, gain=1):
    start = round(seconds*SR)
    n = max(0, min(len(src), len(dst)-start))
    if n: dst[start:start+n] += src[:n]*gain

def housing(signal):
    """Add a shared short, inharmonic sheet-metal enclosure response.

    This is a synthetic impulse response, not a recording of a tub. The lower
    mode is kept quiet and all modes decay within fractions of a second so it
    adds metallic body without masking the direct attacks with a woody thud.
    """
    t=np.arange(round(.145*SR),dtype=np.float64)/SR
    ir=np.zeros_like(t)
    for freq,decay,amp in [(1260,.030,.08),(1780,.041,.18),(2430,.050,.22),
                           (3300,.041,.19),(4520,.028,.12),(6100,.018,.08)]:
        ir+=amp*np.sin(2*np.pi*freq*t)*np.exp(-t/decay)
    ir*=1-np.exp(-t/.00025)
    n=1<<(len(signal)+len(ir)-2).bit_length()
    response=np.fft.irfft(np.fft.rfft(signal,n)*np.fft.rfft(ir,n),n)[:len(signal)]
    response*=np.max(np.abs(signal))/max(np.max(np.abs(response)),1e-12)
    return signal + .30*response

def level(signal, peak):
    signal = signal.copy()
    fade = min(round(.006*SR), len(signal))
    signal[-fade:] *= np.linspace(1,0,fade)
    return signal * (peak/max(np.max(np.abs(signal)), 1e-12))

def write(name, signal):
    peak = np.max(np.abs(signal))
    assert np.isfinite(peak) and peak < 1, (name, peak)
    with wave.open(str(HERE/name), 'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(SR)
        w.writeframes(np.rint(signal*32767).astype('<i2').tobytes())

falls=[]
for i in range(4):
    rng=np.random.default_rng(2400+i)
    y=np.zeros(round(.26*SR))
    scale=rng.uniform(.95,1.06)
    # A definite sharp plate "ting", followed distinctly by the pivot's chhk.
    add_at(y,steel_contact(100+i,.14,1700*scale,9200*scale,32,.008,.042,noise=.20),0,.88)
    add_at(y,metal_scrape(200+i,.040),.060+rng.uniform(-.004,.004),.30)
    for j,at in enumerate([.078,.096,.115]):
        add_at(y,steel_contact(500+10*i+j,.035,2200,10000,18,.002,.009,noise=.20),
               at+rng.uniform(-.003,.003),[.30,.16,.10][j])
    y=level(housing(y),.58); falls.append(y);write(f'rat_fall_{i+1:02}.wav',y)

# A close sibling of the rat's ting-chhk: struck plate, then short pivot sound.
reset=np.zeros(round(.20*SR))
add_at(reset,steel_contact(710,.12,1750,9000,32,.006,.035,noise=.20),0,.88)
add_at(reset,metal_scrape(713,.035),.056,.27)
for j,(at,gain) in enumerate([(.077,.27),(.096,.14),(.114,.08)]):
    add_at(reset,steel_contact(720+j,.032,2200,10000,18,.002,.008,noise=.20),at,gain)
reset=level(housing(reset),.52);write('reset_hit.wav',reset)

# Separate click when the deflected paddle strikes the support axle.
pole=steel_contact(711,.11,2400,10000,25,.002,.010,noise=.27)
pole=level(housing(pole),.16);write('reset_pole_contact.wav',pole)

rises=[]
for i in range(4):
    rng=np.random.default_rng(820+i)
    y=np.zeros(round(.16*SR))
    # Three discrete mechanism contacts; pitch varies within each individual
    # return as well as across the four takes. Fewer clicks reduce rattling.
    previous=None
    for j,(at,gain) in enumerate(zip([.005,.036,.072],[.80,.55,.42])):
        pitch=rng.uniform(.74,1.32)
        while previous is not None and abs(pitch-previous)<.12:
            pitch=rng.uniform(.74,1.32)
        previous=pitch
        part=steel_contact(910+i*30+j,.065,2100*pitch,10000*pitch,20,.0025,.011,noise=.22)
        add_at(y,part,at+rng.uniform(-.002,.002),gain)
    y=level(housing(y),.19);rises.append(y);write(f'rat_rise_{i+1:02}.wav',y)

# Same dry timing as the interactive viewer, making level comparisons easy.
aud=np.zeros(round(5.5*SR));add_at(aud,falls[0],.05);add_at(aud,reset,1.05);add_at(aud,pole,1.26)
add_at(aud,reset,1.90);add_at(aud,pole,2.11);add_at(aud,rises[0],2.11);add_at(aud,reset,3.25);add_at(aud,pole,3.46)
for i,x in enumerate(rises):add_at(aud,x,3.46+i*.006)
write('gallery_sound_audition.wav',aud)
mix=np.zeros(round(.30*SR))
for i,x in enumerate(rises):add_at(mix,x,i*.006)
rise_mix=mix.copy();add_at(mix,pole,0)
print('peaks:', 'fall',*(f'{np.max(np.abs(x)):.3f}' for x in falls),
      'reset',f'{np.max(np.abs(reset)):.3f}', 'rise',*(f'{np.max(np.abs(x)):.3f}' for x in rises))
print('contact + four-rise absolute peak bound:',np.max(np.abs(pole))+sum(np.max(np.abs(x)) for x in rises))
print('one/four aligned-window rise RMS:',np.sqrt(np.mean(np.pad(rises[0],(0,len(mix)-len(rises[0])))**2)),np.sqrt(np.mean(rise_mix**2)))
print('contact + four-rise mix peak:',np.max(np.abs(mix)),'audition peak:',np.max(np.abs(aud)))
