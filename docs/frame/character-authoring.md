# 角色的搭法

[契约入口](contract.md) · [实体数据](contract-data.md) · [场景标注](scene-authoring.md)

猫、老师、同桌和同学是三维生物，由出生点（CreatureSpawn3D）载入。出生点的 `CharacterScene` 根节点是 Character3D 的就是角色，数量不限。Character3D 是游戏层的节点，脚本在 `game/characters/`，不在 `game/adapter/`。

## 角色场景

| 节点 | 内容 |
|---|---|
| Character3D（CharacterBody3D，场景根节点） | `string DisplayName`（台词前的名字，出生点的 `DisplayName` 不空时以它为准）、`float WalkSpeed/RunSpeed`；碰撞层 2，碰撞掩码 1 |
| Shape（CollisionShape3D） | 必须有，胶囊形 |
| Visual（Node3D） | 必须有，装着模型，脚本用它表现坐、趴和镜像 |
| Cat、Pickup | 猫再带这两个子节点。Cat 是行为节点；Pickup 是 Interactable3D，球形，碰撞层 4，提示「抱起」 |
| Teacher | 老师再带这个行为子节点 |

碰撞层 2 是角色的身体：玩家和别的角色不会撞上它，准星的射线也看不到它。碰撞层 4 是猫用来被抱起的球，准星看得到，找接缝挡没挡住的射线看不到。

## 路线

角色没有寻路，只朝目标走直线，被挡住就沿障碍滑行。路线弯折的地方，比如绕过塌洞、上折返的楼梯，由场景脚本给出路径点：老师的 `Patrol` 按路径点走，猫的 `Follow` 接受一个把玩家位置换成当前要去的点的函数，`Chase` 的目标本来就是函数。

## 抱猫和 E 键

玩家可以抱起猫。抱着时再给猫（或它的身体）任何命令都会把猫放下，穿缝也会放下它。双臂一次只拿一样：手里有东西时抱不起猫，抱着猫时取不出背包里的东西。E 键依次作用于准星对着的 Interactable3D、手里的物品、抱着的猫、准星对着的物件，屏幕上的提示按同一顺序。
