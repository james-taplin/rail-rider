"""Offline listening sketches. All sound layers are synthesised for Rail-Rider."""
import numpy as np
import wave
from pathlib import Path
rate=48000
out=Path(__file__).parent
def render(bright, take=0):
    rng=np.random.default_rng(177 + 997*take)
    reel=np.zeros(rate*11)
    def strike(at,gain,noise_ms,modes,soft=1):
        start=round(at*rate)
        duration=max([noise_ms]+[m[1] for m in modes])*7/1000
        t=np.arange(min(int(duration*rate)+1,len(reel)-start))/rate
        if take:
            modes=[(hz*rng.uniform(.89,1.12),ms*rng.uniform(.72,1.25),amp*rng.uniform(.82,1.1)) for hz,ms,amp in modes]
        noise=rng.normal(size=len(t))
        if soft>1: noise=np.convolve(noise,np.ones(soft)/np.sqrt(soft),'same')[:len(t)]
        s=noise*.42*np.exp(-t/(noise_ms*.001))
        for hz,ms,amp in modes:
            s+=amp*np.exp(-t/(ms*.001))*np.sin(2*np.pi*hz*t)
        reel[start:start+len(t)]+=gain*s
    def shake(at,density,g):
        for t in sorted(rng.uniform(0,.19,density)):
            f=rng.uniform(2700,5900)
            strike(at+t,g*rng.uniform(.18,.8),.32,[(f,1.4,.48),(f*.41,2.2,.21)])
    shake(.35,27 if not take else rng.integers(14,38),.31);shake(.69,17,.22)
    strike(1.55,.5,2,[(740,5,.35),(1420,3,.3)])
    for t in np.linspace(.035,.22,24):
        strike(1.55+t,.09*np.sin(t/.25*np.pi),1.8,[(620+1800*t,3,.4)],6)
    strike(1.80,.36,1.3,[(390,11,.55),(1230,6,.5)])
    strike(2.06,.54,1.7,[(520,9,.52),(1840,4,.36)])
    strike(2.83,.91,2.3,[(174,12,.65),(523,9,.36),(1930,2,.33)],3)
    strike(2.838,.45,7,[(322,9,.3),(1100,6,.21)],8)
    if bright:
        strike(2.846,.33,1.4,[(810,20,.48),(1415,12,.3)])
    # Empty can buckles, then skips against the ground.
    strike(4,.58,1.7,[(890,22,.6),(1305,30,.46),(2240,12,.36)])
    for t,a in [(.045,.26),(.13,.17),(.22,.07)]:
        strike(4+t*(rng.uniform(.75,1.18) if take else 1),a,2.4,[(720,15,.7),(1540,19,.4)])
    # Breaking bottle: initial fracture and staggered fragment contacts.
    strike(5.25,.85,4.5,[(2820,8,.35),(4700,4,.4)])
    for t in sorted(rng.uniform(.027,.40,31)):
        f=rng.uniform(2200,7400)
        strike(5.25+t,.33*np.exp(-6*t)*rng.uniform(.3,1),.48,[(f,8,.85),(f*1.38,3,.33)])
    strike(6.5,.52,5.2,[(237,6,.47)],13)
    for t in rng.uniform(.009,.06,10):strike(6.5+t,.047,1.4,[],5)
    strike(7.75,.68,1.9,[(390,24,.45),(891,38+bright*29,.48),(2100,21,.36),(3170,14,.15)])
    strike(9,.62,1.7,[(1130,3,.36),(2870,2,.26)],3)
    for t in [.011,.025,.044,.083]:strike(9+t*(rng.uniform(.65,1.3) if take else 1),.095*np.exp(-20*t),1.4,[],4)
    if bright:
        direct=reel.copy()
        for ms,amp in [(23,.12),(56,.08),(91,.035)]:
            n=int(rate*ms/1000)
            reel[n:]+=amp*np.convolve(direct[:-n],np.ones(7)/7,'same')
        reel+=.18*np.diff(reel,prepend=0)
    reel=reel[:int(9.8*rate)]
    # Common gain preserves relative levels across both auditions.
    pcm=(np.tanh(reel*1.1)*28500).astype('<i2')
    return pcm

def save(path,pcm):
    with wave.open(str(path),'wb') as w:
        w.setparams((1,2,rate,0,'NONE',''));w.writeframes(pcm.tobytes())

if __name__=="__main__":
    for bright,label in enumerate(("A_Close_and_dry","B_Yard_plinker")):
        save(out/(label+'.wav'),render(bright))