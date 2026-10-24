import * as THREE from 'three';
import { W_POS, SUPPORTS } from './world.js';
import {
  EPS, Pose, unit, dot4, boxSlice, ringSlice, restingY, ballRestY, partSolid, ballSolid, solidsOverlap,
} from './four.js';

// How deep two solids may press into each other and still count as only touching.
const TOUCH = 0.005;

// Parts are in local coordinates with the origin at the centre of the base and +z as the front.
// Boxes: [x0, y0, z0, x1, y1, z1]. Planes: centre, size and facing; they carry the printed faces.
// In 4D each of them is also EPS thick either side in W, like everything people make.
const STOOL = {
  name: '凳子',
  small: false,
  radius: 0.2,
  hold: [0.18, -0.55, -0.75],
  boxes: [
    ['stoolwood', -0.18, 0.41, -0.18, 0.18, 0.45, 0.18],
    ['stoolwood', -0.165, 0, -0.165, -0.12, 0.41, -0.12],
    ['stoolwood', 0.12, 0, -0.165, 0.165, 0.41, -0.12],
    ['stoolwood', -0.165, 0, 0.12, -0.12, 0.41, 0.165],
    ['stoolwood', 0.12, 0, 0.12, 0.165, 0.41, 0.165],
    ['stoolwood', -0.12, 0.16, -0.155, 0.12, 0.19, -0.13],
    ['stoolwood', -0.12, 0.16, 0.13, 0.12, 0.19, 0.155],
  ],
  planes: [],
};

const CLOCK = {
  name: '闹钟',
  small: true,
  hold: [0.16, -0.24, -0.42],
  boxes: [
    ['clockred', -0.065, 0.02, -0.03, 0.065, 0.15, 0.03],
    ['clockred', -0.06, 0.15, -0.015, -0.02, 0.18, 0.015],
    ['clockred', 0.02, 0.15, -0.015, 0.06, 0.18, 0.015],
    ['clockhand', -0.05, 0, -0.01, -0.03, 0.02, 0.01],
    ['clockhand', 0.03, 0, -0.01, 0.05, 0.02, 0.01],
  ],
  planes: [{ tex: 'clockface', c: [0, 0.085, 0.031], size: [0.11, 0.11], facing: '+z' }],
  hands: [0, 0.085, 0.034],
};

const PENCILBOX = {
  name: '文具盒',
  small: true,
  hold: [0.16, -0.24, -0.42],
  boxes: [['tinblue', -0.105, 0, -0.038, 0.105, 0.032, 0.038]],
  planes: [{ tex: 'pencillid', c: [0, 0.033, 0], size: [0.21, 0.076], facing: '+y' }],
};

const RABBIT = {
  name: '布兔子',
  small: true,
  hold: [0.16, -0.3, -0.45],
  boxes: [
    ['pinkcloth', -0.07, 0, -0.05, 0.07, 0.13, 0.05],
    ['pinkcloth', -0.06, 0.13, -0.05, 0.06, 0.23, 0.05],
    ['pinkcloth', -0.045, 0.23, -0.015, -0.015, 0.36, 0.015],
    ['pinkcloth', 0.015, 0.23, -0.015, 0.045, 0.36, 0.015],
  ],
  planes: [{ tex: 'rabbitface', c: [0, 0.18, 0.051], size: [0.11, 0.09], facing: '+z' }],
};

// Her home-made cat wand: a bamboo stick with a ball of yarn tied near the tip. Turned into W from
// the handle, its far end reaches exactly as deep as page Ⅱ, so there it is only a slice of yarn.
const WAND = {
  name: '逗猫棒',
  small: true,
  hold: [0.14, -0.26, -0.38],
  boxes: [
    ['bamboo', 0, 0.025, -0.008, 0.42, 0.041, 0.008],
    ['yarn', 0.37, 0, -0.055, 0.48, 0.11, 0.055],
  ],
  planes: [{ tex: 'wandtag', c: [0.08, 0.042, 0], size: [0.09, 0.04], facing: '+y' }],
};

// A rusty nut she picked up by the railway with the prism. Here it is just a nut, but it has another
// part out in W: a ring lying between pages Ⅰ and Ⅱ (0.2 out in W), 0.42 along the nut's length
// from its hinge. No page cuts the ring until the nut is stood in W; then the quarter turn carries
// it round into page Ⅱ.
const NUT = {
  name: '螺母',
  small: true,
  hold: [0.14, -0.24, -0.38],
  boxes: [['rusty', -0.03, 0, -0.026, 0.03, 0.03, 0.026]],
  planes: [{ tex: 'nuttop', c: [0, 0.031, 0], size: [0.06, 0.052], facing: '+y' }],
  hidden: { radius: 0.5, tube: 0.05, x: -0.03 + EPS + 0.42, w: 0.2 },
};

export const PROP_SPAWNS = [
  { id: 'wand', spec: WAND, section: 0, x: 5.25, z: 2.66, y: 1.03, yaw: 0 },
  { id: 'nut', spec: NUT, section: 0, x: 6.49, z: 3.28, y: 0.76, yaw: 0 },
  { id: 'stool', spec: STOOL, section: 0, x: 6.12, z: 3.27, y: 0, yaw: 0 },
  { id: 'clock', spec: CLOCK, section: 0, x: 6.74, z: 3.14, y: 0.76, yaw: -Math.PI / 2 },
  { id: 'pencilbox', spec: PENCILBOX, section: 0, x: 6.6, z: 3.74, y: 0.76, yaw: Math.PI / 2 },
  { id: 'rabbit', spec: RABBIT, section: 0, x: 3.5, z: 3.4, y: 0.5, yaw: Math.PI / 2 },
];

function withColor(g) {
  g.setAttribute('color', new THREE.Float32BufferAttribute(new Float32Array(g.attributes.position.count * 3), 3));
  return g;
}

function boxGeometry([x0, y0, z0, x1, y1, z1]) {
  const g = new THREE.BoxGeometry(x1 - x0, y1 - y0, z1 - z0);
  g.translate((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2);
  return withColor(g);
}

function planeGeometry({ c, size, facing }) {
  const g = new THREE.PlaneGeometry(size[0], size[1]);
  if (facing === '+y') g.rotateX(-Math.PI / 2);
  g.translate(...c);
  return withColor(g);
}

function setFlat(group, v) {
  group.traverse((m) => {
    if (!m.isMesh) return;
    const col = m.geometry.attributes.color;
    for (let i = 0; i < col.count; i++) col.setXYZ(i, v, v, v);
    col.needsUpdate = true;
  });
}

function disposeGroup(g) {
  g.removeFromParent();
  g.traverse((m) => m.isMesh && m.geometry.dispose());
}

const tmpP = new THREE.Vector3();
const tmpN = new THREE.Vector3();
const tmpM = new THREE.Matrix3();

// Re-light every vertex from the page's lamps wherever the object now sits.
export function bakeGroup(group, layer) {
  group.updateMatrixWorld(true);
  group.traverse((m) => {
    if (!m.isMesh) return;
    tmpM.getNormalMatrix(m.matrixWorld);
    const { position, normal, color } = m.geometry.attributes;
    for (let i = 0; i < position.count; i++) {
      tmpP.fromBufferAttribute(position, i).applyMatrix4(m.matrixWorld);
      tmpN.fromBufferAttribute(normal, i).applyMatrix3(tmpM).normalize();
      const c = layer.bake([tmpP.x, tmpP.y, tmpP.z], [tmpN.x, tmpN.y, tmpN.z]);
      color.setXYZ(i, c[0] + 0.04, c[1] + 0.04, c[2] + 0.04);
    }
    color.needsUpdate = true;
  });
}

// How far a turn has got at time t (0..1). An object is only 2·EPS thick in W, so once it starts to
// lean out of its page, the page keeps only the part within EPS·cot(angle) of the turning plane:
// it is gone within the first few degrees. Those degrees are drawn out so the part left in the page
// visibly shrinks from the object's full `reach` down to `near`; the rest of the turn runs evenly.
// Every frame is still the exact slice at its angle. `pace` says which end of the turn lies flat in
// a page: 'start', 'end' or 'both'.
function paced(t, angle, pace, reach, near) {
  if (t >= 1) return angle;
  const span = Math.abs(angle);
  const lean = (s) => Math.atan(EPS / (near + (reach * 1.05 - near) * (1 - s)));
  const ease = (s) => s * s * (3 - 2 * s);
  const into = (s) => (s < 0.4 ? lean(s / 0.4) : lean(1) + (span - lean(1)) * ease((s - 0.4) / 0.6));
  let a;
  if (pace === 'start') a = into(t);
  else if (pace === 'end') a = span - into(1 - t);
  else if (t < 0.3) a = lean(t / 0.3);
  else if (t > 0.7) a = span - lean((1 - t) / 0.3);
  else a = lean(1) + (span - 2 * lean(1)) * ease((t - 0.3) / 0.4);
  return Math.sign(angle) * a;
}

// A thing from her room as a 4D object: a 3D shape EPS thick in W, with a 4D pose. Lying flat in its
// page it is drawn as itself; leaning into W, each page draws its exact slice.
export class Prop {
  constructor({ id, spec, section, x, z, y, yaw }, material, sliceMaterial, layers) {
    this.id = id;
    this.spec = spec;
    this.section = section; // the page it lies in, or stands into W from
    this.layers = layers;
    this.state = 'normal'; // 'normal' | 'held' | 'standing'
    this.mirrored = false;
    this.slices = [];
    this.anim = null;
    this.sliceMaterial = sliceMaterial;
    this.group = new THREE.Group();
    const add = (parent, geometry, tex) => {
      const m = new THREE.Mesh(geometry, material(tex));
      m.userData.tex = tex;
      parent.add(m);
      return m;
    };
    for (const [tex, ...b] of spec.boxes) add(this.group, boxGeometry(b), tex);
    for (const p of spec.planes) add(this.group, planeGeometry(p), p.tex);
    if (spec.hands) {
      this.hands = new THREE.Group();
      this.hands.position.set(...spec.hands);
      this.handMeshes = [
        ['clockhand', 0.004, 0.03],
        ['clockhand', 0.003, 0.043],
        ['clocksec', 0.0015, 0.046],
      ].map(([tex, w, len]) => add(this.hands, boxGeometry([-w, 0, 0, w, len, 0.002]), tex));
      this.group.add(this.hands);
    }
    // The longest side is the one that turns into W; it pivots about its lower end.
    const lo = [Infinity, Infinity, Infinity];
    const hi = [-Infinity, -Infinity, -Infinity];
    for (const [, ...b] of spec.boxes) {
      for (let k = 0; k < 3; k++) {
        lo[k] = Math.min(lo[k], b[k]);
        hi[k] = Math.max(hi[k], b[k + 3]);
      }
    }
    const dims = hi.map((h, k) => h - lo[k]);
    this.axis = dims.indexOf(Math.max(...dims));
    this.axisMin = lo[this.axis];
    this.length = dims[this.axis];
    this.height = hi[1];
    this.pose = Pose.upright(x, y, z, W_POS[section], yaw, false);
    this.show();
  }

  get name() {
    return this.spec.name;
  }

  get x() {
    return this.pose.P[0];
  }

  get z() {
    return this.pose.P[2];
  }

  get position() {
    return new THREE.Vector3(this.pose.P[0], this.pose.P[1], this.pose.P[2]);
  }

  center() {
    return this.position.add(new THREE.Vector3(0, this.height / 2, 0));
  }

  // Clock hands follow the in-game time; mirrored, they visibly run backwards.
  setTime(hours) {
    if (!this.hands) return;
    const [h, m, s] = this.handMeshes;
    h.rotation.z = -((hours % 12) / 12) * Math.PI * 2;
    m.rotation.z = -((hours * 60) % 60) / 60 * Math.PI * 2;
    s.rotation.z = -Math.floor((hours * 3600) % 60) / 60 * Math.PI * 2;
  }

  // The object as box parts in its own frame; printed faces count as very thin boxes.
  parts() {
    const out = [];
    this.group.traverse((m) => {
      if (!m.isMesh || !m.userData.tex) return;
      const frame = new THREE.Matrix4();
      for (let o = m; o !== this.group; o = o.parent) {
        o.updateMatrix();
        frame.premultiply(o.matrix);
      }
      m.geometry.computeBoundingBox();
      const b0 = m.geometry.boundingBox.min.clone();
      const b1 = m.geometry.boundingBox.max.clone();
      for (let k = 0; k < 3; k++) {
        if (b1.getComponent(k) - b0.getComponent(k) >= 0.002) continue;
        b0.setComponent(k, b0.getComponent(k) - 0.001);
        b1.setComponent(k, b1.getComponent(k) + 0.001);
      }
      out.push({ tex: m.userData.tex, b0, b1, frame });
    });
    return out;
  }

  // Its corners in 4D at a pose, and the lowest point of its hidden ring.
  corners(pose = this.pose, withHidden = true) {
    const pts = [];
    for (const { b0, b1, frame } of this.parts()) {
      for (let i = 0; i < 16; i++) {
        const q = new THREE.Vector3(i & 1 ? b1.x : b0.x, i & 2 ? b1.y : b0.y, i & 4 ? b1.z : b0.z).applyMatrix4(frame);
        pts.push(pose.apply([q.x, q.y, q.z, i & 8 ? EPS : -EPS]));
      }
    }
    const r = this.spec.hidden;
    if (r && withHidden) pts.push(pose.apply([r.x, 0, 0, r.w]));
    return pts;
  }

  // The hinge sits EPS in from the end of the long side, so once stood up its own page cuts through
  // solid stuff rather than exactly along its end face.
  hingeAt(pose = this.pose) {
    const v = [0, 0, 0, 0];
    v[this.axis] = this.axisMin + EPS;
    return pose.apply(v);
  }

  // Draw it as it is now. Flat in its page it is simply itself; otherwise every page shows its exact
  // slice. The hidden ring, if any, shows in whichever pages cut it.
  show() {
    this.clearSlices();
    if (this.state === 'held') return;
    const { P, M } = this.pose;
    const flat = this.pose.flat && this.anim?.kind !== 'turn';
    if (flat) {
      const L = this.layers[this.section];
      L.group.add(this.group);
      this.group.matrixAutoUpdate = false;
      this.group.matrix.set(
        M[0][0], M[1][0], M[2][0], P[0],
        M[0][1], M[1][1], M[2][1], P[1],
        M[0][2], M[1][2], M[2][2], P[2],
        0, 0, 0, 1,
      );
      this.group.matrixWorldNeedsUpdate = true;
      this.group.visible = true;
      bakeGroup(this.group, L);
    } else {
      this.group.visible = false;
    }
    const parts = flat ? [] : this.parts();
    W_POS.forEach((c, s) => {
      const g = this.sliceGroup(this.pose, parts, c);
      if (!g) return;
      g.position.set(P[0], P[1], P[2]);
      this.layers[s].group.add(g);
      bakeGroup(g, this.layers[s]);
      this.slices.push({ section: s, group: g, local: g.userData.center });
    });
  }

  sliceGroup(pose, parts, c) {
    const g = new THREE.Group();
    const centers = [];
    for (const part of parts) {
      const cut = boxSlice(pose, part, c);
      if (!cut) continue;
      g.add(new THREE.Mesh(cut.geometry, this.sliceMaterial(part.tex)));
      centers.push(cut.center);
    }
    const ring = this.spec.hidden && ringSlice(pose, this.spec.hidden, c);
    if (ring) {
      g.add(new THREE.Mesh(ring.geometry, this.sliceMaterial('ringskin')));
      centers.push(ring.center);
    }
    if (!centers.length) return null;
    g.userData.center = centers.reduce((a, v) => a.add(v), new THREE.Vector3()).divideScalar(centers.length);
    return g;
  }

  clearSlices() {
    for (const s of this.slices) disposeGroup(s.group);
    this.slices = [];
  }

  sliverIn(section) {
    return this.slices.find((s) => s.section === section);
  }

  sliverCenter(s) {
    return this.position.add(s.local);
  }

  // Moving it moves every slice of it at once, in every page.
  moveTo(v) {
    const d = [v.x - this.pose.P[0], v.y - this.pose.P[1], v.z - this.pose.P[2]];
    const A = this.anim;
    if (A?.kind === 'turn' && !A.inHand) for (const p of [A.from.P, A.pivot]) d.forEach((x, i) => (p[i] += x));
    d.forEach((x, i) => (this.pose.P[i] += x));
    if (!this.group.matrixAutoUpdate) {
      this.group.matrix.setPosition(v);
      this.group.matrixWorldNeedsUpdate = true;
    }
    for (const s of this.slices) s.group.position.copy(v);
  }

  // Come to rest on whatever is under it, in any page its body reaches.
  settle() {
    if (this.anim) {
      this.anim.settle = true;
      return;
    }
    this.pose.P[1] = restingY(this.pose, this.corners(), SUPPORTS);
    this.show();
  }

  hold(camera) {
    this.state = 'held';
    this.clearSlices();
    camera.add(this.group);
    this.group.matrixAutoUpdate = true;
    this.group.position.set(...this.spec.hold);
    this.group.rotation.set(0.25, -0.5, 0);
    this.group.scale.set(this.mirrored ? -1 : 1, 1, 1);
    this.group.visible = true;
    setFlat(this.group, 0.55);
  }

  drop(section, x, z, yaw) {
    this.finishHandTurn();
    this.state = 'normal';
    this.section = section;
    this.pose = Pose.upright(x, 0, z, W_POS[section], yaw, this.mirrored);
    this.settle();
  }

  // Lying in page `section` with its hinge in her hand, ready to be stood in W from there.
  heldPose(section, hand, yaw) {
    const pose = Pose.upright(hand.x, hand.y, hand.z, W_POS[section], yaw, this.mirrored);
    const h = this.hingeAt(pose);
    for (let i = 0; i < 3; i++) pose.P[i] += pose.P[i] - h[i];
    return pose;
  }

  // Turn into W straight from her hand; the hinge stays where her hand is.
  standFromHand(section, position, yaw) {
    this.finishHandTurn();
    this.state = 'normal';
    this.section = section;
    this.pose = this.heldPose(section, position, yaw);
    this.standUp(false);
  }

  // The turns it can make from a pose, each as a plane (a, b), an angle and a pivot.
  // Standing it in W: a quarter turn about the hinge, the long side turning from lying along the
  // page to pointing into +W. Laying it down turns it back.
  standTurn(pose) {
    return { a: pose.M[this.axis].slice(), b: unit(3), angle: Math.PI / 2, pivot: this.hingeAt(pose) };
  }

  layTurn(pose) {
    return { a: this.standDir, b: unit(3), angle: -Math.PI / 2, pivot: this.hingeAt(pose) };
  }

  // Flipping in place: a half turn through W about its own middle plane.
  flipTurn(pose) {
    return { a: pose.M[0].slice(), b: unit(3), angle: Math.PI, pivot: [...pose.P] };
  }

  // Flipping round a vertical plane between her and the object.
  acrossTurn(pose, plane) {
    const k = plane.axis === 'z' ? 2 : 0;
    const pivot = [...pose.P];
    pivot[k] = plane.at;
    const side = Math.sign(pose.P[k] - plane.at) || 1;
    return { a: unit(k).map((v) => v * side), b: unit(3), angle: Math.PI, pivot };
  }

  standUp(rest = true) {
    this.state = 'standing';
    this.standDir = this.pose.M[this.axis].slice();
    this.turn(this.standTurn(this.pose), 'start', 1.4, rest);
  }

  layDown() {
    this.state = 'normal';
    this.turn(this.layTurn(this.pose), 'end', 1.0, true);
  }

  // The pose it would end in if stood in W from `pose` and left to rest.
  stoodFrom(pose) {
    const { a, b, angle, pivot } = this.standTurn(pose);
    const stood = pose.turned(a, b, angle, pivot);
    stood.P[1] = restingY(stood, this.corners(stood), SUPPORTS);
    return stood;
  }

  // Its body at a pose as solids, for collisions: a box per part, and a chain of small balls, close
  // enough to touch, along the middle of the hidden ring.
  bodyAt(pose, parts = this.parts()) {
    const out = parts.map((part) => partSolid(pose, part));
    const r = this.spec.hidden;
    if (r) {
      for (let i = 0; i < 32; i++) {
        const ph = (i / 32) * Math.PI * 2;
        const ball = ballSolid(pose.apply([r.x, r.radius + r.tube + r.radius * Math.cos(ph), r.radius * Math.sin(ph), r.w]), r.tube);
        ball.ring = true;
        out.push(ball);
      }
    }
    return out;
  }

  // Whether turning from `pose` would run into any of `solids`: the first blocked step, the pose
  // there and the piece of it that hits; null if the way is clear. The steps are small enough that
  // nothing a centimetre thin slips between two of them. With `ignoreStart`, whatever it already
  // touches at the start doesn't count, such as what it stands on.
  sweepHit(pose, turn, solids, ignoreStart) {
    const parts = this.parts();
    const start = this.bodyAt(pose, parts);
    const reach = Math.max(...start.map((e) => Math.hypot(...e.c.map((v, i) => v - turn.pivot[i])) + (e.r ?? Math.hypot(...e.E.flat()))));
    // A turn about the pivot never leaves the 4D ball of that reach round it.
    const near = solids.filter((s) => s.lo.every((v, i) => v < turn.pivot[i] + reach) && s.hi.every((v, i) => v > turn.pivot[i] - reach));
    const live = ignoreStart ? near.filter((s) => !start.some((e) => solidsOverlap(e, s, TOUCH))) : near;
    const steps = Math.max(8, Math.ceil((Math.abs(turn.angle) * reach) / 0.01));
    for (let k = ignoreStart ? 1 : 0; k <= steps; k++) {
      const p = pose.turned(turn.a, turn.b, (turn.angle * k) / steps, turn.pivot);
      for (const piece of this.bodyAt(p, parts)) {
        const solid = live.find((s) => solidsOverlap(piece, s, TOUCH));
        if (solid) return { step: k, steps, pose: p, piece, solid };
      }
    }
    return null;
  }

  // The outline of its slice by the page w = c at a pose, as pairs of world-space points: the edges
  // of each part's slice and the middle line of the hidden ring.
  outlineAt(pose, c, parts = this.parts()) {
    const P = new THREE.Vector3(pose.P[0], pose.P[1], pose.P[2]);
    const pts = [];
    for (const part of parts) {
      const cut = boxSlice(pose, part, c);
      if (!cut) continue;
      const edges = new THREE.EdgesGeometry(cut.geometry, 20);
      const a = edges.attributes.position.array;
      for (let i = 0; i < a.length; i += 3) pts.push(new THREE.Vector3(a[i], a[i + 1], a[i + 2]).add(P));
      edges.dispose();
      cut.geometry.dispose();
    }
    const ring = this.spec.hidden && ringSlice(pose, this.spec.hidden, c);
    if (ring) {
      for (const v of ring.path) pts.push(v.clone().add(P));
      ring.geometry.dispose();
    }
    return pts;
  }

  // Where page `section` cuts the hidden ring at a pose: its centre and the way its opening faces.
  ringIn(section, pose = this.pose) {
    const r = this.spec.hidden && ringSlice(pose, this.spec.hidden, W_POS[section]);
    if (!r) return null;
    r.geometry.dispose();
    return { section, center: r.center.add(new THREE.Vector3(pose.P[0], pose.P[1], pose.P[2])), axis: r.axis };
  }

  // A half turn through W about its own middle plane: it comes back as its mirror image.
  flipMirror() {
    if (this.state === 'held') return this.turnInHand();
    this.turn(this.flipTurn(this.pose), 'both', 1.2, true, () => {
      this.mirrored = !this.mirrored;
    });
  }

  // A half turn through W about a vertical plane between her and the object: it swings out into W
  // and comes back round on her side as its mirror image, then drops onto whatever is below.
  flipAcross(plane) {
    this.turn(this.acrossTurn(this.pose, plane), 'both', 1.4, false, () => {
      this.mirrored = !this.mirrored;
      this.anim = { kind: 'fall', y0: this.pose.P[1], y1: restingY(this.pose, this.corners(), SUPPORTS), t: 0, dur: 0.35 };
      this.show();
    });
  }

  // Flipped in her hand: the same half turn, sliced exactly, in the hand's own frame.
  turnInHand() {
    this.group.scale.set(1, 1, 1);
    for (const c of this.group.children) c.visible = false;
    this.handSlice = new THREE.Group();
    this.group.add(this.handSlice);
    this.handPose = Pose.upright(0, 0, 0, 0, 0, this.mirrored);
    this.turn(this.flipTurn(this.handPose), 'both', 1.2, false, () => this.finishHandTurn(), this.handPose);
    this.anim.inHand = true;
  }

  finishHandTurn() {
    if (!this.handSlice) return;
    this.anim = null;
    this.mirrored = !this.mirrored;
    disposeGroup(this.handSlice);
    this.handSlice = null;
    for (const c of this.group.children) c.visible = true;
    this.group.scale.set(this.mirrored ? -1 : 1, 1, 1);
  }

  // Start turning by `angle` in the plane (a, b) about `pivot`. The pace is set from how far the
  // object reaches from the turning plane, measured at whichever end of the turn it lies flat.
  turn({ a, b, angle, pivot }, pace, dur, rest, then, from = this.pose) {
    from = new Pose([...from.P], from.M.map((col) => col.slice()));
    const flatEnd = pace === 'end' ? from.turned(a, b, angle, pivot) : from;
    const d = this.corners(flatEnd, false).map((X) => dot4(X.map((x, i) => x - pivot[i]), a));
    const reach = Math.max(...d.map(Math.abs));
    const oneSide = d.every((x) => x > 0) || d.every((x) => x < 0);
    const near = Math.min(reach, Math.max(2 * EPS, oneSide ? Math.min(...d.map(Math.abs)) : 0));
    this.anim = { kind: 'turn', from, a, b, angle, pivot, pace, dur, reach, near, t: 0, settle: rest, then };
  }

  update(dt) {
    const A = this.anim;
    if (!A) return;
    A.t = Math.min(1, A.t + dt / A.dur);
    if (A.kind === 'fall') {
      const p = this.position;
      p.y = A.y0 + (A.y1 - A.y0) * A.t * A.t;
      this.moveTo(p);
      if (A.t >= 1) {
        this.anim = null;
        this.show();
      }
      return;
    }
    const pose = A.from.turned(A.a, A.b, paced(A.t, A.angle, A.pace, A.reach, A.near), A.pivot);
    if (A.t >= 1) {
      pose.clean();
      this.anim = null;
    }
    if (A.inHand) {
      this.handPose = pose;
      disposeGroup(this.handSlice);
      this.handSlice = this.sliceGroup(pose, this.parts(), 0) ?? new THREE.Group();
      this.group.add(this.handSlice);
      setFlat(this.handSlice, 0.55);
    } else {
      this.pose = pose;
      this.show();
    }
    if (this.anim) return;
    if (A.settle) this.settle();
    A.then?.();
  }
}

// The rubber ball from the railway is a 4D ball. Its centre lies between pages Ⅰ and Ⅱ, so each of
// them holds a slice of it: a ball of the same size in the same place. Turning or flipping it about
// its centre changes nothing, and moving it in one page moves it in the other. Its lowest point is
// between the pages too, so wherever the floor runs through W it rests on the floor there, and in
// both pages it hangs a little above the floor.
export class Ball4 {
  constructor({ x, z, w, radius4 }, material, shadowMaterial, layers) {
    this.w = w;
    this.R = radius4;
    this.layers = layers;
    this.parts = [];
    for (let s = 0; s < W_POS.length; s++) {
      const d = W_POS[s] - w;
      if (Math.abs(d) >= radius4) continue;
      const r = Math.sqrt(radius4 ** 2 - d ** 2);
      const mesh = new THREE.Mesh(withColor(new THREE.IcosahedronGeometry(r, 1)), material);
      const shadow = new THREE.Mesh(withColor(new THREE.PlaneGeometry(r * 2.2, r * 2.2).rotateX(-Math.PI / 2)), shadowMaterial);
      layers[s].group.add(mesh, shadow);
      this.parts.push({ section: s, mesh, r, shadow });
    }
    this.place(x, z);
  }

  // A shadow on whatever is under the ball in each page shows how far above it the ball hangs.
  // Over the hole there is nothing to fall on.
  shade() {
    for (const { section, mesh, shadow } of this.parts) {
      const { x, y, z } = mesh.position;
      const w = W_POS[section];
      let top = -Infinity;
      for (const s of SUPPORTS) {
        if (x >= s.x0 && x <= s.x1 && z >= s.z0 && z <= s.z1 && w >= s.w0 && w <= s.w1 && s.y < y) top = Math.max(top, s.y);
      }
      shadow.visible = top > -Infinity;
      shadow.position.set(x, top + 0.008, z);
    }
  }

  get name() {
    return '皮球';
  }

  partIn(section) {
    return this.parts.find((p) => p.section === section);
  }

  center(section) {
    return this.partIn(section)?.mesh.position.clone();
  }

  // Held in her hand: every slice is centred on the same point.
  setCenter(p) {
    for (const part of this.parts) part.mesh.position.copy(p);
    this.shade();
  }

  // Put down over (x, z): it sinks until its 4D body touches a support, wherever in W that is.
  place(x, z) {
    const y = ballRestY(x, z, this.w, this.R, SUPPORTS);
    for (const part of this.parts) {
      part.mesh.position.set(x, y, z);
      bakeGroup(part.mesh, this.layers[part.section]);
    }
    this.shade();
  }
}
