// Every sound is synthesized with WebAudio; there are no audio files.

function noiseBuffer(ctx, seconds, brown) {
  const len = Math.floor(ctx.sampleRate * seconds);
  const buf = ctx.createBuffer(1, len, ctx.sampleRate);
  const d = buf.getChannelData(0);
  let last = 0;
  for (let i = 0; i < len; i++) {
    const w = Math.random() * 2 - 1;
    if (brown) {
      last = (last + 0.02 * w) / 1.02;
      d[i] = last * 3.5;
    } else d[i] = w;
  }
  return buf;
}

const LAYER_SOUND = [
  { roomLP: 380, room: 0.22, drone: 55, droneGain: 0.02, hum: 0.012, air: 0.0 },
  { roomLP: 900, room: 0.12, drone: 49, droneGain: 0.018, hum: 0.0, air: 0.012 },
  { roomLP: 160, room: 0.35, drone: 36.7, droneGain: 0.05, hum: 0.0, air: 0.0 },
];

export class Sound {
  init() {
    if (this.ctx) return;
    const ctx = (this.ctx = new AudioContext());
    this.master = ctx.createGain();
    this.master.gain.value = 0.9;
    this.master.connect(ctx.destination);
    this.white = noiseBuffer(ctx, 2, false);
    this.brown = noiseBuffer(ctx, 4, true);

    const loop = (buf) => {
      const s = ctx.createBufferSource();
      s.buffer = buf;
      s.loop = true;
      s.start();
      return s;
    };

    this.roomLP = ctx.createBiquadFilter();
    this.roomLP.type = 'lowpass';
    this.roomGain = ctx.createGain();
    loop(this.brown).connect(this.roomLP).connect(this.roomGain).connect(this.master);

    this.droneLP = ctx.createBiquadFilter();
    this.droneLP.type = 'lowpass';
    this.droneLP.frequency.value = 140;
    this.droneGain = ctx.createGain();
    this.droneLP.connect(this.droneGain).connect(this.master);
    this.drones = [0, 0.7].map((det) => {
      const o = ctx.createOscillator();
      o.type = 'sawtooth';
      o.detune.value = det * 10;
      o.connect(this.droneLP);
      o.start();
      return o;
    });

    this.humGain = ctx.createGain();
    for (const [f, g] of [[100, 1], [200, 0.4], [300, 0.15]]) {
      const o = ctx.createOscillator();
      o.frequency.value = f;
      const og = ctx.createGain();
      og.gain.value = g;
      o.connect(og).connect(this.humGain);
      o.start();
    }
    this.humGain.connect(this.master);

    const airHP = ctx.createBiquadFilter();
    airHP.type = 'highpass';
    airHP.frequency.value = 3000;
    this.airGain = ctx.createGain();
    loop(this.white).connect(airHP).connect(this.airGain).connect(this.master);

    // The creature: a low throb under a band of breath-like noise, louder as it gets closer in 4D.
    this.crBP = ctx.createBiquadFilter();
    this.crBP.type = 'bandpass';
    this.crBP.frequency.value = 260;
    this.crBP.Q.value = 5;
    this.crGain = ctx.createGain();
    this.crGain.gain.value = 0;
    loop(this.white).connect(this.crBP).connect(this.crGain).connect(this.master);
    const lfo = ctx.createOscillator();
    lfo.frequency.value = 0.6;
    const lfoGain = ctx.createGain();
    lfoGain.gain.value = 120;
    lfo.connect(lfoGain).connect(this.crBP.frequency);
    lfo.start();
    const sub = ctx.createOscillator();
    sub.frequency.value = 33;
    this.subGain = ctx.createGain();
    this.subGain.gain.value = 0;
    sub.connect(this.subGain).connect(this.master);
    sub.start();

    this.peekGain = ctx.createGain();
    this.peekGain.gain.value = 0;
    const whine = ctx.createOscillator();
    whine.frequency.value = 1760;
    whine.connect(this.peekGain).connect(this.master);
    whine.start();

    this.beatTimer = 0;
    this.rustleTimer = 0;
    this.setLayer(0);
  }

  get now() {
    return this.ctx.currentTime;
  }

  ramp(param, value, time = 0.4) {
    param.cancelScheduledValues(this.now);
    param.setTargetAtTime(value, this.now, time / 3);
  }

  setLayer(i) {
    if (!this.ctx) return;
    const s = LAYER_SOUND[i];
    this.ramp(this.roomLP.frequency, s.roomLP);
    this.ramp(this.roomGain.gain, s.room);
    for (const o of this.drones) this.ramp(o.frequency, s.drone);
    this.ramp(this.droneGain.gain, s.droneGain);
    this.ramp(this.humGain.gain, s.hum);
    this.ramp(this.airGain.gain, s.air);
  }

  burst({ dur = 0.15, type = 'bandpass', freq = 1000, q = 1, gain = 0.2, attack = 0.005, sweepTo, when = 0 }) {
    const ctx = this.ctx;
    const t = this.now + when;
    const s = ctx.createBufferSource();
    s.buffer = this.white;
    s.playbackRate.value = 0.8 + Math.random() * 0.4;
    const f = ctx.createBiquadFilter();
    f.type = type;
    f.frequency.setValueAtTime(freq, t);
    if (sweepTo) f.frequency.exponentialRampToValueAtTime(sweepTo, t + dur);
    f.Q.value = q;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(gain, t + attack);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    s.connect(f).connect(g).connect(this.master);
    s.start(t, Math.random() * 1.5);
    s.stop(t + dur + 0.05);
  }

  tone({ freq = 60, to, dur = 0.3, gain = 0.3, type = 'sine', when = 0 }) {
    const ctx = this.ctx;
    const t = this.now + when;
    const o = ctx.createOscillator();
    o.type = type;
    o.frequency.setValueAtTime(freq, t);
    if (to) o.frequency.exponentialRampToValueAtTime(to, t + dur);
    const g = ctx.createGain();
    g.gain.setValueAtTime(gain, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g).connect(this.master);
    o.start(t);
    o.stop(t + dur + 0.05);
  }

  step(running, surface) {
    if (!this.ctx) return;
    const freq = { ',': 500, ';': 1700 }[surface] ?? 1200;
    this.burst({ dur: 0.09, freq: freq * (0.85 + Math.random() * 0.3), q: 1.2, gain: running ? 0.28 : 0.16 });
    this.tone({ freq: 80, dur: 0.07, gain: running ? 0.12 : 0.06 });
  }

  flip() {
    this.burst({ dur: 0.9, freq: 3200, sweepTo: 120, q: 2, gain: 0.45, attack: 0.05 });
    this.tone({ freq: 70, to: 28, dur: 0.9, gain: 0.45 });
    for (let i = 0; i < 4; i++) this.burst({ dur: 0.05, type: 'highpass', freq: 2500, gain: 0.12, when: 0.3 + i * 0.07 });
  }

  blocked() {
    this.tone({ freq: 70, to: 40, dur: 0.35, gain: 0.5 });
    this.burst({ dur: 0.2, type: 'lowpass', freq: 220, gain: 0.4 });
  }

  peek(on) {
    if (!this.ctx) return;
    this.burst({ dur: 0.04, type: 'highpass', freq: 3000, gain: 0.15 });
    this.ramp(this.peekGain.gain, on ? 0.004 : 0, 0.3);
  }

  sting() {
    for (const f of [110, 116.5, 155.6, 233]) this.tone({ freq: f, dur: 2.2, gain: 0.08, type: 'sawtooth' });
    this.burst({ dur: 1.5, type: 'lowpass', freq: 1800, sweepTo: 80, gain: 0.35 });
  }

  caught() {
    for (const f of [92, 97.5, 138.6, 196, 207.7]) this.tone({ freq: f, to: f * 0.5, dur: 2.5, gain: 0.1, type: 'sawtooth' });
    this.burst({ dur: 1.2, freq: 600, sweepTo: 60, q: 0.7, gain: 0.7 });
    this.flip();
  }

  paper() {
    this.burst({ dur: 0.12, type: 'highpass', freq: 2200, gain: 0.18 });
    this.burst({ dur: 0.18, type: 'highpass', freq: 1800, gain: 0.12, when: 0.1 });
  }

  eat() {
    for (let i = 0; i < 3; i++) this.burst({ dur: 0.12, type: 'lowpass', freq: 500, gain: 0.25, when: i * 0.35 });
  }

  heartbeat() {
    this.tone({ freq: 55, to: 35, dur: 0.15, gain: 0.5 });
    this.tone({ freq: 50, to: 32, dur: 0.15, gain: 0.35, when: 0.22 });
  }

  // prox: 0..1 closeness of the creature; danger: 0..1 for the heartbeat.
  update(dt, prox, danger) {
    if (!this.ctx) return;
    this.ramp(this.crGain.gain, prox * prox * 0.5, 0.3);
    this.ramp(this.subGain.gain, prox * prox * 0.35, 0.3);
    // Rustling like pages being turned, coming from nowhere in particular.
    this.rustleTimer -= dt;
    if (prox > 0.25 && this.rustleTimer <= 0) {
      this.rustleTimer = 0.2 + Math.random() * (2.5 - prox * 2);
      const n = 1 + Math.floor(Math.random() * 4);
      for (let i = 0; i < n; i++) {
        this.burst({ dur: 0.06 + Math.random() * 0.08, type: 'highpass', freq: 1500 + Math.random() * 2000, gain: 0.05 + prox * 0.15, when: i * 0.06 });
      }
    }
    this.beatTimer -= dt;
    if (danger > 0.3 && this.beatTimer <= 0) {
      this.beatTimer = 1.1 - danger * 0.55;
      this.heartbeat();
    }
  }
}
