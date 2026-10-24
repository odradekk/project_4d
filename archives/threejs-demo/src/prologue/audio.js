import { Sound } from '../audio.js';

// Ambience per page: her room is quiet and close, the empty page is thin air, the deep page is
// bright and wide (a soft chord does most of the work there).
const PAGE_SOUND = [
  { roomLP: 300, room: 0.12, drone: 55, droneGain: 0, hum: 0, air: 0 },
  { roomLP: 700, room: 0.06, drone: 49, droneGain: 0.008, hum: 0, air: 0.01 },
  { roomLP: 1200, room: 0.05, drone: 65.4, droneGain: 0, hum: 0, air: 0.004 },
];

export class PrologueSound extends Sound {
  init() {
    if (this.ctx) return;
    super.init();
    const ctx = this.ctx;

    const padLP = ctx.createBiquadFilter();
    padLP.type = 'lowpass';
    padLP.frequency.value = 1600;
    this.padGain = ctx.createGain();
    this.padGain.gain.value = 0;
    padLP.connect(this.padGain).connect(this.master);
    for (const f of [261.63, 329.63, 392.0, 493.88, 587.33]) {
      for (const det of [-4, 4]) {
        const o = ctx.createOscillator();
        o.type = 'triangle';
        o.frequency.value = f;
        o.detune.value = det;
        const g = ctx.createGain();
        g.gain.value = 0.2;
        o.connect(g).connect(padLP);
        o.start();
      }
    }

    // Purr: low rumble chopped about 24 times a second.
    const purrSrc = ctx.createBufferSource();
    purrSrc.buffer = this.brown;
    purrSrc.loop = true;
    const purrLP = ctx.createBiquadFilter();
    purrLP.type = 'lowpass';
    purrLP.frequency.value = 220;
    const purrAM = ctx.createGain();
    purrAM.gain.value = 0.5;
    const purrLfo = ctx.createOscillator();
    purrLfo.type = 'square';
    purrLfo.frequency.value = 24;
    const purrDepth = ctx.createGain();
    purrDepth.gain.value = 0.5;
    purrLfo.connect(purrDepth).connect(purrAM.gain);
    this.purrGain = ctx.createGain();
    this.purrGain.gain.value = 0;
    purrSrc.connect(purrLP).connect(purrAM).connect(this.purrGain).connect(this.master);
    purrSrc.start();
    purrLfo.start();

    this.tickTimer = 0;
    this.trainTimer = 25 + Math.random() * 20;
    this.breathTimer = 0;
    this.scrapeTimer = 0;
    this.setLayer(this.page ?? 0);
  }

  setLayer(i) {
    if (!this.ctx) return;
    this.page = i;
    const s = PAGE_SOUND[i];
    this.ramp(this.roomLP.frequency, s.roomLP);
    this.ramp(this.roomGain.gain, s.room);
    for (const o of this.drones) this.ramp(o.frequency, s.drone);
    this.ramp(this.droneGain.gain, s.droneGain);
    this.ramp(this.humGain.gain, s.hum);
    this.ramp(this.airGain.gain, s.air);
    if (this.padGain) this.ramp(this.padGain.gain, i === 2 ? 0.03 : 0, 2.5);
  }

  // The moment she realises: the music and the room tone drain away.
  drain() {
    this.ramp(this.padGain.gain, 0, 2.5);
    this.ramp(this.roomGain.gain, 0.02, 3);
    this.ramp(this.airGain.gain, 0.012, 3);
  }

  tick() {
    this.burst({ dur: 0.015, type: 'highpass', freq: 4200, gain: 0.04 });
  }

  train() {
    const ctx = this.ctx;
    const t = this.now;
    const lp = ctx.createBiquadFilter();
    lp.type = 'lowpass';
    lp.frequency.value = 650;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(0.03, t + 0.8);
    g.gain.setValueAtTime(0.03, t + 2.2);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 3.5);
    lp.connect(g).connect(this.master);
    for (const f of [233, 277]) {
      const o = ctx.createOscillator();
      o.type = 'sawtooth';
      o.frequency.value = f;
      o.connect(lp);
      o.start(t);
      o.stop(t + 3.6);
    }
  }

  // far: the cat is on another page, heard as if through a wall.
  meow(far = false) {
    const ctx = this.ctx;
    const t = this.now;
    const o = ctx.createOscillator();
    o.type = 'sawtooth';
    const base = 500 + Math.random() * 80;
    o.frequency.setValueAtTime(base, t);
    o.frequency.linearRampToValueAtTime(base * 1.6, t + 0.18);
    o.frequency.linearRampToValueAtTime(base * 1.15, t + 0.55);
    const sum = ctx.createGain();
    for (const [f, q, gain] of [[900, 4, 1], [1800, 6, 0.6]]) {
      const bp = ctx.createBiquadFilter();
      bp.type = 'bandpass';
      bp.frequency.value = f;
      bp.Q.value = q;
      const bg = ctx.createGain();
      bg.gain.value = gain;
      o.connect(bp).connect(bg).connect(sum);
    }
    const g = ctx.createGain();
    const peak = far ? 0.05 : 0.22;
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(peak, t + 0.06);
    g.gain.setValueAtTime(peak, t + 0.4);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.62);
    let out = sum;
    if (far) {
      const lp = ctx.createBiquadFilter();
      lp.type = 'lowpass';
      lp.frequency.value = 600;
      out = sum.connect(lp);
    }
    out.connect(g).connect(this.master);
    o.start(t);
    o.stop(t + 0.65);
  }

  hiss() {
    this.burst({ dur: 0.8, type: 'highpass', freq: 2600, gain: 0.35, attack: 0.03 });
  }

  breath(panic) {
    this.burst({ dur: 0.35, freq: 1100, q: 0.7, gain: 0.04 + panic * 0.08, attack: 0.12 });
    this.burst({ dur: 0.45, freq: 800, q: 0.7, gain: 0.03 + panic * 0.06, attack: 0.1, when: 0.45 - panic * 0.2 });
  }

  // The grazer at work: a wet rasp, like something scraping glass from the other side.
  scrape(level) {
    const ctx = this.ctx;
    const t = this.now;
    const s = ctx.createBufferSource();
    s.buffer = this.white;
    const bp = ctx.createBiquadFilter();
    bp.type = 'bandpass';
    bp.frequency.setValueAtTime(380 + Math.random() * 120, t);
    bp.frequency.linearRampToValueAtTime(260, t + 0.7);
    bp.Q.value = 2.5;
    const am = ctx.createGain();
    am.gain.value = 0.5;
    const lfo = ctx.createOscillator();
    lfo.type = 'sawtooth';
    lfo.frequency.value = 28 + Math.random() * 10;
    const depth = ctx.createGain();
    depth.gain.value = 0.5;
    lfo.connect(depth).connect(am.gain);
    const g = ctx.createGain();
    const peak = 0.05 + level * 0.45;
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(peak, t + 0.1);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.75);
    s.connect(bp).connect(am).connect(g).connect(this.master);
    s.start(t, Math.random());
    s.stop(t + 0.8);
    lfo.start(t);
    lfo.stop(t + 0.8);
    this.tone({ freq: 55, to: 38, dur: 0.5, gain: 0.05 + level * 0.25 });
  }

  bite() {
    this.burst({ dur: 0.5, type: 'lowpass', freq: 700, sweepTo: 90, gain: 0.5 });
    this.tone({ freq: 70, to: 30, dur: 0.6, gain: 0.4 });
  }

  pickUp() {
    this.burst({ dur: 0.06, freq: 1400, q: 1, gain: 0.08 });
  }

  putDown() {
    this.tone({ freq: 110, to: 70, dur: 0.1, gain: 0.12 });
    this.burst({ dur: 0.05, freq: 900, q: 1, gain: 0.06 });
  }

  // Turning something into W rises; laying it back falls.
  turn(up) {
    this.burst({ dur: 0.5, freq: up ? 300 : 2600, sweepTo: up ? 2600 : 300, q: 3, gain: 0.18, attack: 0.05 });
    this.tone({ freq: up ? 880 : 1320, to: up ? 1320 : 880, dur: 0.4, gain: 0.03 });
  }

  mirrorObject() {
    this.burst({ dur: 0.35, freq: 1600, sweepTo: 500, q: 4, gain: 0.15 });
    this.burst({ dur: 0.03, type: 'highpass', freq: 3500, gain: 0.1, when: 0.33 });
  }

  // A child's scream, built from a gliding buzz pushed through "ah" vowel formants.
  scream() {
    const ctx = this.ctx;
    const t = this.now;
    const out = ctx.createGain();
    out.gain.setValueAtTime(0.0001, t);
    out.gain.exponentialRampToValueAtTime(0.6, t + 0.06);
    const shaper = ctx.createWaveShaper();
    const curve = new Float32Array(1024);
    for (let i = 0; i < 1024; i++) {
      const x = (i / 1023) * 2 - 1;
      curve[i] = Math.tanh(x * 2.5);
    }
    shaper.curve = curve;
    const o = ctx.createOscillator();
    o.type = 'sawtooth';
    o.frequency.setValueAtTime(700, t);
    o.frequency.exponentialRampToValueAtTime(1150, t + 0.25);
    o.frequency.setTargetAtTime(1020, t + 0.3, 0.6);
    const vib = ctx.createOscillator();
    vib.frequency.value = 6.5;
    const vibGain = ctx.createGain();
    vibGain.gain.value = 40;
    vib.connect(vibGain).connect(o.frequency);
    const noise = ctx.createBufferSource();
    noise.buffer = this.white;
    noise.loop = true;
    const breathHP = ctx.createBiquadFilter();
    breathHP.type = 'highpass';
    breathHP.frequency.value = 1500;
    const breathGain = ctx.createGain();
    breathGain.gain.value = 0.25;
    const sum = ctx.createGain();
    for (const [f, q, gain] of [[1000, 5, 1], [1650, 7, 0.7], [3100, 9, 0.35]]) {
      const bp = ctx.createBiquadFilter();
      bp.type = 'bandpass';
      bp.frequency.value = f;
      bp.Q.value = q;
      const bg = ctx.createGain();
      bg.gain.value = gain;
      o.connect(bp);
      noise.connect(breathHP).connect(breathGain).connect(bp);
      bp.connect(bg).connect(sum);
    }
    sum.connect(shaper).connect(out).connect(this.master);
    const sources = [o, vib, noise];
    for (const s of sources) s.start(t);
    return {
      cut: () => {
        const now = this.now;
        out.gain.cancelScheduledValues(now);
        out.gain.setValueAtTime(0, now);
        for (const s of sources) s.stop(now + 0.05);
      },
    };
  }

  silence() {
    this.ramp(this.master.gain, 0, 0.05);
  }

  // s: { page, calm, purr, panic 0..1, grazer 0..1, danger 0..1 }
  update(dt, s) {
    if (!this.ctx) return;
    if (s.page === 0 && s.calm) {
      this.tickTimer -= dt;
      if (this.tickTimer <= 0) {
        this.tickTimer = 1;
        this.tick();
      }
      this.trainTimer -= dt;
      if (this.trainTimer <= 0) {
        this.trainTimer = 50 + Math.random() * 40;
        this.train();
      }
    }
    this.ramp(this.purrGain.gain, s.purr ? 0.16 : 0, 0.5);
    if (s.panic > 0) {
      this.breathTimer -= dt;
      if (this.breathTimer <= 0) {
        this.breathTimer = 1.7 - s.panic * 0.95;
        this.breath(s.panic);
      }
    }
    if (s.grazer > 0.05) {
      this.scrapeTimer -= dt;
      if (this.scrapeTimer <= 0) {
        this.scrapeTimer = 1.5 - s.grazer * 0.9;
        this.scrape(s.grazer);
      }
    }
    this.ramp(this.crGain.gain, s.grazer * s.grazer * 0.25, 0.4);
    this.ramp(this.subGain.gain, s.grazer * s.grazer * 0.3, 0.4);
    this.beatTimer -= dt;
    if (s.danger > 0.3 && this.beatTimer <= 0) {
      this.beatTimer = 1.1 - s.danger * 0.55;
      this.heartbeat();
    }
  }
}
