# 区域场景标注与载入校验

[契约入口](contract.md) · [实体数据](contract-data.md) · [动作](contract-actions.md)

一个区域一个 Godot 场景；楼房元素只摆一次，固定物、可转物和出生点放在所属 Section 下。节点脚本在 `game/adapter/`，只把标注转换成引擎数据，不计算四维几何。本页约定完整格式；已实现 Region、Section、楼房元素、固定物件、可转物件、盒子零件、接缝、啃食者，以及玩家和角色的出生点、四维球、隐藏圆环物和放样装置，其余节点随对应能力实现。TurnableObject3D 和 CreatureSpawn3D 的 `Rotation4` 尚未导出，初始姿态只取 Transform；Grazer3D 的 `Rotation4` 已导出。

## 节点与导出属性

```text
Room (Node3D, Region3D)
  Sections (Node)
    I (Node3D, Section3D)
      Fixed (Node3D)
      Turnables (Node3D)
      Spawns (Node3D)
    II (Node3D, Section3D)
  Buildings (Node3D)
    Home (Node3D, Building3D)
      Floor (Node3D, BuildingElement3D)
        Part (MeshInstance3D, BoxPart3D)
  Items (Node3D)
  Devices (Node3D)
  Grazers (Node3D)
  Seams (Node3D)
  ExpectedPuzzleOperations (Node)
```

分组节点名称固定，内容节点名称可读即可，稳定 ID 来自属性。所有 Node3D 的 Scale=(1,1,1)，禁止父级缩放/剪切。实体 XYZ 用相对区域根的 Transform，Section 节点保持单位变换，不用其 Z 或视觉偏移表达 W。适配层把 Transform 的 XYZ 与旋转转成引擎值；需要初始四维旋转的动态实体另读 Rotation4 属性，不把两者重复相乘：Rotation4 为空时取 Transform 的旋转，非空时节点不得再带旋转，否则载入报错。

下表类型指 Godot 侧 C# 属性，每项均 `[Export]`。`A<T>` 在表中表示 `Godot.Collections.Array<T>`，不是待实现的新集合。Resource 自定义类均为 `partial class : Resource`、`[GlobalClass]`，只含表中数据。Godot 官方 [C# 导出属性](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_exports.html)要求 Variant-compatible 类型，支持 Resource 和类型化数组；不得直接导出引擎 record、IReadOnlyList 或嵌套普通 C# 集合。

| 脚本（原生类型） | 导出属性与对应含义 |
|---|---|
| Region3D（Node3D，`[Tool]`） | `double PreviewW`；`bool PrismEnabled`；`A<SharedValueResource> SharedValues` |
| Section3D（Node3D） | `string SectionId`、`double W` |
| Building3D（Node3D） | `string BuildingId`、`double WMin`、`double WMax` |
| BuildingElement3D（Node3D） | `string EntityId`、`string BuildingId`、`double WMin/WMax`、`A<IntervalResource> Gaps`；BoxPart3D 子节点给 Shape |
| FixedObject3D（Node3D） | `string EntityId`；BoxPart3D 子节点给 Shape；Section 从祖先取得 |
| TurnableObject3D（Node3D） | `string EntityId/BuildingId`、`double[] Rotation4`（尚未导出）、`TurnableResource Interaction`；Section 从祖先取得；节点原点放在握持点，平放物拿起时瞬移到手点；BoxPart3D 子节点给 Shape；Label3D 子节点是字样模板：物件平放时，适配层在它所在的截面里按物件当前姿态放一份副本，翻面后字样成镜像，转动途中和竖起时不显示 |
| SphereItem3D（Node3D） | `string EntityId/BuildingId`、`string PartId`、`double W`、`double[] Rotation4`、`double Radius`、`TurnableResource Interaction`、`Material Material`；原点是球心 |
| HiddenRingItem3D（Node3D） | `string EntityId/BuildingId`、`double W`、`double[] Rotation4`、`TurnableResource Interaction`、`RingResource Ring`；BoxPart3D 子节点给 Body；原点同可转物件，Ring 的 `Center` 和 `Rotation4` 取本体的局部坐标 |
| LoftDevice3D（Node3D） | `string EntityId`、`double W`、`double[] Rotation4`、`A<LoftSegmentResource> Segments`、`DeviceMotionResource Motion` |
| Grazer3D（Node3D） | `string EntityId`、`double W`、`double[] Rotation4`、`MetaballShapeResource Shape/OpenShape`；节点的 XYZ 是身体原点，旋转只取 `Rotation4`，不读节点自己的旋转；`Shape` 是闭嘴时的身体，`OpenShape` 是张到最大时的身体，球的 ID 和顺序必须与 `Shape` 相同，载入时用引擎在初始姿态上试一次，数值不合法报在 `OpenShape` 上，不填表示嘴不会张开；节点只给出第一帧，之后的移动、转身、张嘴和啃食由游戏层的时间轴（`GrazerTimeline`）按帧交给引擎 |
| CreatureSpawn3D（Node3D） | `string EntityId`、`double[] Rotation4`（尚未导出）、`double PassageRadius`、`double PassageHeight`、`bool CanUseSeamsWithoutPrism`、`PackedScene CharacterScene`、`string DisplayName`（台词前的名字，空则用角色场景自己的）；Section 来自祖先 |
| Character3D（CharacterBody3D） | 角色场景的根节点，游戏层节点，格式见[角色搭法](character-authoring.md) |
| Seam3D（Node3D） | `string SeamId`、`Vector3 HorizontalNormal`、`string[] Sections`、`bool Mirrored/Open/Severed`、`double VisualHeight`；Position 来自 Transform.Origin，原点放在地面；`Open` 默认 true，`VisualHeight` 默认 3 米，是镜片里细光的高度；Sections 按 W 从浅到深列出 |
| BoxPart3D（MeshInstance3D） | `string PartId`、`A<FaceUvResource> FaceUvs`；Mesh 必须为 BoxMesh，Size 与本地 Transform 给 HalfSize/Center/Rotation |
| ExpectedOperationNode（Node） | `ExpectedOperationResource Operation` |

视觉高度、角色 PackedScene、棱镜开关属于宿主，不进入四维引擎。每个出生点创建一个 Creature3Definition：位置取出生点的 XYZ，W 取所属 Section，四维旋转为单位旋转。出生点的 CharacterScene 决定它是谁：根节点是 FirstPersonPlayer 的是玩家，一个区域恰有一个；根节点是 Character3D 的是猫、老师、同桌等角色，数量不限。每个角色放在所属 Section 的物理世界里，位置和朝向取自出生点的 XYZ 变换，穿缝后由适配层移到目标 Section 的物理世界。适配层每个物理帧用 SynchronizeActor 把角色的位置和朝向交给引擎，不另存 W 或手性。玩家场景是 CharacterBody3D + CollisionShape3D + Camera3D，猫和其他角色不需要 Camera3D。角色普通 yaw 由出生点 XYZ 朝向取得，四维旋转另存。

| Resource | 全部导出字段 |
|---|---|
| IntervalResource | `double Min/Max` |
| SharedValueResource | `string Id`、`double Min/Max/InitialValue` |
| TurnableResource | `bool Holdable/Packable`、`double HorizontalRadius`、`Axis3 LongAxis`、`Vector3 Hinge`、`double Reach`；Hinge内缩≥0.02m，Reach按该铰链核算 |
| FaceUvResource | `Axis3 Axis`、`bool Positive`、`Vector2 Scale/Offset` |
| RingResource | `string PartId`、`double[] Center`、`double[] Rotation4`、`double MajorRadius/TubeRadius`、`Material Material` |
| LoftSegmentResource | `A<LoftKeyResource> Keys` |
| LoftKeyResource | `double W`、`A<ConvexPartResource> Parts` |
| ConvexPartResource | `string PartId`、`Vector3[] Vertices`、`A<ConvexFaceResource> Faces`、`Material Material` |
| ConvexFaceResource | `int[] Vertices`、`FaceUvResource Uv` |
| DeviceRotationResource（DeviceMotionResource 变体） | `RotationPlane Plane`、`double[] Pivot`、`double Min/Max/Initial` |
| DeviceTranslationResource（DeviceMotionResource 变体） | `Axis3 Axis`、`double Min/Max/Initial` |
| MetaballShapeResource | `A<MetaballResource> Balls`、`double Threshold` |
| MetaballResource | `string Id`、`double[] Center`、`double Radius/Weight` |

double[] 四维点长度为 4，顺序 XYZW；Rotation4 长度为 16，按 X/Y/Z/W 四列，每列 XYZW；空数组只表示单位旋转（节点上的 Rotation4 为空时取节点 Transform 的旋转，见上），四维点不可用空数组。适配层只做布局转换与基础格式校验，旋转是否合法交给引擎。所有数组默认空集合，必需的 Resource 未赋值报告节点属性错误。未知脚本节点不静默当四维实体。

测试区域为 `game/regions/section_test.tscn`，可直接运行；`game/main.tscn` 实例化它。`game/regions/item_test.tscn` 放四维球、螺母、吊柜和推拉窗。当前阶段一个区域只放一个玩家出生点。楼房白模材质使用 StandardMaterial3D，镜片沿用材质，并以实体遮罩绘制纯色轮廓；球、圆环和放样零件没有网格，着色取各自的 `Material`，缺省用默认材质；其他着色材质留到表现层工作。

啃食者的测试区域为 `game/regions/grazer_test.tscn`：三个截面，啃食者从 W=11 的深处出发。`game/tests/grazer_demo.tscn` 让它沿时间轴逼近，啃掉一扇门和一道缝，音乐和画面闪烁随它与玩家的四维距离变化（调试启动 `--start=grazer-demo`）。

使用时间轴的两条约定：要啃的楼房元素必须写在时间轴的“咬”里，不能直接调 `ConsumeStructure`，否则引擎里没了，Godot 里节点还在，看得见也撞得到；分段 `Play` 时，后一段的第一个关键帧要接上前一段的最后一个，因为 `Play` 会让啃食者立刻跳到新时间轴的第一个关键帧。

`[Tool]` 的 PreviewW 变动时调用 GetGeometryAtW，只显示该 W 的生成切片；不由工具脚本重算 W 区间、球半径或放样。表现层灯光/贴花仍由宿主管理。白模 debug 游戏可以用滚轮选择任意已配置 Section；连续 W 查看是同一只读引擎入口。

放样装置里各截面的零件怎么搭，见[装置搭法](device-authoring.md)；角色场景的格式、路线和抱猫，见[角色搭法](character-authoring.md)。

## 独立前置布置与预期操作

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

前置布置资源的字段如下。ID使用string，枚举原样，四维值按本页数组格式；资源读取后生成完整引擎定义。

| Resource | 全部导出字段与含义 |
|---|---|
| ExpectedOperationResource | `string Id`、`ExpectedSetupResource Setup`、`A<ExpectedStepResource> Steps` |
| ExpectedSetupResource | `A<EntityOverrideResource> EntityOverrides`、`A<SeamOverrideResource> Seams`、`A<SharedValueOverrideResource> SharedValues`、`A<CarrySetupResource> Carry` |
| EntityOverrideResource | `NodePath Entity`引用现有动态实体模板；`Vector3 Position`、`double W`、`double[] Rotation4`、`double DeviceInitial`只覆盖适用字段，其余（含BuildingId）沿用完整模板定义 |
| SeamOverrideResource | `string Id`、`bool Open/Severed` |
| SharedValueOverrideResource | `string Id`、`double Value` |
| CarrySetupResource | `string Actor/Item`、`CarryLocation Location` |

ExpectedStepResource是封闭抽象Resource，各变体如下。

| Resource | 全部导出字段 |
|---|---|
| ExpectedRotationResource | `ExpectedRotationKind Kind`（StandUp/LayDown/Flip）、`string Actor/Entity/Target`、`bool ShouldFail`、`FailureCode Failure` |
| ExpectedMoveResource | `string Entity`、`Vector3 Delta/ExpectedApplied`、`bool ShouldFail`、`FailureCode Failure` |
| ExpectedDeviceResource | `string Device`、`double Target/ExpectedPosition`、`bool ShouldFail`、`FailureCode Failure` |
| ExpectedCrossingResource | `string Actor/Seam/Target`、`bool HasPrism`、`int FreeBackpackSlots`、`bool ShouldFail`、`FailureCode Failure` |
| ExpectedAdvanceResource | `double Delta` |

ShouldFail=false时忽略Failure；StandUp之外Target必须为空，防止无效设置掩盖错误。

至少配置学校铅笔原页放倒后关窗，以及房间逗猫棒越栏、正反竖起、隐藏圆环在预期空地竖起等解法。墙根受阻可以作为 ExpectedFailure=RotationBlocked 的独立教学反例，不把它当设计错误。前置布置明确角色站位和朝向，不能只写“螺母能转”。

## Load 诊断与 debug 展示

```csharp
public enum LoadErrorCode
{
    InvalidNumber, InvalidId, DuplicateId, MissingReference,
    InvalidSectionOrder, InvalidInterval, InvalidPose, InvalidGeometry,
    InvalidInteraction, InvalidMotion, InvalidCarry,
    PartBoundaryTooClose, HiddenStructureTooClose,
    BuildingCoverage, BuildingInterference, SeamTooClose,
    ExpectedOperationFailed, UnsupportedDefinition
}
public sealed record LoadError(
    LoadErrorCode Code, Source Source, EntityId? Entity,
    OperationId? Operation, int? StepIndex, string Detail,
    IReadOnlyList<Contact> Contacts);
```

| 校验 | 错误与边界 |
|---|---|
| 非有限数、范围/长度/旋转、最薄实体厚度 | InvalidNumber/InvalidPose；ε 固定 0.006m，采样固定 0.01m 且仍小于最薄实体；本轮时长按 #6 的四个值 |
| 空/重复 ID、失效引用、Section W 非递增 | InvalidId/DuplicateId/MissingReference/InvalidSectionOrder |
| 接缝的位置或法线不是有限数；法线不是水平单位向量；Sections 少于两个、重复、不按 W 递增或引用不存在的截面 | InvalidNumber/InvalidGeometry/DuplicateId/InvalidSectionOrder/MissingReference；错误指向 `Position`、`HorizontalNormal`、`Sections` 字段 |
| 当前实现尚未支持的定义 | UnsupportedDefinition；不忽略未实现的数据。当前世界接受楼房元素、固定物、可转物、四维物品、三维生物、啃食者、接缝和装置；SharedValues 必须为空集合，否则返回该错误；预期操作接受 ExpectedRotation、ExpectedCrossing、ExpectedDevice 和 ExpectedAdvance 四种步骤，Setup 里的 SharedValues 必须为空，EntityOverrides 只接受同类的可转物、四维物品、三维生物和装置；后续任务实现后逐项开放 |
| 空或逆向区间、缺口越界/内部重叠 | InvalidInterval；缺口允许端点相接。楼房按缺口分割后，仅保留正W厚度的剩余段并逐段闭合，切片/碰撞/支撑使用同一实体。缺口覆盖全范围时允许空实体，不生成孤立零厚端盖；不同形状用独立元素表示，各自使用闭 W 区间；共享边界同时保留两者的切片，按实体与零件 ID 稳定排序。若某个可玩 Section 只应显示一种形状，把换形边界放在两个 Section 之间 |
| 派生几何不可表示 | 世界变换后的顶点、法线和 UV 必须有限，否则返回 InvalidNumber；在当前 double 精度下，盒子须保留三维体积，输出三角形须有非零面积及正确绕序，否则返回 InvalidGeometry。固定物的 W−ε 与 W+ε 须真正夹住 W，且厚度大于采样距离；报错 Source 定位零件、FaceUvs 或 Pose.Position.W。不设统一坐标上限 |
| 盒子退化、放样不凸/拓扑不对应、元球空形、圆环参数非法 | InvalidGeometry，精确定位零件或关键形状。放样的零件还须是闭合、朝外的凸体；相邻关键形状的对应面须平行、朝向相同，对应面的顶点须在四维里共面；每块比采样距离薄也报此错 |
| 元球：没有球；球 ID 为空或重复；球心、半径、权重或阈值不是有限数；半径、权重或阈值不为正；球的权重凑不够阈值 | InvalidGeometry/InvalidId/DuplicateId/InvalidNumber，错误指向 `Shape.Balls[i]` 的 `Id`、`Center`、`Radius`、`Weight`，或 `Shape.Threshold`、`Shape.Balls`；啃食者的姿态按任意合法四维旋转检查，不要求平放 |
| 放样的关键形状 W 不递增、段与段重叠 | InvalidInterval；零件号重复为 DuplicateId |
| 可转物交互参数/装置轨道非法、携带冲突 | InvalidInteraction/InvalidMotion/InvalidCarry。装置的 InvalidMotion：未定义的平面或轴、限位长度不为正、初始位置越限、轨道不与世界 X、Y、Z 轴之一重合 |
| 正反竖起完成后零件端面距任一可玩 Section（含原Section）小于 0.02m | PartBoundaryTooClose；等于 0.02m 通过，原Section不设ε豁免；总长不足以让铰链两侧都留0.02m时同样报错 |
| 静止隐藏结构距任一 Section 不足“管半径+ε+0.02m” | HiddenStructureTooClose；圆环中心圆最近 W 距离与该下限比较，不把管半径重复扣两次 |
| 所属楼房不能包住本区域内物品正反竖起及隐藏结构的完整活动范围 | BuildingCoverage；物品节点 BuildingId 必填，适配层直接填写TurnableDefinition/ItemDefinition.Building，不按XYZ范围猜测归属 |
| 活动范围会触及另一栋楼 | BuildingInterference |
| 共享某 Section 的两缝水平距离<0.8m | SeamTooClose，等于0.8m通过；仅 W 不同不能抵消此错误；错误指向后一道缝的 `Position` |
| 独立预期操作的返回值/实际位移/碰撞或终态不符合声明 | ExpectedOperationFailed，含 Operation 与从0开始的 StepIndex、实际 Failure/Contacts |

安全距离按实际铰链和全部零件变换计算，包含隐藏结构；浮点计算只允许1e-10m的数值舍入余量，不能用0.005m碰撞容差放宽0.02m安全规则。

数值校验在所有构建都返回 LoadError；debug 适配层加载失败时停止区域运行，逐条输出 `ERROR: <Code> <NodePath>.<Property> <Entity> <Operation>[<StepIndex>]: <Detail>`，并在调试文字面板显示同一列表，方便在编辑器定位。release 也不接收非法世界，显示关卡载入失败；诊断文本不作为游戏台词。

基础格式失败时跳过依赖该数据的几何校验，避免把缺失字段当0产生连锁错误，其余独立项继续。错误按 NodePath、Property、Code、Entity、Operation、StepIndex 字典序返回。Contact 含碰撞实体/零件与四维点，错误 Source 指向动作节点或具体导出属性。

出入口净空：引擎用一个方盒代替角色的胶囊检测缝口两端的占据。以角色站位为中心，沿世界 X 和 Z 各 ±PassageRadius 的方形范围内，从脚到 PassageHeight，W 方向 ±ε，不能有东西。所以这个方形里的任何东西（包括胶囊碰不到的方形角落、坡道和门槛）都会挡住穿缝，缝口地面要平。

验例：同一 Section 缝距 0.79m 返回 SeamTooClose，0.80m通过；球 R=0.25、中心 W=0.21、两 Section W=0/0.42 保持合法；原型的0.71m缝距和不足的负W楼房余量不能直接复制进正式场景。Load 验证使用世界副本，不消耗正式世界事件，不将谜题解法执行后的状态带入初始游戏。
