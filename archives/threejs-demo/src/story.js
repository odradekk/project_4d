// Notes the player can read. mirrorWritten notes were written reversed by the child:
// they read correctly only while the player is mirrored too.
export const NOTES = {
  scope: {
    text: '小满的立体镜。\n她小时候最喜欢的东西。后来她说，透过它看到的不是画片，是"隔壁那一页"。',
  },
  n1: {
    text: '三月十二日\n小满的房门又是从里面反锁的。我拿螺丝刀把锁拆了，屋里没人，窗户是焊死的。\n半个钟头以后，她从屋里走出来，问我为什么拆她的锁。',
  },
  n2: {
    text: '三月二十日\n她现在一顿吃三碗，还是一天比一天瘦。医院查不出毛病。\n今天她用左手拿筷子。她以前是右撇子。\n她写给我的字条，我要对着镜子才看得懂。',
  },
  n3: {
    text: '四月一日\n她说那个东西没有恶意。"它看我们，就像我们看抽屉里面。"\n她说在家里翻一次，人就反了；再翻一次，又正回来。\n我问她翻到哪里去。她指了指自己的房间。',
  },
  junk: {
    text: '别打开她的门。\n不管里面是谁在敲。',
  },
  n5: {
    text: '本报讯　一九九七年四月三日\n本市一名十一岁女孩在家中失踪。据其父亲称，事发时女孩房门自内反锁，窗户完好。警方已介入调查。\n另据邻居反映，该户近一个月夜间常有翻纸的声音。',
  },
  n4: {
    crayon: true,
    mirrorWritten: true,
    text: '爸爸\n我在这一页是正的\n你那一页我是反的\n所以吃多少都没有用',
  },
  wall: {
    crayon: true,
    mirrorWritten: true,
    text: '爸爸\n我在这一页',
  },
  final: {
    crayon: true,
    mirrorWritten: true,
    text: '爸爸\n门我从里面锁好了\n这样它就进不来了',
  },
};

export const ENDING = '门是从里面锁上的。\n可它从来不需要门。\n\n— 试玩结束 —';
