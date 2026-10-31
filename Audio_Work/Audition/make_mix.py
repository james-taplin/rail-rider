"""Extract the approved mix and assemble the review from runtime WAV files."""
from make_auditions import render,save,rate,out,np,wave
audio=out.parents[1]/"Audio"
audio.mkdir(exist_ok=True)
banks=[("Shake",0,.35,.64,8),("Cock",0,1.55,2.33,1),("Fire",0,2.83,3.35,1),
       ("Can",1,4,4.65,6),("Glass",0,5.25,5.93,6),("Thud",0,6.5,6.73,6),
       ("Loco",1,7.75,8.51,6),("Brick",0,9,9.34,6)]
preview=[]
for take in range(8):
    reels=[render(bright,take) for bright in (0,1)]
    for name,bright,start,end,count in banks:
        if take>=count: continue
        clip=reels[bright][round(start*rate):round(end*rate)].copy()
        nonzero=np.flatnonzero(clip)
        clip=clip[nonzero[0]:nonzero[-1]+2]
        filename=name if count==1 else f"{name}_{take+1:02}"
        save(audio/(filename+".wav"),clip)
for name,bright,start,end,count in banks:
    paths=sorted(audio.glob(name+"_*.wav"))[:3] if count>1 else [audio/(name+".wav")]
    for path in paths:
        with wave.open(str(path),"rb") as w:
            assert w.getsampwidth()==2 and w.getframerate()==rate and w.getnchannels()==1
            preview.extend([np.frombuffer(w.readframes(w.getnframes()),dtype="<i2"),np.zeros(round(rate*.16),dtype="<i2")])
    preview.append(np.zeros(round(rate*.35),dtype="<i2"))
save(out/"Mixed_repeat_hits.wav",np.concatenate(preview))
