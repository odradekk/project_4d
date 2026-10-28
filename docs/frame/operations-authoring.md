# 预期操作

[场景标注](scene-authoring.md) · [契约入口](contract.md) · [动作](contract-actions.md)

谜题的解法可以写进关卡：载入区域时，引擎在世界的副本上把每一条解法走一遍，结果和写的不同就拒绝载入。节点放在区域的 `ExpectedPuzzleOperations` 下，格式如下。例子：[家的区域](home-region.md)的 `game/regions/room/operations.tscn`。

```csharp
public sealed record ExpectedOperation(
    OperationId Id, Source Source, ExpectedSetup Setup,
    IReadOnlyList<ExpectedStep> Steps);
public sealed record ExpectedSetup(
    IReadOnlyList<EntityDefinition> EntityOverrides,
    IReadOnlyList<SeamOverride> Seams,
    IReadOnlyList<SharedValueOverride> SharedValues,
    IReadOnlyList<CarrySetup> Carry);
public sealed record SeamOverride(SeamId Id, bool Open, bool Severed);
public sealed record SharedValueOverride(SharedValueId Id, double Value);
public enum CarryLocation { Hand, Backpack }
public sealed record CarrySetup(EntityId Actor, EntityId Item, CarryLocation Location);
public enum ExpectedRotationKind { StandUp, LayDown, Flip }
public abstract record ExpectedStep;
public sealed record ExpectedRotation(RotationRequest Request,
    FailureCode? ExpectedFailure) : ExpectedStep;
public sealed record ExpectedMove(MoveRequest Request, Vec3 ExpectedApplied,
    FailureCode? ExpectedFailure) : ExpectedStep;
public sealed record ExpectedDevice(EntityId Device, double Target,
    double ExpectedPosition, FailureCode? ExpectedFailure) : ExpectedStep;
public sealed record ExpectedCrossing(CrossSeamRequest Request,
    FailureCode? ExpectedFailure) : ExpectedStep;
public sealed record ExpectedAdvance(double Delta) : ExpectedStep;
```

每个 ExpectedOperation 从原始世界的独立副本开始，不沿用前一谜题的状态。EntityOverrides 以完整实体定义替换同 ID 动态实体初态，类别和身份不变；Seams/SharedValues 按 ID 替换指定初值（接缝的 Open 和 Severed 一起替换），其余沿用载入数据；Carry 建立初始手持/背包关系，手里按 HoldEntity，背包按 PackEntity，世界不接受就报 InvalidCarry。覆盖后仍执行形状和尺寸检查。可用 EntityOverrides 预置角色站位、物品位置、装置位置，步骤表示真实先后顺序。装置只能被同一实体 ID 的 DeviceDefinition 替换；适配层把 DeviceInitial 写进替换定义的 Motion.Initial，替换后的限位和初始位置照常校验，错误带上 Operation。

ExpectedFailure=null 表示必须成功；非 null 必须得到该失败且世界不变。旋转成功步骤立即 Start 后按配置时长推进到结束，再进入下一步；ExpectedAdvance 用于显式模拟额外时间。Move/Device 同时核对实际位移/位置，比较容差 `1e-9m`（旋转位置为 `1e-9rad`）。装置步骤即时完成，不推进时间；ExpectedFailure=null 时装置的实际位置须等于 ExpectedPosition，被障碍挡在 target 之前停下也算成功，位置就是停下处；ExpectedFailure 非 null 时不比较 ExpectedPosition。此容差只核对预期值，不取代碰撞容差。所有可推导错误均一次收集；步骤失败后停止该序列，继续其他独立操作。

前置布置资源的字段如下。ID使用string，枚举原样，四维值按[场景标注](scene-authoring.md)的数组格式；资源读取后生成完整引擎定义。`Entity` 路径从区域根起算。适配层已读 EntityOverrides、Carry，以及旋转、装置、穿缝三种步骤，其余资源要用时再做。

| Resource | 全部导出字段与含义 |
|---|---|
| ExpectedOperationResource | `string Id`、`ExpectedSetupResource Setup`、`A<ExpectedStepResource> Steps` |
| ExpectedSetupResource | `A<EntityOverrideResource> EntityOverrides`、`A<SeamOverrideResource> Seams`、`A<SharedValueOverrideResource> SharedValues`、`A<CarrySetupResource> Carry` |
| EntityOverrideResource | `NodePath Entity`引用现有动态实体模板；`Vector3 Position`、`double W`、`double[] Rotation4`、`double DeviceInitial`只覆盖适用字段，其余（含BuildingId）沿用完整模板定义 |
| SeamOverrideResource | `string Id`、`bool Open/Severed` |
| SharedValueOverrideResource | `string Id`、`double Value` |
| CarrySetupResource | `string Actor/Item`、`CarryLocation Location` |

ExpectedStepResource是封闭的基类Resource，不用抽象类（原因见 `game/adapter/ExpectedStepResource.cs` 的注释），各变体如下。

| Resource | 全部导出字段 |
|---|---|
| ExpectedRotationResource | `ExpectedRotationKind Kind`（StandUp/LayDown/Flip）、`string Actor/Entity/Target`、`bool ShouldFail`、`FailureCode Failure` |
| ExpectedMoveResource | `string Entity`、`Vector3 Delta/ExpectedApplied`、`bool ShouldFail`、`FailureCode Failure` |
| ExpectedDeviceResource | `string Device`、`double Target/ExpectedPosition`、`bool ShouldFail`、`FailureCode Failure` |
| ExpectedCrossingResource | `string Actor/Seam/Target`、`bool HasPrism`、`int FreeBackpackSlots`、`bool ShouldFail`、`FailureCode Failure` |
| ExpectedAdvanceResource | `double Delta` |

ShouldFail=false时忽略Failure；StandUp之外Target必须为空，防止无效设置掩盖错误。读不出的步骤和多给的 Target，报在节点的 `Operation.Steps[i]` 上。

至少配置学校铅笔原页放倒后关窗，以及房间逗猫棒越栏、正反竖起、隐藏圆环在预期空地竖起等解法。墙根受阻可以作为 ExpectedFailure=RotationBlocked 的独立教学反例，不把它当设计错误。前置布置明确角色站位和朝向，不能只写“螺母能转”。
