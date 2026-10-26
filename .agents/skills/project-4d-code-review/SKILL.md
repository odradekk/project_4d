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
| `engine/**`、`engine.tests/**` | [框架文档](../../../docs/framework.md)、玩法文档；决策笔记 `2026-10-26-engine-rewrite`、`2026-10-26-device-translation` |
| `game/adapter/**` | 框架文档「引擎模块」；决策笔记 `2026-10-26-region-scene-authoring` |
| `game/**` 的其余部分 | 框架文档、玩法文档、[序章文档](../../../docs/prologue.md)、[故事文档](../../../docs/story.md)；决策笔记 `2026-10-26-prologue-greybox`、`2026-10-26-game-layer-csharp`、`2026-10-26-region-scene-authoring`。[风格文档](../../../docs/style.md)等序章定稿后才适用 |
| `docs/**` | `AGENTS.md`「Language and terms」；玩法文档「术语」表 |
| `.agents/notes/**` | [.agents/notes/accepted/AGENTS.md](../../notes/accepted/AGENTS.md)；`decision-notes` skill |
| `archives/threejs-demo/**` | 已冻结，不改（`AGENTS.md`「Map」） |

决策笔记按 ID 查找：`git ls-files '*<ID>.md'`。

框架、玩法、序章文档目前描述的是要实现的设计，引擎和游戏代码还没写。代码已经实现的部分，文档就是规则；还没实现的部分，文档只约束改动新写的代码，文档和现有代码之间已知的差距不算问题。下面几处已被已接受的决策笔记取代，以笔记为准，#1 会改文档：

- `AGENTS.md` 里说原型是「reference implementation for ported 4D math」和「Ported math is checked against the archived prototype's output」两句：`2026-10-26-engine-rewrite`。
- 框架文档解耦规则第 7 条、模块表的「原型对应文件」一列：`2026-10-26-engine-rewrite`；「楼房」的资产制作：`2026-10-26-region-scene-authoring`；游戏层「GDScript 或 C# 均可」：`2026-10-26-game-layer-csharp`；四维装置只有旋转运动：`2026-10-26-device-translation`。
- 序章文档第 4 场「绕着桌沿翻过去」：`2026-10-26-prologue-greybox`。序章文档待定问题第 2 条已由玩法文档「接缝」定下。

## 自动运行的检查

目前没有。钩子要每个 clone 自己连接，仓库也还没有 CI（#1 会加上）。

## 由审查者运行

用 `pre-push-checks` skill 为改动的路径挑出要跑的检查，运行后把命令和结果写进报告：

- 文件大小：`python .agents/scripts/check_file_size.py --all`。
- 提交信息里的 AI 署名：`echo "HEAD $(git rev-parse HEAD) refs/heads/main $(git rev-parse origin/main)" | python .agents/scripts/strip_attribution.py --pre-push`，检查还不在 `origin/main` 上的提交。
- 决策笔记：`python <decision-notes skill 的目录>/scripts/verify_notes.py .agents/notes --base $(git merge-base HEAD origin/main)`。
- `AGENTS.md` Agent workflow 一节表格里对应改动路径的检查：引擎测试、`dotnet build game/Game.csproj`、`dotnet build game/Game.sln`、Godot 的 `--scene` 和 `--import` 检查。Godot 的路径写在那张表下面。Godot 载入失败时退出码仍是 0，输出里有 `ERROR:` 行就算失败。

## 阻止合并的要求

1. **引擎不依赖 Godot。** `engine/` 不引用任何 Godot 包或程序集；引擎测试在 `dotnet test` 下运行，不需要 Godot。一旦引用，四维逻辑会和渲染缠在一起，测试也离不开 Godot。来源：`AGENTS.md`「Architecture」；框架文档「解耦规则」第 1、7 条。
2. **四维计算只在引擎里。** 适配层和游戏层不自己算四维位置、旋转、切片、碰撞、支撑或跨截面规则，只调用引擎接口；可以把编排参数传给引擎。否则同一条规则会有两份实现，互相对不上。来源：`AGENTS.md`「Architecture」；解耦规则第 2 条。
3. **引擎契约最小且一致。** 只有接口契约直接用到的类型是 `public`，其余都是 `internal`；契约里只出现引擎自己的类型，不出现 Godot 类型。改接口的 PR 同时改适配层和契约文档。否则并行的几条工作线会对着不同的契约写代码。来源：解耦规则第 3、6 条；#2。
4. **引擎可复现，不碰外部资源。** 引擎只在宿主调用推进接口、传入时间步长时前进，相同的输入序列得到相同的输出；不读系统时间，不渲染、不放声音、不读输入、不读写文件。否则自动化测试无法复现。来源：`AGENTS.md`「Architecture」；解耦规则第 4、5 条。
5. **数值和坐标约定。** 引擎用 `double` 计算；坐标单位是米，Y 轴朝上，右手系，和 Godot 一致；适配层只做 `double` 到 `float` 的转换。来源：`AGENTS.md`「Architecture」；解耦规则第 3 条。
6. **精确运动学，没有四维动力学。** 几何、切片、支撑和扫掠碰撞按精确公式计算；不模拟惯性、翻滚、堆叠，失去支撑的物体竖直下落。否则切片会忽大忽小，玩家无法推理。来源：`AGENTS.md`「Architecture」；框架文档「核心原则」第 4 条。
7. **谜题和剧情条件只走通用查询。** 条件通过引擎的通用查询读取状态；需要新条件时，在引擎里加一个通用查询，不写只服务一个谜题的判断。原型里的 `inPit`、`ballBridges` 就是反例。来源：`AGENTS.md`「Architecture」；核心原则第 6 条。
8. **跨截面规则不被绕过。** 穿过镜像缝只翻转穿缝者和随身物品的四维旋转，不翻转任何截面的几何；手性由旋转矩阵算出，不另存开关；竖起的物体只能在原截面放倒；换截面只能经接缝。镜像归属曾在文档里写错过。来源：框架文档「跨截面规则」；提交 `6007ef9`。
9. **新的四维物品先在引擎里做完。** 一个四维物品子类进关卡之前，引擎里的切片、碰撞、支撑三套精确算法都已实现并有测试。来源：框架文档「内容分类」的「四维物品」。
10. **引擎测试的期望值来自几何。** 期望值来自手算的精确情形、必须成立的几何性质、框架文档里由公式算出的数值；不翻译原型代码，也不用原型生成的数据。来源：决策笔记 `2026-10-26-engine-rewrite`。
11. **游戏层只用 C#。** 不加 GDScript 文件。来源：决策笔记 `2026-10-26-game-layer-csharp`。
12. **楼房元素只摆一次。** 区域场景里每个楼房元素只出现一次，并标注 W 区间；不在几个截面里各摆一份。来源：决策笔记 `2026-10-26-region-scene-authoring`。
13. **白模阶段不做表现层。** 序章定稿之前，改动不加 PS1 渲染、贴图、正式建模、氛围光照、人物动画或正式音效。来源：决策笔记 `2026-10-26-prologue-greybox` 的 Scope 一节。
14. **不改归档原型。** `archives/threejs-demo/` 已冻结。来源：`AGENTS.md`「Map」。
15. **导出配置映射。** `game/Game.sln` 里的每个项目都把 Godot 的 ExportDebug、ExportRelease 映射到对应的 Debug、Release。漏掉的话，导出版会用未经优化的引擎。来源：`AGENTS.md`「Map」；提交 `423dab8`。
16. **语言。** 代码、注释、提交信息用英文；文档和游戏文本用中文。来源：`AGENTS.md`「Language and terms」。
17. **一个概念一个词。** 用词以玩法文档的「术语」表为准，新概念先在表里定词；代码里 `Section`（某个 W 上的三维世界）和 `Slice`（物体在截面里切出的形状）分开；「页」只出现在游戏文本里。改名时在全仓库搜一遍旧词：曾有一次改名在序章文档里漏了几处。来源：`AGENTS.md`「Language and terms」；提交 `3eee9fd`。

## 建议

这些作为建议报告，不阻止合并。

- **代码原则**（全部代码）：少即是多、KISS、组合优于继承、信息隐藏、YAGNI、不过度设计、代码胜过注释。重点看：只有一个实现、也不需要替换的类是否多套了一层接口；普通 .NET 类型是否依赖了 `GodotObject`；是否留下没人用的辅助函数、包装层和设置；注释是否只写代码表达不了的原因。来源：`AGENTS.md`「Code principles」。

## 流程检查

- 改动满足所做 issue 的验收标准。完成 issue 的 PR 在描述里写 `Fixes #N`，它的提交不写 issue 编号；不经 PR 直接提交到 `main` 的，在提交信息里写 issue 编号，完成 issue 的那个提交写 `Fixes #N`。来源：`AGENTS.md` 的 Tracker 一行。
- 每个 PR 有且只有一个 `kind/*` 标签，再加上所有适用的 `area/*` 标签。来源：`AGENTS.md` 的 Labels 一行。
- 改动不修改 `.agents/notes/proposed/` 以外的决策笔记，`decision-notes` 校验器带 `--base` 通过；和已接受的笔记冲突时，在报告里指出冲突，交给用户决定要不要重新规划，不改笔记。来源：`AGENTS.md` 的 Decision notes 一行。
- 如果改动碰到的代码在注释里引用了某篇决策笔记，改动要遵守这篇笔记；笔记已被取代时，遵守取代它的那篇。新代码的写法由某篇已接受的笔记解释时，在注释里引用它。来源：`decision-notes` skill。
- 新写或改动的文字按 `plain-language` skill 修订。来源：`AGENTS.md` 的 Prose 一行。

## 报告

每个问题写明：来自哪项检查（如果有）、问题是什么、在哪里、有什么影响、证据。阻止合并的问题排在建议前面，然后列出运行过的检查和结果。有 PR 时，关于某一处的问题写在 diff 的那一行上，关于整个改动的问题写在 PR 上；没有 PR 时，在报告里给出文件和行号。作者收到审查意见后，先用 `verify-bug` skill 核实；成立的就修，不成立的给出证据。

来源核对到提交 `b6e7e5a`（2026-10-26）。
