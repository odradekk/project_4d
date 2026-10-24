import * as THREE from 'three';
import { Slicer } from '../creature.js';
import { creatureMaterial } from '../ps1.js';

const TEETH = 12;
const LIPS = 12;
const FANGS = 16;
const SPINE = 7;
const FEELERS = 4;

// A grazer from the tank. It feeds on the thin film our world is made of.
// Its mouth faces along `yaw`; its body trails away from our page into +W, so as it comes
// closer in W the loose teeth cross our page first, then the rim of the mouth, then the body.
export class Grazer {
  constructor(scene) {
    this.x = 0;
    this.y = 1.5;
    this.z = 0;
    this.w = 0;
    this.yaw = 0;
    this.t = 0;
    this.spin = 0;
    this.open = 0; // 0..1: the mouth widening at the very end
    this.active = false;
    this.balls = Array.from({ length: TEETH + LIPS + FANGS + SPINE + FEELERS * 3 }, () => ({ x: 0, y: 0, z: 0, w: 0, r: 0 }));
    // Pale and faintly lit from within, so it reads against a room that is going dark.
    const mat = creatureMaterial([0.82, 0.76, 0.84], 0.3);
    this.material = mat;
    this.slicers = [new Slicer({ periodic: false }), new Slicer({ periodic: false })];
    this.meshes = this.slicers.map((s) => {
      const m = new THREE.Mesh(s.geo, mat);
      m.frustumCulled = false;
      m.visible = false;
      scene.add(m);
      return m;
    });
  }

  get forward() {
    return [Math.sin(this.yaw), Math.cos(this.yaw)];
  }

  mouth() {
    const [fx, fz] = this.forward;
    return new THREE.Vector3(this.x + fx * 0.25, this.y, this.z + fz * 0.25);
  }

  pose() {
    const t = this.t;
    const [fx, fz] = this.forward;
    const sx = fz;
    const sz = -fx;
    let k = 0;
    const set = (x, y, z, w, r) => {
      const b = this.balls[k++];
      b.x = x; b.y = y; b.z = z; b.w = w; b.r = r;
    };
    // Teeth turn in the (up, W) plane: in our page they come and go as small separate lumps.
    for (let i = 0; i < TEETH; i++) {
      const a = (i / TEETH) * Math.PI * 2 + this.spin;
      set(this.x + fx * 0.15, this.y + Math.cos(a) * 0.75, this.z + fz * 0.15, this.w + Math.sin(a) * 0.75, 0.2);
    }
    // The rim of the mouth lies in our three directions and breathes slightly in W.
    const rim = 1.0 + 0.4 * this.open;
    for (let i = 0; i < LIPS; i++) {
      const a = (i / LIPS) * Math.PI * 2;
      const rr = rim + Math.sin(t * 1.3 + i) * 0.05;
      set(
        this.x + sx * Math.cos(a) * rr,
        this.y + Math.sin(a) * rr,
        this.z + sz * Math.cos(a) * rr,
        this.w + 0.05 * Math.sin(3 * a + t),
        0.27,
      );
    }
    // A ring of small hooks just inside the rim, angled back into the throat.
    for (let i = 0; i < FANGS; i++) {
      const a = (i / FANGS) * Math.PI * 2 + 0.2;
      const rr = rim * 0.68;
      set(
        this.x - fx * 0.12 + sx * Math.cos(a) * rr,
        this.y + Math.sin(a) * rr,
        this.z - fz * 0.12 + sz * Math.cos(a) * rr,
        this.w + 0.03 * Math.sin(5 * a - t * 2),
        0.1,
      );
    }
    for (let j = 1; j <= SPINE; j++) {
      set(
        this.x - fx * (0.4 + 0.5 * j),
        this.y + 0.12 * Math.sin(t * 0.7 + j),
        this.z - fz * (0.4 + 0.5 * j),
        this.w + 0.12 * j,
        0.85 - 0.04 * j + 0.05 * Math.sin(t * 1.1 + j),
      );
    }
    // Feelers reach ahead, swinging through W.
    for (let f = 0; f < FEELERS; f++) {
      const side = f % 2 ? 1 : -1;
      const up = f < 2 ? 0.6 : -0.5;
      for (let j = 1; j <= 3; j++) {
        const sway = Math.sin(t * 1.6 + f * 1.3 + j * 0.5);
        set(
          this.x + fx * (0.3 + 0.35 * j) + sx * side * (0.9 + 0.15 * j),
          this.y + up + 0.15 * sway,
          this.z + fz * (0.3 + 0.35 * j) + sz * side * (0.9 + 0.15 * j),
          this.w - 0.35 * j * (0.6 + 0.4 * sway),
          0.12,
        );
      }
    }
  }

  update(dt) {
    this.t += dt;
    this.spin += dt * (0.9 + 3 * this.open);
    this.pose();
    const [fx, fz] = this.forward;
    // Measure the throat from a plane just in front of the lips.
    this.material.uniforms.uThroat.value.set(this.x + fx * 0.4, this.y, this.z + fz * 0.4, 1);
    this.material.uniforms.uThroatDir.value.set(fx, 0, fz);
  }

  slice(i, w) {
    if (!this.active) {
      this.slicers[i].geo.setDrawRange(0, 0);
      return;
    }
    const [fx, fz] = this.forward;
    this.slicers[i].slice(this.balls, w, this.x - fx * 1.2, this.y, this.z - fz * 1.2, 0.22);
  }

  // Closeness in 4D, 0..1, for sound.
  proximity(x, y, z, w) {
    let d = Infinity;
    for (const b of this.balls) d = Math.min(d, Math.hypot(x - b.x, y - b.y, z - b.z, b.w - w) - b.r);
    return Math.max(0, Math.min(1, 1 - d / 7));
  }
}
