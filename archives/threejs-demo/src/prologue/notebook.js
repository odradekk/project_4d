// Her maths exercise book: each rule she figured out, drawn the way a twelve-year-old would.

const KAI = '"KaiTi", "STKaiti", serif';
const W = 520;
const H = 640;

function rng(seed) {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

class Pen {
  constructor(ctx, seed) {
    this.x = ctx;
    this.r = rng(seed);
  }

  // A wobbly stroke through the points, drawn twice like a pencil going over its own line.
  line(pts, color = '#3a3a48', width = 2, closed = false) {
    const x = this.x;
    x.strokeStyle = color;
    x.lineWidth = width;
    x.lineCap = 'round';
    x.lineJoin = 'round';
    for (let pass = 0; pass < 2; pass++) {
      x.beginPath();
      const all = closed ? [...pts, pts[0]] : pts;
      all.forEach(([px, py], i) => {
        const jx = px + (this.r() - 0.5) * 2.5;
        const jy = py + (this.r() - 0.5) * 2.5;
        if (i === 0) x.moveTo(jx, jy);
        else x.lineTo(jx, jy);
      });
      x.stroke();
    }
  }

  rect(x0, y0, w, h, color, width) {
    this.line([[x0, y0], [x0 + w, y0], [x0 + w, y0 + h], [x0, y0 + h]], color, width, true);
  }

  circle(cx, cy, r, color, width) {
    const pts = [];
    for (let i = 0; i < 18; i++) pts.push([cx + Math.cos((i / 18) * Math.PI * 2) * r, cy + Math.sin((i / 18) * Math.PI * 2) * r]);
    this.line(pts, color, width, true);
  }

  // Crayon fill: many short scribbles inside a box.
  fill(x0, y0, w, h, color) {
    const x = this.x;
    x.strokeStyle = color;
    x.lineWidth = 3;
    x.globalAlpha = 0.45;
    for (let i = 0; i < w / 4; i++) {
      x.beginPath();
      const px = x0 + i * 4 + this.r() * 3;
      x.moveTo(px, y0 + this.r() * 4);
      x.lineTo(px + 6, y0 + h - this.r() * 4);
      x.stroke();
    }
    x.globalAlpha = 1;
  }

  text(str, px, py, size = 24, color = '#2f2f3a', rot = 0, mirrored = false) {
    const x = this.x;
    x.save();
    x.translate(px, py);
    x.rotate(rot);
    if (mirrored) x.scale(-1, 1);
    x.fillStyle = color;
    x.font = `${size}px ${KAI}`;
    x.fillText(str, 0, 0);
    x.restore();
  }
}

function paper(x) {
  x.fillStyle = '#f4efe1';
  x.fillRect(0, 0, W, H);
  x.strokeStyle = 'rgba(80,120,200,0.25)';
  x.lineWidth = 1;
  for (let y = 96; y < H; y += 34) {
    x.beginPath();
    x.moveTo(0, y);
    x.lineTo(W, y);
    x.stroke();
  }
  x.strokeStyle = 'rgba(210,70,70,0.35)';
  x.beginPath();
  x.moveTo(56, 0);
  x.lineTo(56, H);
  x.stroke();
}

function lines(p, rows, y0 = 438) {
  rows.forEach((s, i) => p.text(s, 74, y0 + i * 34, 24, '#2f2f3a', (i % 2 ? 1 : -1) * 0.004));
}

// A stack of pages, drawn as slanted sheets.
function stack(p, cx, top, n, gap = 26) {
  for (let i = 0; i < n; i++) {
    const y = top + i * gap;
    p.line([[cx - 110, y + 18], [cx - 40, y - 18], [cx + 110, y - 18], [cx + 40, y + 18]], '#6a6a80', 1.6, true);
  }
}

const PAGES = [
  {
    id: 'look',
    draw(p) {
      p.text('看', 74, 74, 40, '#c0392b');
      p.line([[110, 210], [170, 170], [230, 210], [170, 250]], '#2f2f3a', 3, true);
      p.circle(170, 210, 18, '#2a6fb0', 3);
      p.fill(160, 200, 20, 20, '#2a6fb0');
      p.rect(290, 150, 170, 130, '#2f2f3a', 2);
      for (let i = 0; i < 3; i++) {
        for (let j = 0; j < 2; j++) {
          p.rect(305 + i * 52, 185 + j * 48, 34, 16, '#8a6a3a', 2);
          p.line([[310 + i * 52, 185 + j * 48], [310 + i * 52, 170 + j * 48], [334 + i * 52, 170 + j * 48], [334 + i * 52, 185 + j * 48]], '#8a6a3a', 1.5);
        }
      }
      p.line([[230, 210], [285, 210]], '#2f2f3a', 2);
      p.line([[275, 202], [285, 210], [275, 218]], '#2f2f3a', 2);
      lines(p, ['按住，举起来看，', '能看到另一页的教室。', '一模一样，可是一个人也没有。', '椅子都倒扣在桌子上。']);
    },
  },
  {
    id: 'flip',
    draw(p) {
      p.text('翻面', 74, 74, 40, '#c0392b');
      p.rect(110, 180, 120, 60, '#2f2f3a', 3);
      p.fill(112, 182, 116, 56, '#f19fae');
      p.text('橡皮', 135, 222, 30, '#2f2f3a');
      p.rect(300, 180, 120, 60, '#2f2f3a', 3);
      p.fill(302, 182, 116, 56, '#f19fae');
      p.text('橡皮', 395, 222, 30, '#2f2f3a', 0, true);
      p.line([[230, 170], [265, 140], [300, 170]], '#2f2f3a', 2);
      p.line([[288, 160], [300, 170], [290, 182]], '#2f2f3a', 2);
      lines(p, ['绕着边翻过去，', '橡皮上的字就反了！', '同桌说我会变魔术。']);
    },
  },
  {
    id: 'stand',
    draw(p) {
      p.text('竖起来', 74, 74, 40, '#c0392b');
      stack(p, 260, 190, 5, 30);
      p.rect(252, 130, 16, 200, '#2f2f3a', 2);
      p.fill(254, 132, 12, 196, '#e8b84a');
      p.line([[252, 330], [260, 352], [268, 330]], '#2f2f3a', 2);
      lines(p, ['把东西竖进去，', '它就插进了好几页里。', '这一页只剩薄薄的一片，', '再放倒，就回来了。']);
    },
  },
  {
    id: 'glow',
    draw(p) {
      p.text('发光的东西', 74, 74, 40, '#c0392b');
      p.rect(120, 220, 120, 70, '#2f2f3a', 3);
      for (let i = 0; i < 8; i++) {
        const a = (i / 8) * Math.PI * 2;
        p.line([[180 + Math.cos(a) * 80, 255 + Math.sin(a) * 60], [180 + Math.cos(a) * 105, 255 + Math.sin(a) * 80]], '#e8b84a', 3);
      }
      p.line([[310, 290], [310, 255], [360, 255], [360, 220], [410, 220], [410, 290]], '#2f2f3a', 3, true);
      p.text('讲台', 150, 330, 22);
      p.text('那一页的讲台', 300, 330, 22);
      lines(p, ['有的东西会发光。', '别的页里也有它，', '可是样子不一样。']);
    },
  },
  {
    id: 'seam',
    draw(p) {
      p.text('缝', 74, 74, 40, '#c0392b');
      stack(p, 230, 200, 4, 34);
      p.line([[230, 120], [228, 360]], '#2f2f3a', 2);
      for (let y = 150; y < 350; y += 22) p.line([[218, y], [242, y + 6]], '#c0392b', 2);
      p.line([[226, 110], [234, 110], [232, 140], [228, 140]], '#7a7a7a', 2, true);
      p.circle(400, 230, 34, '#1c1b1f', 3);
      p.fill(370, 200, 60, 60, '#1c1b1f');
      p.line([[372, 205], [380, 180], [392, 200]], '#1c1b1f', 3);
      p.line([[408, 200], [420, 180], [428, 205]], '#1c1b1f', 3);
      p.circle(388, 228, 4, '#9bd13a', 3);
      p.circle(412, 228, 4, '#9bd13a', 3);
      lines(p, ['猫能看见缝！', '举着棱镜走过去，', '就能从缝里钻到那一页。', '（煤球比我先知道。）']);
    },
  },
  {
    id: 'link',
    draw(p) {
      p.text('隔着一页', 74, 74, 40, '#c0392b');
      // Two sheets: her hand holding the stub of the stick here, the yarn ball over there.
      p.line([[80, 300], [130, 180], [250, 180], [200, 300]], '#6a6a80', 2, true);
      p.line([[290, 300], [340, 180], [460, 180], [410, 300]], '#6a6a80', 2, true);
      p.text('这一页', 120, 330, 20);
      p.text('那一页', 330, 330, 20);
      p.rect(160, 230, 10, 10, '#8a6a3a', 3);
      p.line([[140, 270], [158, 242], [172, 242], [182, 268]], '#e3a77a', 3);
      p.circle(375, 240, 20, '#c0392b', 3);
      p.fill(358, 224, 34, 34, '#c0392b');
      for (let x = 180; x < 350; x += 16) p.line([[x, 236], [x + 8, 236]], '#2f2f3a', 2);
      lines(p, ['把逗猫棒竖进去，', '毛线球就跑到了那一页。', '我在这边动，那边的球也跟着动！', '它们是同一根逗猫棒。']);
    },
  },
  {
    id: 'kinds',
    draw(p) {
      p.text('三种东西', 74, 74, 40, '#c0392b');
      // Stool -> four little squares.
      p.rect(80, 150, 60, 8, '#8a5a2a', 3);
      p.line([[86, 158], [86, 210]], '#8a5a2a', 3);
      p.line([[134, 158], [134, 210]], '#8a5a2a', 3);
      p.line([[110, 225], [110, 245]], '#2f2f3a', 2);
      for (const [x, y] of [[90, 262], [126, 262], [90, 282], [126, 282]]) p.rect(x, y, 8, 8, '#8a5a2a', 2);
      // Ball here and ball there.
      p.circle(235, 190, 22, '#d8402f', 3);
      p.fill(215, 170, 40, 40, '#f2c94c');
      p.circle(235, 270, 22, '#d8402f', 3);
      p.fill(215, 250, 40, 40, '#f2c94c');
      for (let y = 214; y < 246; y += 10) p.line([[235, y], [235, y + 5]], '#2f2f3a', 2);
      // Nut -> big ring.
      p.rect(355, 180, 18, 12, '#6e5446', 3);
      p.line([[364, 205], [364, 225]], '#2f2f3a', 2);
      p.circle(364, 275, 34, '#9aa6c8', 4);
      p.text('凳子', 92, 320, 18);
      p.text('皮球', 218, 320, 18);
      p.text('螺母', 346, 320, 18);
      lines(p, ['竖进去以后：', '大部分东西只剩薄薄一片；', '皮球两页都有一个，怎么转都一样；', '螺母在那一页变成了大圆圈。', '（皮球和螺母都是铁道边捡的。）'], 404);
    },
  },
  {
    id: 'question',
    draw(p) {
      p.text('体动成什么？', 74, 74, 40, '#c0392b');
      p.circle(100, 220, 4, '#2f2f3a', 4);
      p.line([[150, 220], [210, 220]], '#2f2f3a', 3);
      p.rect(250, 190, 60, 60, '#2f2f3a', 3);
      p.rect(350, 200, 55, 55, '#2f2f3a', 3);
      p.rect(370, 180, 55, 55, '#2f2f3a', 3);
      p.line([[350, 200], [370, 180]], '#2f2f3a', 3);
      p.line([[405, 200], [425, 180]], '#2f2f3a', 3);
      p.line([[405, 255], [425, 235]], '#2f2f3a', 3);
      p.line([[350, 255], [370, 235]], '#2f2f3a', 3);
      p.text('？', 450, 235, 48, '#c0392b');
      lines(p, ['点动成线，线动成面，面动成体。', '那体动了，会变成什么？', '老师说，这个问题你们长大了就知道了。']);
    },
  },
];

export class Notebook {
  constructor(canvas, unlocked) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.unlocked = new Set(unlocked);
    this.index = 0;
  }

  get pages() {
    return PAGES.filter((p) => this.unlocked.has(p.id));
  }

  unlock(id) {
    if (this.unlocked.has(id)) return false;
    this.unlocked.add(id);
    return true;
  }

  show(id) {
    const i = this.pages.findIndex((p) => p.id === id);
    if (i >= 0) this.index = i;
    this.render();
  }

  turn(d) {
    const n = this.pages.length;
    this.index = (this.index + d + n) % n;
    this.render();
  }

  render() {
    const page = this.pages[this.index];
    paper(this.ctx);
    page.draw(new Pen(this.ctx, PAGES.indexOf(page) * 97 + 13));
    this.ctx.fillStyle = 'rgba(60,60,80,0.5)';
    this.ctx.font = `16px ${KAI}`;
    this.ctx.fillText(`${this.index + 1} / ${this.pages.length}`, W - 70, H - 20);
  }
}
