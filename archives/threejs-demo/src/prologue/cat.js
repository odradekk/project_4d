import * as THREE from 'three';

function box(material, tex, [x0, y0, z0, x1, y1, z1]) {
  const g = new THREE.BoxGeometry(x1 - x0, y1 - y0, z1 - z0);
  g.translate((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2);
  g.setAttribute('color', new THREE.Float32BufferAttribute(new Float32Array(g.attributes.position.count * 3), 3));
  return new THREE.Mesh(g, material(tex));
}

const rand = (a, b) => a + Math.random() * (b - a);

// 煤球: a small black stray. Cats can see seams; that is the whole plot of the prologue.
export class Cat {
  constructor(material) {
    this.root = new THREE.Group();
    this.body = new THREE.Group();
    this.root.add(this.body);
    const m = (tex, b) => {
      const mesh = box(material, tex, b);
      this.body.add(mesh);
      return mesh;
    };
    m('fur', [-0.065, 0.12, -0.17, 0.065, 0.25, 0.15]);
    this.head = new THREE.Group();
    this.head.position.set(0, 0.25, 0.16);
    this.body.add(this.head);
    const h = (tex, b) => this.head.add(box(material, tex, b));
    h('fur', [-0.06, -0.05, -0.04, 0.06, 0.06, 0.08]);
    h('fur', [-0.055, 0.06, -0.01, -0.02, 0.1, 0.02]);
    h('fur', [0.02, 0.06, -0.01, 0.055, 0.1, 0.02]);
    h('cateye', [-0.042, 0.005, 0.08, -0.016, 0.022, 0.082]);
    h('cateye', [0.016, 0.005, 0.08, 0.042, 0.022, 0.082]);
    h('catnose', [-0.008, -0.018, 0.08, 0.008, -0.006, 0.082]);
    this.legs = [[-0.04, 0.11], [0.04, 0.11], [-0.04, -0.13], [0.04, -0.13]].map(([x, z]) => {
      const leg = new THREE.Group();
      leg.position.set(x, 0.14, z);
      leg.add(box(material, 'fur', [-0.016, -0.14, -0.016, 0.016, 0, 0.016]));
      this.body.add(leg);
      return leg;
    });
    this.tail = new THREE.Group();
    this.tail.position.set(0, 0.22, -0.17);
    let parent = this.tail;
    this.tailSegs = [];
    for (let i = 0; i < 3; i++) {
      const seg = new THREE.Group();
      seg.position.set(0, i === 0 ? 0 : 0.09, 0);
      seg.add(box(material, 'fur', [-0.012, 0, -0.012, 0.012, 0.09, 0.012]));
      parent.add(seg);
      this.tailSegs.push(seg);
      parent = seg;
    }
    this.body.add(this.tail);

    this.section = 0;
    this.pos = new THREE.Vector3();
    this.yaw = 0;
    this.state = 'wander';
    this.pose = 'sit';
    this.walk = 0;
    this.t = 0;
    this.timer = 2;
    this.target = null;
    this.jump = null;
    this.lookTarget = null;
    this.avoid = null; // (x, z) => true where it won't walk while chasing
    this.onMeow = null;
    this.meowTimer = rand(6, 12);
    this.zone = null;
  }

  setShade(v) {
    this.root.traverse((o) => {
      if (!o.isMesh) return;
      const col = o.geometry.attributes.color;
      for (let i = 0; i < col.count; i++) col.setXYZ(i, v, v, v);
      col.needsUpdate = true;
    });
  }

  place(section, x, y, z, yaw) {
    this.section = section;
    this.pos.set(x, y, z);
    this.yaw = yaw;
  }

  wander(zone) {
    this.state = 'wander';
    this.zone = zone;
    this.target = null;
    this.timer = rand(1, 3);
  }

  goTo(x, z, speed, then) {
    this.state = 'walk';
    this.target = { x, z, speed, then };
  }

  // Like goTo, but also changes height along the way (down a ramp, say).
  walkTo(x, y, z, speed, then) {
    this.state = 'walk';
    this.target = { x, y, z, speed, then };
  }

  // Chase whatever getTarget() returns ({x, y, z} or null), pouncing when close.
  chase(getTarget) {
    this.state = 'chase';
    this.getTarget = getTarget;
    this.target = null;
    this.pounceCd = 0.5;
  }

  sitFacing(yaw) {
    this.state = 'sit';
    this.target = null;
    this.yaw = yaw;
  }

  leap(x, y, z, dur, then) {
    this.state = 'jump';
    this.jump = { from: this.pos.clone(), to: new THREE.Vector3(x, y, z), t: 0, dur, then };
    this.yaw = Math.atan2(x - this.pos.x, z - this.pos.z);
  }

  meow() {
    this.onMeow?.(this);
  }

  update(dt) {
    this.t += dt;
    this.pose = 'sit';
    if (this.state === 'wander') {
      if (!this.target) {
        this.timer -= dt;
        if (this.timer <= 0) {
          const z = this.zone;
          this.target = { x: rand(z.x0, z.x1), z: rand(z.z0, z.z1), speed: 0.35 };
        }
      }
      if (this.target && this.stepToward(dt)) {
        this.target = null;
        this.timer = rand(2, 6);
      }
    } else if (this.state === 'walk' || this.state === 'run') {
      if (this.stepToward(dt)) {
        const then = this.target.then;
        this.target = null;
        this.state = 'sit';
        then?.();
      }
    } else if (this.state === 'chase') {
      const T = this.getTarget?.();
      this.pounceCd -= dt;
      this.lookTarget = T;
      if (T) {
        const d = Math.hypot(T.x - this.pos.x, T.z - this.pos.z);
        if (d > 0.35) {
          const { x, z } = this.pos;
          this.target = { x: T.x, z: T.z, speed: 1.1 };
          this.stepToward(dt);
          this.target = null;
          // Ground it won't set foot on: it waits at the edge instead.
          if (this.avoid?.(this.pos.x, this.pos.z)) {
            this.pos.x = x;
            this.pos.z = z;
            this.pose = 'sit';
          }
        } else if (this.pounceCd <= 0 && !this.avoid?.(T.x, T.z)) {
          const back = this.getTarget;
          this.leap(T.x, 0, T.z, 0.35, () => this.chase(back));
        }
      }
    } else if (this.state === 'jump') {
      const J = this.jump;
      J.t = Math.min(1, J.t + dt / J.dur);
      this.pos.lerpVectors(J.from, J.to, J.t);
      this.pos.y += Math.sin(J.t * Math.PI) * 0.45;
      this.pose = 'jump';
      if (J.t >= 1) {
        this.state = 'sit';
        J.then?.();
      }
    }
    if (this.state !== 'held') {
      this.meowTimer -= dt;
      if (this.meowTimer <= 0) {
        this.meowTimer = this.state === 'perch' ? rand(2.5, 5) : rand(10, 22);
        this.meow();
      }
    }
    this.animate();
  }

  stepToward(dt) {
    const T = this.target;
    const dx = T.x - this.pos.x;
    const dy = T.y === undefined ? 0 : T.y - this.pos.y;
    const dz = T.z - this.pos.z;
    const d = Math.hypot(dx, dy, dz);
    if (d < 0.03) return true;
    const s = Math.min(d, T.speed * dt);
    this.pos.x += (dx / d) * s;
    this.pos.y += (dy / d) * s;
    this.pos.z += (dz / d) * s;
    const want = Math.atan2(dx, dz);
    if (Math.hypot(dx, dz) > 0.01) this.yaw += Math.atan2(Math.sin(want - this.yaw), Math.cos(want - this.yaw)) * Math.min(1, dt * 8);
    this.walk += dt * T.speed * 22;
    this.pose = 'walk';
    return false;
  }

  animate() {
    if (this.state === 'held') return;
    this.root.position.copy(this.pos);
    this.root.rotation.set(0, this.yaw, 0);
    const walking = this.pose === 'walk';
    this.body.rotation.x = this.pose === 'sit' ? -0.45 : this.pose === 'jump' ? -0.25 : 0;
    this.body.position.y = this.pose === 'sit' ? 0.03 : 0;
    this.legs.forEach((leg, i) => {
      const phase = i === 0 || i === 3 ? 0 : Math.PI;
      leg.rotation.x = walking ? Math.sin(this.walk + phase) * 0.6 : this.pose === 'sit' && i < 2 ? 0.45 : 0;
    });
    const sway = this.state === 'sit' && this.target === null ? 0.5 : 0.25;
    this.tail.rotation.x = -0.9 + Math.sin(this.t * 1.7) * 0.15;
    this.tailSegs.forEach((s, i) => (s.rotation.z = Math.sin(this.t * 2.3 + i) * sway * (i + 1) * 0.4));
    // Following something with its eyes; otherwise just looking about.
    const L = this.lookTarget;
    if (L) {
      const rel = Math.atan2(L.x - this.pos.x, L.z - this.pos.z) - this.yaw;
      const turn = Math.atan2(Math.sin(rel), Math.cos(rel));
      this.head.rotation.y = Math.max(-1.2, Math.min(1.2, turn));
      const flat = Math.hypot(L.x - this.pos.x, L.z - this.pos.z);
      this.head.rotation.x = Math.max(-0.6, Math.min(0.8, -Math.atan2(L.y - this.pos.y - 0.3, flat)));
    } else {
      this.head.rotation.y = Math.sin(this.t * 0.6) * 0.2;
      this.head.rotation.x = 0;
    }
  }

  // In her arms: face up toward her, legs dangling.
  holdIn(camera) {
    this.state = 'held';
    camera.add(this.root);
    this.root.position.set(-0.17, -0.5, -0.42);
    this.root.rotation.set(-0.35, 0.35, 0);
    this.body.rotation.x = 0.2;
    this.legs.forEach((l) => (l.rotation.x = 0.3));
  }

  release(scene) {
    scene.add(this.root);
    this.state = 'sit';
  }

  updateHeld(dt) {
    this.t += dt;
    this.head.rotation.x = -0.3 + Math.sin(this.t * 0.8) * 0.08;
    this.tailSegs.forEach((s, i) => (s.rotation.z = Math.sin(this.t * 2 + i) * 0.2));
  }
}
