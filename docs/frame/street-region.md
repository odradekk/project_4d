# 街道的区域

[场景标注](scene-authoring.md) · [角色搭法](character-authoring.md) · [家的区域](home-region.md)

`game/regions/street.tscn` 是序章第 2 场（上学路）和第 6 场（回家路与小卖部）共用的区域，格式见[场景标注](scene-authoring.md)。楼房元素、固定物件和道具分成 `game/regions/street/` 下的几个子场景，盒子零件的模板是 `game/regions/box_part.tscn`。本页不是契约，只写脚本和以后的修改要遵守的限制；具体坐标以场景为准。

坐标：x 向东（沿街），z 向南，y 向上，单位米。区域只有一个截面Ⅰ（W = 0）和一栋楼 `street`（W −0.5 到 0.5），棱镜关闭。两场的脚本在 `game/flow/`：`SchoolRoad` 和 `HomeRoad`，共用 `Street`；调试入口是 `-- --start=school-road` 和 `-- --start=home-road`。

## 子场景

| 文件 | 内容 |
|---|---|
| `road.tscn` | 地面、街两端的墙、南北两排楼、横幅和它的两根杆、广播杆、校门的墙（x 64.7–65.3，门洞 z 0.5–4.5） |
| `building.tscn` | 筒子楼和楼道：楼道宽 3 米（x −1.5 到 1.5），口在 z −5，尽头在 z −14；斜坡、斜坡左边的扶手和二楼平台 |
| `kiosk.tscn` | 小卖部的墙、顶和窗口柜台，x 35–41，z −5 到 −2，窗口朝南 |
| `things.tscn` | 固定物件：家门、早点摊、校门口的小摊、跳皮筋的绳子、货架、柜台上的货 |
| `props.tscn` | 道具、标记点和招牌，放在 `Sections/Ⅰ/Props` 下，用 `Region3D.Props("Ⅰ")` 取 |

## 楼道和斜坡

- 斜坡 35°，脚在 z −6，顶在 z −10.28、高 3 米，宽 1.5 米（x 0 到 1.5）；平台在斜坡顶上，z −10.28 到 −14。台阶用斜坡代替：角色靠 Godot 物理走路，上不了台阶，斜坡在 45° 以内才走得上去。
- 斜坡左边（x −1.5 到 0）是楼道里的空地，一直通到尽头的墙角；斜坡底下的空间从侧面敞开。猫在斜坡底下往坡脚走，过了 z −6.9 就会卡住，那里离地不到 0.35 米。斜坡左边沿有一道扶手 `Rail`，所以走在斜坡上的猫和人不会从左边掉下去，猫斜着去找站在平台左半边的小满时，会贴着扶手滑上去。
- 煤球的出生点在斜坡底下 (0.75, 0.02, −8.0)，那里离地 1.16 米，装得下它。第 2 场它在那里；第 6 场脚本把它挪到楼道口。
- 猫没有寻路。第 6 场它跟着小满上楼，路径点是 `RampMouth`（斜坡口，在斜坡前）和 `WalkwayFront`（斜坡左边空地的前端）：猫在楼道深处或不在斜坡正前方时，先走这两个点，再直接上斜坡。
- 家门 `HomeDoor` 在平台尽头 z −14 的墙上。它就是家的前门；怎么和家的区域接上，见[序章的流程](prologue-flow.md)。

## 道具和标记点

放在 `Props` 下，玩家按 E 用的东西是 `Interactable3D`，在第 3 层（`collision_layer = 4`），提示文字由脚本按当时的情形写。

- 第 2 场用 `BunStall`、`ToyStall`、`SchoolGate`；第 6 场用 `IcePop`、`Soda`、`Spicy`、`Stickers`、`Sausage`、`HomeDoor`。脚本把用不到的道具关掉。
- 喂猫的交互 `Feed` 不在区域里，第 2 场的脚本把它挂在猫身上，跟着猫走；那时 `Cat.Holdable` 是 false，猫不能被抱起。
- 标记点（Marker3D）：`EmptyCorner`（楼道尽头的墙角，猫盯着它）、`GateStart`（第 6 场小满站的地方，朝向回家的方向）、`CatWaits`（第 6 场猫等的地方）、`RampMouth`、`WalkwayFront`、`Speaker`（广播的声源）、`Bell`（自行车铃的声源）。
- 招牌（Label3D）：横幅「迎接新千年」（两面各一块）和校门上的「厂子弟小学」。

## 人和钱

出生点：小满 `xiaoman` 在平台上，面朝楼道；煤球 `cat`；早点摊的摊主 `bun_vendor`、校门口小摊的摊主 `toy_vendor`、小卖部的老板娘 `clerk`；三个跳皮筋的孩子 `rope_a`、`rope_b`、`rope_c`。摊主和老板娘用 `game/characters/vendor.tscn`，孩子用同学的场景，名字由出生点的 `DisplayName` 给。

钱是 `Wallet`（`game/flow/Wallet.cs`），单位是毛，场景之间传。第 2 场默认 20 毛，包子 5 毛；第 6 场默认 15 毛，小卖部的冰棍 3 毛、汽水 10 毛、辣条 2 毛、贴纸 5 毛，火腿肠 5 毛。挑任何一样再买火腿肠，都不会超过包子之后剩下的 15 毛。
