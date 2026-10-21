#!/usr/bin/env python3
"""Original deterministic synthesized effects; no downloaded or sampled third-party audio."""
from pathlib import Path
import math,random,struct,wave
ROOT=Path(__file__).resolve().parents[2]
OUTPUT=ROOT/'Assets/Resources/Audio'
RATE=44100

def write(name,duration,seed,synth):
    rng=random.Random(seed);values=[];low=0.
    for n in range(round(RATE*duration)):
        t=n/RATE;white=rng.uniform(-1,1);low+=.12*(white-low)
        sample=synth(t,white,low)
        # Short edge fades prevent digital discontinuities; cap rather than hard-clip.
        values.append(sample*min(1,t/.0007,(duration-t)/.008))
    peak=max(abs(v)for v in values);gain=.85/peak
    with wave.open(str(OUTPUT/(name+'.wav')),'wb')as wav:
        wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(RATE)
        wav.writeframes(b''.join(struct.pack('<h',round(v*gain*32767))for v in values))

def pistol(t,w,l):
    body=math.sin(2*math.pi*(125*t-45*t*t))*math.exp(-t*48)
    crack=(w-.7*l)*math.exp(-t*105)
    tail=l*math.exp(-t*18)
    slide=0 if t<.09 else .14*w*math.exp(-(t-.09)*180)
    return .65*crack+.8*body+.3*tail+slide

def drone(t,w,l):
    return .8*w*math.exp(-t*135)+.8*math.sin(2*math.pi*(185*t-65*t*t))*math.exp(-t*58)+.3*l*math.exp(-t*22)

def door(t,w,l):
    # Bolt disengage, rising motor/rail friction, then latch-stop clunk.
    bolt=(.6*w+.5*math.sin(2*math.pi*320*t))*math.exp(-t*55)
    active=max(0,min(1,(t-.30)/.12,(1.5-t)/.12))
    motor=active*(.09*math.sin(2*math.pi*(90*t+18*t*t))+.035*math.sin(2*math.pi*420*t)+.16*l)
    stop=0 if t<1.48 else (.45*w+.6*math.sin(2*math.pi*140*(t-1.48)))*math.exp(-(t-1.48)*65)
    return bolt+motor+stop

if __name__=='__main__':
    OUTPUT.mkdir(parents=True,exist_ok=True)
    write('PlayerPistol',.28,481,pistol);write('DroneShot',.23,482,drone);write('LabDoorOpen',1.6,483,door)
