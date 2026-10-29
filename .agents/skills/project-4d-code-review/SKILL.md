---
name: project-4d-code-review
description: 按本仓库写下的规则和以往出过的错，审查 project_4d 的改动。用于审查 PR、分支、直接提交到 main 的提交，或还没提交的改动。
---

# 审查 project_4d 的改动

本 skill 由 `setup-code-review` 生成，也由它刷新。可以随意修改，但每项检查都要保留来源。

## 谁来审查

能启动子代理时，审查放在一个新的子代理里做，用能用的最强模型（Claude Code 里是 `opus`），你负责协调。写过这个改动的代理，或者看着它写出来的代理，和改动带着同样的假设，发现不了这些假设里藏着的问题。

1. 找到改动。PR：比较它的 head 和它与基准分支的 merge base，基准分支用 `gh pr view <N> --json baseRefName` 查。分支：和它与 `main` 的 merge base 比较。直接提交到 `main` 的：用户指定了提交就审查那些提交，没指定时审查还不在 `origin/main` 上的提交；两种情况都把还没提交的改动一起算进去。
2. 启动审查者时只给它这些：改动（base 和 head，或者提交列表加没提交的路径）、本 skill 的路径、改动所做的 issue 和决策笔记的 ID，以及 PR 描述（如果有），描述里的说法都要核实。产生这个改动的对话和推理不要给它。改动跨「规则来源」表里的几行时，每行启动一个审查者，再加一个只对照 issue 验收标准检查改动的审查者。
3. 把审查者发现的问题照原样写进给用户的报告。核实事实性的说法，比如某一行是否存在；不同意某个判断时，把你的理由写在旁边，不要删掉它。

没有子代理时自己审查，并在报告里说明审查是在作者的上下文里做的。

## 流程

审查者：

1. 列出改动的路径；对每个路径，先读「规则来源」里列出的文档，再读 diff。
2. 读 diff，并读足够多的周边代码来理解设计。
3. 对改动的路径运行「由审查者运行」里的检查。
4. 依次套用阻止合并的要求、改动路径上的建议、流程检查。
5. 写报告。

## 规则来源

| 路径 | 规则在 |
|---|---|
| 全仓库 | [AGENTS.md](../../../AGENTS.md)；用词以[玩法文档](../../../docs/gameplay.md)的「术语」表为准 |
| `engine/**`、`engine.tests/**` | [计算原则](../../../docs/frame/principles.md)、[内容分类](../../../docs/frame/content.md)、[跨截面规则与尺寸规范](../../../docs/frame/rules.md)、[引擎模块](../../../docs/frame/engine.md)、[公共契约](../../../docs/frame/contract.md)及其分页（[数据定义](../../../docs/frame/contract-data.md)、[动作与推进](../../../docs/frame/contract-actions.md)、[抓取与布置](../../../docs/frame/contract-carry.md)、[截面与查询](../../../docs/frame/contract-views.md)）、玩法文档；决策笔记 `2026-10-26-engine-rewrite`、`2026-10-26-device-translation` |
| `game/adapter/**` | 引擎模块、公共契约及其分页、[场景标注](../../../docs/frame/scene-authoring.md)、[预期操作](../../../docs/frame/operations-authoring.md)、[装置搭法](../../../docs/frame/device-authoring.md)；决策笔记 `2026-10-26-region-scene-authoring` |
| `game/**` | [框架目录](../../../docs/frame/README.md)、玩法文档；决策笔记 `2026-10-26-prologue-greybox`、`2026-10-26-game-layer-csharp`。[风格文档](../../../docs/style.md)等序章定稿后才适用。改动在下面几行的目录里时，另外读那一行的文档 |
| `game/regions/**` | 场景标注、预期操作、装置搭法、对应的区域页（[家](../../../docs/frame/home-region.md)、[学校](../../../docs/frame/school-region.md)、[街道](../../../docs/frame/street-region.md)）；决策笔记 `2026-10-26-region-scene-authoring` |
| `game/characters/**` | [角色搭法](../../../docs/frame/character-authoring.md) |
| `game/creatures/**` | [啃食者搭法](../../../docs/frame/grazer-authoring.md)、家的区域页 |
| `game/flow/**`、`game/ui/**`、`game/debug/**`、`game/main.tscn` | [序章的流程](../../../docs/frame/prologue-flow.md)、写到这个场景脚本的区域页、[序章文档](../../../docs/prologue.md)、[故事文档](../../../docs/story.md) |
| `docs/**` | `AGENTS.md`「Language and terms」；玩法文档「术语」表 |
| `.agents/notes/**` | [.agents/notes/accepted/AGENTS.md](../../notes/accepted/AGENTS.md)；`decision-notes` skill |
| `archives/threejs-demo/**` | 已冻结，不改（`AGENTS.md`「Map」） |

决策笔记按 ID 查找：`git ls-files '*<ID>.md'`。

现行的文档就是规则。描述目标设计的文档，已实现的部分是规则；还没实现的部分只约束改动新写的代码，文档和现有代码之间已知的差距不算问题。还没实现的有：存档（`Capture`、`Restore`）、共享量（`SetSharedValue`、`Region3D.SharedValues`）、`[Tool]` 的 W 选择器、被啃几何的烘焙、PS1 渲染管线，以及预期操作页里还没有资源的设置和步骤（`SeamOverride`、`SharedValueOverride`、`ExpectedMove`、`ExpectedAdvance`）。

## 自动运行的检查

每个 PR 的 GitHub Actions 都运行以下检查，配置见 [.github/workflows/check.yml](../../../.github/workflows/check.yml)。CI 已通过的机械检查不重复报告；失败、缺失或尚未完成的检查要写明。有 PR 时读取实际检查结果，不把本地通过写成远端已通过。

- `dotnet build game/Game.sln`。
- `dotnet test engine.tests`。
- `python .agents/scripts/check_file_size.py --all`。

## 由审查者运行

用 `pre-push-checks` skill 为改动的路径挑出手动检查，运行后把命令和结果写进报告：

- 提交信息里的 AI 署名：`echo "HEAD $(git rev-parse HEAD) refs/heads/main $(git rev-parse origin/main)" | python .agents/scripts/strip_attribution.py --pre-push`，检查还不在 `origin/main` 上的提交。
- 决策笔记：`python <decision-notes skill 的目录>/scripts/verify_notes.py .agents/notes --base $(git merge-base HEAD origin/main)`。
- 场景与导入资源：运行 `AGENTS.md` Agent workflow 表格对应的 Godot `--scene` 和 `--import` 检查。Godot 路径在表格下面；退出码为 0 但输出含 `ERROR:` 仍算失败。仓库没有 `.gitattributes`，Windows 上 `--import` 会改写 `.import` 文件的换行。如果 `.import` 文件只有换行变了（`git diff --ignore-cr-at-eol` 看不到差别），用 `git restore` 还原，不提交，也不算问题。来源：用户，2026-10-29。

钩子仍需每个 clone 自己连接。没有 PR 或 CI 结果时，按 `pre-push-checks` 补齐受改动影响的本地检查，并说明尚无远端结果。

## 阻止合并的要求

1. **引擎不依赖 Godot。** `engine/` 不引用任何 Godot 包或程序集；引擎测试在 `dotnet test` 下运行，不需要 Godot。一旦引用，四维逻辑会和渲染缠在一起，测试也离不开 Godot。来源：`AGENTS.md`「Architecture」；引擎模块「解耦规则」第 1、7 条。
2. **四维计算和判断只在引擎里。** 适配层和游戏层不自己算四维位置、旋转、切片、碰撞、支撑或跨截面规则，也不另写一份引擎的容差，只调用引擎接口；可以把编排参数传给引擎。要知道物体在哪、截面里看得见什么、接缝能不能过，就读引擎的查询和事件（`ReadEntity`、`ReadSeam`、`GetSliceBounds`、`Contact.Section`、`PrismView.SeenEntity`），不手摆标记盒子，也不按坐标、距离或 W 自己判断。否则同一条规则有两份实现，互相对不上。来源：`AGENTS.md`「Architecture」；解耦规则第 2 条；提交 `afd4ceb`、`97c656d`、`6c03b49`、`15381e2`、`c4a14b8`。
3. **挡人的东西做成引擎物体。** 挡玩家或角色的护栏、挡板、补缝面做成带 `EntityId` 的引擎固定物体，不放只有 Godot 看得见的碰撞体。只给交互射线用的薄盒子（如 `DoorLock`、猫的 `Pickup`）和区域页写明的普通碰撞体（学校的 `Door` 门板）除外。引擎查询看不见 Godot 的碰撞体，谜题可能被绕过，预期操作也测不到。来源：提交 `16e346d`（删掉 `PitGuard`）、`5f1301f`、`f7f6ae7`、`6406780`。
4. **引擎契约最小，文档跟着代码改。** 只有接口契约直接用到的类型是 `public`，其余都是 `internal`；契约里只出现引擎自己的类型，不出现 Godot 类型。改接口的 PR 同时改适配层和对应的契约页；改了行为、实体 ID、节点名或数值的 PR，同时改提到它们的区域页和搭法页。审查对照的就是这些文档，文档过时会让审查者误判，事后还要另外提交去补。来源：解耦规则第 3、6 条；#2；提交 `6fa7225`、`e132245`。
5. **引擎可复现，不碰外部资源。** 引擎只在宿主调用推进接口、传入时间步长时前进，相同的输入序列得到相同的输出；不读系统时间，不渲染、不放声音、不读输入、不读写文件。否则自动化测试无法复现。来源：`AGENTS.md`「Architecture」；解耦规则第 4、5 条。
6. **数值和坐标约定。** 引擎用 `double` 计算；坐标单位是米，Y 轴朝上，右手系，和 Godot 一致；适配层只做 `double` 到 `float` 的转换。来源：`AGENTS.md`「Architecture」；解耦规则第 3 条。
7. **精确运动学，没有四维动力学。** 几何、切片、支撑和扫掠碰撞按精确公式计算；不模拟惯性、翻滚、堆叠，失去支撑的物体竖直下落。否则切片会忽大忽小，玩家无法推理。来源：`AGENTS.md`「Architecture」；计算原则「核心原则」第 4 条。
8. **谜题和剧情条件只走通用查询。** 条件通过引擎的通用查询读取状态；需要新条件时，在引擎里加一个通用查询，不写只服务一个谜题的判断。原型里的 `inPit`、`ballBridges` 就是反例。来源：`AGENTS.md`「Architecture」；核心原则第 6 条。
9. **跨截面规则不被绕过。** 穿过镜像缝只翻转穿缝者和随身物品的四维旋转，不翻转任何截面的几何；手性由旋转矩阵算出，不另存开关；竖起的物体只能在原截面放倒；换截面只能经接缝。镜像归属曾在文档里写错过。来源：跨截面规则与尺寸规范「跨截面规则」；内容分类和动作与推进里的镜像缝；提交 `6007ef9`。
10. **实体记录不原地修改。** 引擎里的实体记录（`StaticEntity`、`Dynamic`、`Device`）只整体替换，定义加载后不变，变化写进状态。落点缓存依赖这一点，否则可转动物体会悬在空中，`FindSupport` 返回旧的落点；曾有从背包取出物品时改写了定义里的楼房。来源：提交 `552d867`（`PairLandings` 的注释）、`268b2a2`。
11. **新的四维物品先在引擎里做完。** 一个四维物品子类进关卡之前，引擎里的切片、碰撞、支撑三套精确算法都已实现并有测试。来源：内容分类「四维物品」。
12. **引擎测试的期望值来自几何，测试能发现回归。** 期望值来自手算的精确情形、必须成立的几何性质、框架文档里由公式算出的数值；不翻译原型代码，也不用原型生成的数据。测试的构造要真的触发被测情形，被测行为坏掉时测试会失败；曾有测试里的墙没碰到立方体，去掉墙也照样通过。来源：决策笔记 `2026-10-26-engine-rewrite`；提交 `212931a`、`f51326e`、`30eb651`。
13. **游戏层只用 C#。** 不加 GDScript 文件。来源：决策笔记 `2026-10-26-game-layer-csharp`。
14. **楼房元素只摆一次。** 区域场景里每个楼房元素只出现一次，并标注 W 区间；不在几个截面里各摆一份，也不为颜色或材质按 W 拆成两份。只有被啃时要整块消失的部分才单独成为一个元素，如 `DeepCorner`。来源：决策笔记 `2026-10-26-region-scene-authoring`；家的区域页第 35 行；提交 `16e346d`。
15. **白模阶段不做表现层。** 序章定稿之前，改动不加 PS1 渲染、贴图、正式建模、氛围光照、人物动画或其余环境声；声音可以用占位音效。区域只用适配层的默认光照，不加太阳、天空、台灯这类氛围光；区域页写明的玩法提示光（截面Ⅲ的两盏粉金色灯）除外。来源：决策笔记 `2026-10-26-prologue-greybox` 第 53 行；提交 `16e346d`；用户，2026-10-29。
16. **剧情步骤只推进一次。** 场景脚本等角色走到、吃完这类动作时，也要处理动作被新命令替换（`ActionReplaced`）；一次性的台词和事件不重复、不遗漏。否则剧情卡住，玩家只能重开。来源：提交 `16e346d`、`ee2649f`、`1a4f429`、`22d30ef`、`02c5825`。
17. **场景之间衔接正确。** 下一场从上一场结束的位置和状态开始；留给后一场用的节点自己停掉回调，不用 `ProcessMode = Disabled`（区域会一起冻住）；不读已经释放的节点。这类问题要连续玩通才会出现。来源：序章的流程「切换」；提交 `1929b9f`、`bdf7767`、`b0588e4`。
18. **Godot 检查不轮询瞬时状态。** 检查不去读只持续几帧的阶段或位置，改为等事件（如 `CatRoom.StageEntered`）或等到位置，超时按物理帧数算。否则检查偶发失败，掩盖真问题。来源：提交 `97c656d`、`432e529`、`a11f986`、`a0a1ea6`。
19. **播放过声音的无头检查，退出前等音频线程。** 退出前等 0.5 秒，节点离开场景树时不在主线程上等。音频线程要等声音淡出才放开它，不等就会在退出时报 `resources still in use`，Godot 检查判为失败。来源：提交 `a59d505`、`dee1337`；`game/audio/GrazerMusic.cs` 的注释。
20. **带类型 `[Export]` 数组的 Resource 基类不写成 `abstract`。** 照 `ExpectedStepResource` 的做法。写成 abstract 时，曾在一台机器上 5 次运行有 4 次报 `Handle is not initialized`，和 Godot issue 112067 的报错相同；原因还没确认。来源：提交 `65cbe6e`；`game/adapter/ExpectedStepResource.cs` 的注释。
21. **不改归档原型。** `archives/threejs-demo/` 已冻结。来源：`AGENTS.md`「Map」。
22. **导出配置映射。** `game/Game.sln` 里的每个项目都把 Godot 的 ExportDebug、ExportRelease 映射到对应的 Debug、Release。漏掉的话，导出版会用未经优化的引擎。来源：`AGENTS.md`「Map」；提交 `423dab8`。
23. **语言。** 代码、注释、提交信息用英文；文档和游戏文本用中文。来源：`AGENTS.md`「Language and terms」。
24. **一个概念一个词。** 用词以玩法文档的「术语」表为准，新概念先在表里定词；代码里 `Section`（某个 W 上的三维世界）和 `Slice`（物体在截面里切出的形状）分开；「页」只出现在游戏文本里。改名时在全仓库搜一遍旧词：曾有一次改名在序章文档里漏了几处。来源：`AGENTS.md`「Language and terms」；提交 `3eee9fd`。

## 建议

这些作为建议报告，不阻止合并。

- **代码原则**（全部代码）：少即是多、KISS、组合优于继承、信息隐藏、YAGNI、不过度设计、代码胜过注释。重点看：只有一个实现、也不需要替换的类是否多套了一层接口；普通 .NET 类型是否依赖了 `GodotObject`；是否留下没人用的辅助函数、包装层和设置；注释是否只写代码表达不了的原因。来源：`AGENTS.md`「Code principles」。
- **每帧调用的引擎计算**（`game/**`）：每帧或每次移动都会调用的切片、折叠面预览和扫掠，不在物体还在动时反复算，等它静止后再算；先测量确认慢在哪里。曾有手里螺母的折叠面每 0.15 秒算一次，每次约 200 毫秒。来源：提交 `5f5d080`、`b7cfe9a`、`2a96159`、`7da1451`。
- **按实体 ID 找节点**（`game/**`）：用 `SectionScene.StaticNodes` 按实体找节点，不按 Godot 生成的节点名找，`Door.1` 和 `Door_1` 会撞名。来源：提交 `a59d505`、`dee1337`。

## 流程检查

- 改动满足所做 issue 的验收标准。完成 issue 的 PR 在描述里写 `Fixes #N`，它的提交不写 issue 编号；不经 PR 直接提交到 `main` 的，在提交信息里写 issue 编号，完成 issue 的那个提交写 `Fixes #N`。来源：`AGENTS.md` 的 Tracker 一行。
- 每个 PR 有且只有一个 `kind/*` 标签，再加上所有适用的 `area/*` 标签。来源：`AGENTS.md` 的 Labels 一行。
- 改动不修改 `.agents/notes/proposed/` 以外的决策笔记，`decision-notes` 校验器带 `--base` 通过；和已接受的笔记冲突时，在报告里指出冲突，交给用户决定要不要重新规划，不改笔记。来源：`AGENTS.md` 的 Decision notes 一行。
- 如果改动碰到的代码在注释里引用了某篇决策笔记，改动要遵守这篇笔记；笔记已被取代时，遵守取代它的那篇。新代码的写法由某篇已接受的笔记解释时，在注释里引用它。来源：`decision-notes` skill。
- 新写或改动的文字按 `plain-language` skill 修订。来源：`AGENTS.md` 的 Prose 一行。

## 报告

每个问题写明：来自哪项检查（如果有）、问题是什么、在哪里、有什么影响、证据。阻止合并的问题排在建议前面，然后列出运行过的检查和结果。有 PR 时，关于某一处的问题写在 diff 的那一行上，关于整个改动的问题写在 PR 上；没有 PR 时，在报告里给出文件和行号。作者收到审查意见后，先用 `verify-bug` skill 核实；成立的就修，不成立的给出证据。

来源核对到提交 `5541624`（2026-10-29）。
