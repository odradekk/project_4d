# 游戏层只用 C#

Status: accepted

## Problem

框架文档允许游戏层用 GDScript 或 C#。剧情和谜题脚本、角色、UI 都在游戏层，它们要大量调用 C# 写的适配层。整个项目用一种还是两种语言，要在开始写游戏代码之前定下来。

## Proposal

游戏层全部用 C#，包括剧情和谜题脚本、角色、UI 和在编辑器里运行的工具脚本。整个项目只有一套工具链，构建解决方案就能检查全部游戏代码；适配层的类型可以直接使用，有编译期类型检查。框架文档里「GDScript 或 C# 均可」改成只用 C#。

## Alternatives considered

**剧情和谜题脚本用 GDScript。** 改完马上生效，不用重新编译。但要维护两种语言，GDScript 调用 C# 适配层要经过 Godot 的跨语言调用，类型检查也弱；白模阶段改动最频繁的谜题条件，恰好要大量调用适配层。

## Acceptance criteria

- 游戏项目里没有 GDScript 文件。
- 框架文档已更新。

## Risks

- 改脚本要重新编译，迭代比 GDScript 慢。
