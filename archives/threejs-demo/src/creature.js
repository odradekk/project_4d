import * as THREE from 'three';
import { creatureMaterial } from './ps1.js';

// W is periodic with three pages: going 0 -> 1 -> 2 -> 0 walks around the house once.
export const PERIOD = 3;
export const wrapW = (d) => {
  d = ((d % PERIOD) + PERIOD) % PERIOD;
  return d > PERIOD / 2 ? d - PERIOD : d;
};
const modW = (w) => ((w % PERIOD) + PERIOD) % PERIOD;

// Each ball contributes (1 - d²/R²)³ inside radius R; the surface is where the sum reaches ISO.
// For a lone ball that surface sits at radius r, so R² = r² / (1 - ∛ISO).
const ISO = 0.2;
const R2K = 1 / (1 - Math.cbrt(ISO));

const CORNERS = [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]];
const TETS = [[0, 5, 1, 6], [0, 1, 2, 6], [0, 2, 3, 6], [0, 3, 7, 6], [0, 7, 4, 6], [0, 4, 5, 6]];

// Cuts the 4D field with the hyperplane w = const and triangulates the 3D cross-section
// with marching tetrahedra on a coarse grid, which keeps the result low-poly.
export class Slicer {
  constructor({ periodic = true } = {}) {
    // periodic: W wraps around every PERIOD units, as in the three-page loop of the first demo.
    this.periodic = periodic;
    this.cell = 0;
    this.nx = 28;
    this.ny = 26;
    this.nz = 28;
    this.sx = this.nx + 1;
    this.sxy = this.sx * (this.ny + 1);
    this.values = new Float32Array(this.sxy * (this.nz + 1));
    this.maxTris = 30000;
    this.pos = new Float32Array(this.maxTris * 9);
    this.geo = new THREE.BufferGeometry();
    const attr = new THREE.BufferAttribute(this.pos, 3);
    attr.setUsage(THREE.DynamicDrawUsage);
    this.geo.setAttribute('position', attr);
    this.geo.setDrawRange(0, 0);
    this.cp = Array.from({ length: 8 }, () => [0, 0, 0]);
    this.cv = new Float32Array(8);
  }

  slice(balls, w, cx, cy, cz, cell) {
    const { nx, ny, nz, sx, sxy, values } = this;
    this.cell = cell;
    const ox = cx - (nx * cell) / 2;
    const oy = cy - (ny * cell) / 2;
    const oz = cz - (nz * cell) / 2;
    values.fill(-ISO);
    for (const b of balls) {
      const dw = this.periodic ? wrapW(b.w - w) : b.w - w;
      const R2 = b.r * b.r * R2K;
      const rest = R2 - dw * dw;
      if (rest <= 0) continue;
      const R = Math.sqrt(rest);
      const i0 = Math.max(0, Math.ceil((b.x - R - ox) / cell));
      const i1 = Math.min(nx, Math.floor((b.x + R - ox) / cell));
      const j0 = Math.max(0, Math.ceil((b.y - R - oy) / cell));
      const j1 = Math.min(ny, Math.floor((b.y + R - oy) / cell));
      const k0 = Math.max(0, Math.ceil((b.z - R - oz) / cell));
      const k1 = Math.min(nz, Math.floor((b.z + R - oz) / cell));
      for (let k = k0; k <= k1; k++) {
        const dz = oz + k * cell - b.z;
        for (let j = j0; j <= j1; j++) {
          const dy = oy + j * cell - b.y;
          const dyz = dz * dz + dy * dy + dw * dw;
          let idx = i0 + sx * j + sxy * k;
          for (let i = i0; i <= i1; i++, idx++) {
            const dx = ox + i * cell - b.x;
            const q = (dx * dx + dyz) / R2;
            if (q < 1) {
              const s = 1 - q;
              values[idx] += s * s * s;
            }
          }
        }
      }
    }

    const off = [0, 1, 1 + sx, sx, sxy, 1 + sxy, 1 + sx + sxy, sx + sxy];
    const { cp, cv } = this;
    this.n = 0;
    for (let k = 0; k < nz; k++) {
      for (let j = 0; j < ny; j++) {
        for (let i = 0; i < nx; i++) {
          const base = i + sx * j + sxy * k;
          let pos = 0;
          for (let c = 0; c < 8; c++) {
            cv[c] = values[base + off[c]];
            if (cv[c] > 0) pos++;
          }
          if (pos === 0 || pos === 8) continue;
          for (let c = 0; c < 8; c++) {
            cp[c][0] = ox + (i + CORNERS[c][0]) * cell;
            cp[c][1] = oy + (j + CORNERS[c][1]) * cell;
            cp[c][2] = oz + (k + CORNERS[c][2]) * cell;
          }
          for (const t of TETS) this.tet(t[0], t[1], t[2], t[3]);
        }
      }
    }
    this.geo.attributes.position.needsUpdate = true;
    this.geo.setDrawRange(0, this.n * 3);
  }

  edge(a, b, out) {
    const { cp, cv } = this;
    const t = cv[a] / (cv[a] - cv[b]);
    out[0] = cp[a][0] + (cp[b][0] - cp[a][0]) * t;
    out[1] = cp[a][1] + (cp[b][1] - cp[a][1]) * t;
    out[2] = cp[a][2] + (cp[b][2] - cp[a][2]) * t;
  }

  tri(p, q, r) {
    if (this.n >= this.maxTris) return;
    const o = this.n * 9;
    const P = this.pos;
    P[o] = p[0]; P[o + 1] = p[1]; P[o + 2] = p[2];
    P[o + 3] = q[0]; P[o + 4] = q[1]; P[o + 5] = q[2];
    P[o + 6] = r[0]; P[o + 7] = r[1]; P[o + 8] = r[2];
    this.n++;
  }

  tet(a, b, c, d) {
    const v = this.cv;
    let ni = 0;
    let no = 0;
    for (const id of [a, b, c, d]) {
      if (v[id] > 0) INS[ni++] = id;
      else OUTS[no++] = id;
    }
    if (ni === 1 || ni === 3) {
      const lone = ni === 1 ? INS[0] : OUTS[0];
      const rest = ni === 1 ? OUTS : INS;
      this.edge(lone, rest[0], E0);
      this.edge(lone, rest[1], E1);
      this.edge(lone, rest[2], E2);
      this.tri(E0, E1, E2);
    } else if (ni === 2) {
      this.edge(INS[0], OUTS[0], E0);
      this.edge(INS[0], OUTS[1], E1);
      this.edge(INS[1], OUTS[1], E2);
      this.edge(INS[1], OUTS[0], E3);
      this.tri(E0, E1, E2);
      this.tri(E0, E2, E3);
    }
  }
}
const INS = [0, 0, 0, 0];
const OUTS = [0, 0, 0, 0];
const E0 = [0, 0, 0];
const E1 = [0, 0, 0];
const E2 = [0, 0, 0];
const E3 = [0, 0, 0];

const BOUNDS = { x0: 0.6, x1: 13.4, z0: 0.6, z1: 10.4 };

// A hand reaching into our space from W. In its own page you see the palm and finger stumps;
// one page "ahead" (+W) only the five fingertips cross, as separate blobs; behind it (-W) is clear.
export class Creature {
  constructor(scene) {
    this.x = 3.6;
    this.z = 8.6;
    this.y = 1.2;
    this.w = 1.0;
    this.yaw = Math.PI / 2;
    this.curl = 0.6;
    this.fingerScale = 1;
    this.t = 0;
    this.state = 'dormant';
    this.attention = 0;
    this.timer = 0;
    this.target = { x: this.x, z: this.z, w: this.w };
    this.last = null;
    this.balls = Array.from({ length: 25 }, () => ({ x: 0, y: 0, z: 0, w: 0, r: 0 }));
    const mat = creatureMaterial();
    this.slicers = [new Slicer(), new Slicer()];
    this.meshes = this.slicers.map((s) => {
      const m = new THREE.Mesh(s.geo, mat);
      m.frustumCulled = false;
      scene.add(m);
      return m;
    });
    this.pose();
  }

  pose() {
    const t = this.t;
    const b = this.balls;
    const fx = Math.sin(this.yaw);
    const fz = Math.cos(this.yaw);
    const set = (o, x, y, z, w, r) => {
      o.x = x; o.y = y; o.z = z; o.w = w; o.r = r;
    };
    set(b[0], this.x, this.y, this.z, this.w, 0.47 + 0.03 * Math.sin(t * 2.1));
    for (let i = 1; i <= 4; i++) {
      set(b[i], this.x - fx * 0.22 * i, this.y + 0.3 * i, this.z - fz * 0.22 * i, this.w - 0.15 * i, 0.4 - 0.03 * i);
    }
    const spread = [-0.85, -0.35, -0.05, 0.25, 0.55];
    for (let k = 0; k < 5; k++) {
      const a = this.yaw + spread[k] + 0.08 * Math.sin(t * 0.6 + k);
      const hx = Math.sin(a);
      const hz = Math.cos(a);
      let px = this.x + hx * 0.32;
      let py = this.y - 0.05;
      let pz = this.z + hz * 0.32;
      const reachW = 0.95 + 0.12 * Math.sin(t * 0.9 + k * 1.7);
      for (let j = 1; j <= 4; j++) {
        const pitch = -0.15 + this.curl * 0.35 * j + 0.12 * Math.sin(t * 1.3 + k + j);
        const seg = 0.24;
        px += hx * Math.cos(pitch) * seg;
        pz += hz * Math.cos(pitch) * seg;
        py -= Math.sin(pitch) * seg;
        const r = [0.19, 0.17, 0.16, 0.15][j - 1] * (k === 0 ? 1.1 : 1) * this.fingerScale;
        set(b[5 + k * 4 + j - 1], px, py, pz, this.w + reachW * (j / 4) ** 1.6, r);
      }
    }
  }

  // Signed field value at a 4D point; > 0 means inside the creature.
  field(x, y, z, w) {
    let f = -ISO;
    for (const b of this.balls) {
      const dw = wrapW(b.w - w);
      const q = ((x - b.x) ** 2 + (y - b.y) ** 2 + (z - b.z) ** 2 + dw * dw) / (b.r * b.r * R2K);
      if (q < 1) f += (1 - q) ** 3;
    }
    return f;
  }

  touches(x, z, w) {
    return this.field(x, 1.0, z, w) > -0.03 || this.field(x, 1.5, z, w) > -0.03;
  }

  // 0..1 closeness in 4D, used for sound, flicker and screen noise.
  proximity(x, y, z, w) {
    let d = Infinity;
    for (const b of this.balls) {
      const dw = wrapW(b.w - w);
      d = Math.min(d, Math.hypot(x - b.x, y - b.y, z - b.z, dw) - b.r);
    }
    return Math.max(0, Math.min(1, 1 - d / 5));
  }

  // 'eye' when it looks into that page, 'faint' when only part of it crosses, '' otherwise.
  presence(layer) {
    if (this.state === 'dormant') return '';
    if (Math.abs(wrapW(this.w - layer)) < 0.6) return 'eye';
    for (const b of this.balls) if (Math.abs(wrapW(b.w - layer)) < b.r) return 'faint';
    return '';
  }

  wake() {
    if (this.state !== 'dormant') return;
    this.state = 'wander';
    this.attention = 0;
    this.timer = 0;
  }

  // Scripted scenes place the hand directly and drive its w themselves. Close to the camera
  // the full-size fingertips fill the screen, so scripts shrink them.
  beginScript(x, y, z, yaw, w, focus) {
    if (this.state !== 'script') this.prevState = this.state;
    this.state = 'script';
    this.focus = focus;
    this.x = x;
    this.y = y;
    this.z = z;
    this.yaw = yaw;
    this.w = w;
    this.curl = 0.1;
    this.fingerScale = 0.8;
  }

  endScript(playerLayer) {
    this.fingerScale = 1;
    if (this.prevState === 'dormant') {
      this.state = 'dormant';
      return;
    }
    this.state = 'wander';
    this.attention = 0;
    this.w = modW(playerLayer + 1.5);
    this.timer = 0;
  }

  notice() {
    this.attention = 1;
    this.state = 'hunt';
  }

  retreat(playerLayer) {
    this.state = 'retreat';
    this.timer = 7;
    this.attention = 0;
    this.target.w = modW(playerLayer - 1.5);
  }

  moveTo(tx, tz, tw, speed, wSpeed, dt) {
    const dx = tx - this.x;
    const dz = tz - this.z;
    const d = Math.hypot(dx, dz);
    if (d > 0.05) {
      const s = Math.min(d, speed * dt);
      this.x += (dx / d) * s;
      this.z += (dz / d) * s;
      const want = Math.atan2(dx, dz);
      this.yaw += wrapAngle(want - this.yaw) * Math.min(1, dt * 2);
    }
    const dw = wrapW(tw - this.w);
    this.w = modW(this.w + Math.sign(dw) * Math.min(Math.abs(dw), wSpeed * dt));
    this.x = Math.max(BOUNDS.x0, Math.min(BOUNDS.x1, this.x));
    this.z = Math.max(BOUNDS.z0, Math.min(BOUNDS.z1, this.z));
    return d;
  }

  pickTarget(p) {
    if (Math.random() < 0.5) {
      const a = Math.random() * Math.PI * 2;
      const r = 2.5 + Math.random() * 4.5;
      this.target.x = p.x + Math.cos(a) * r;
      this.target.z = p.z + Math.sin(a) * r;
    } else {
      this.target.x = BOUNDS.x0 + Math.random() * (BOUNDS.x1 - BOUNDS.x0);
      this.target.z = BOUNDS.z0 + Math.random() * (BOUNDS.z1 - BOUNDS.z0);
    }
    this.target.w = Math.random() < 0.35 ? modW(p.layer + (Math.random() - 0.5) * 0.8) : Math.random() * PERIOD;
    this.timer = 7 + Math.random() * 6;
  }

  // p: { x, z, layer, moving, running, peeking, peekLayer, camPos, camDir }
  // Returns 'hunt' on the frame it starts hunting, so the caller can play a sting.
  update(dt, p) {
    this.t += dt;
    let event = null;
    if (this.state === 'dormant' || this.state === 'script') {
      this.pose();
      return event;
    }

    // It sees everything in its own page, walls and closed doors included.
    const dist = Math.hypot(p.x - this.x, p.z - this.z);
    const sees = Math.abs(wrapW(this.w - p.layer)) < 0.6 && dist < 9;
    let gain = 0;
    if (sees) {
      const near = 1 - dist / 9;
      gain += (0.025 + 0.25 * near * near) * (p.running ? 1.8 : p.moving ? 1 : 0.5);
    }
    if (p.peeking && Math.abs(wrapW(this.w - p.peekLayer)) < 0.9) {
      const v = new THREE.Vector3(this.x - p.camPos.x, this.y - p.camPos.y, this.z - p.camPos.z);
      const d = v.length();
      if (d < 12 && v.normalize().dot(p.camDir) > 0.85) gain += 0.6;
    }
    this.attention = Math.max(0, Math.min(1, this.attention + gain * dt - (sees ? 0 : 0.07 * dt)));

    if (this.state === 'retreat') {
      this.timer -= dt;
      this.moveTo(this.x, this.z, this.target.w, 0, 0.5, dt);
      this.curl = lerp(this.curl, 0.9, dt);
      if (this.timer <= 0) {
        this.state = 'wander';
        this.attention = 0;
        this.pickTarget(p);
      }
    } else if (this.state === 'wander') {
      this.timer -= dt;
      const d = this.moveTo(this.target.x, this.target.z, this.target.w, 0.55, 0.08, dt);
      if (this.timer <= 0 || d < 0.3) this.pickTarget(p);
      this.curl = lerp(this.curl, 0.6 + 0.2 * Math.sin(this.t * 0.5), dt);
      if (this.attention >= 1) {
        this.state = 'hunt';
        event = 'hunt';
      }
    } else if (this.state === 'hunt') {
      if (sees) this.last = { x: p.x, z: p.z, w: p.layer };
      if (this.last) this.moveTo(this.last.x, this.last.z, this.last.w, 1.2, 0.3, dt);
      this.curl = lerp(this.curl, 0.12, dt * 2);
      if (this.attention < 0.3) {
        this.state = 'wander';
        this.pickTarget(p);
      }
    }
    this.y = 1.2 + 0.1 * Math.sin(this.t * 0.7);
    this.pose();
    return event;
  }

  slice(i, w) {
    if (this.state === 'dormant') {
      this.slicers[i].geo.setDrawRange(0, 0);
      return;
    }
    // Scripted close-ups: a finer grid around the point the script is staging.
    if (this.state === 'script') this.slicers[i].slice(this.balls, w, this.focus.x, this.focus.y, this.focus.z, 0.08);
    else this.slicers[i].slice(this.balls, w, this.x, this.y + 0.2, this.z, 0.16);
  }
}

const lerp = (a, b, t) => a + (b - a) * Math.min(1, t);
const wrapAngle = (a) => Math.atan2(Math.sin(a), Math.cos(a));
