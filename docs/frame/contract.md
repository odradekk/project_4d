# 四维引擎公共契约

[框架目录](README.md) · [数据](contract-data.md) · [动作与推进](contract-actions.md) · [截面与查询](contract-views.md) · [场景标注与载入校验](scene-authoring.md)

> 状态：待用户批准。本文定义 #2 的可审阅契约，不表示已有实现。批准后，各实现以这些类型、结果和实例为测试依据。存档只约定签名，白模阶段不实现保存格式。

## 入口与边界

调用者只持有一个具体的 `World`。下列七组方法属于同一个类，按调用任务命名；不建立七个转发接口，也不使用通用命令或泛型查询协议。适配层从 Godot 场景建立输入，输入与返回值仅使用 `FourD` 自有类型。纯进程内测试直接运行真实 `World`。

| 分组 | 方法所在页 |
|---|---|
| 世界 | 本页的 `Load` |
| 实体 | [数据](contract-data.md)的 `AddEntity`、`RemoveEntity`、`ReadEntity` |
| 动作 | [动作与推进](contract-actions.md)的规划、启动、平移、装置、穿缝、共享量与编排 |
| 推进 | [动作与推进](contract-actions.md)的 `Step` |
| 截面 | [截面与查询](contract-views.md)的切片、预览与隐藏轮廓 |
| 查询 | [截面与查询](contract-views.md)的支撑、占据、存在、距离、手性等 |
| 存档 | 本页的 `Capture`、`Restore`，仅签名 |

```csharp
public sealed class World
{
    public static LoadResult Load(WorldDescription description);
    public WorldState Capture();
    public RestoreResult Restore(WorldState state);
}
public abstract record LoadResult
{
    public sealed record Loaded(World World) : LoadResult;
    public sealed record Invalid(IReadOnlyList<LoadError> Errors) : LoadResult;
}
public sealed class WorldState
{
    private WorldState() { } // Engine-created opaque payload.
}
public abstract record RestoreResult
{
    public sealed record Restored : RestoreResult;
    public sealed record Rejected(Failure Failure) : RestoreResult;
}
```

签名用 C# 表达数据结构；省略的构造函数与访问器不是要求生成代码。`WorldState` 由引擎创建，宿主整体传递。序列化、版本、兼容性和恢复错误细分留到存档任务确定，本轮不能据此写磁盘格式。

`Load` 校验完整世界及预期操作，全部通过才创建世界。失败返回全部可独立判断的错误，不产生可使用的半成品；错误列表顺序见[场景标注](scene-authoring.md)。载入成功时实体处于声明的初始状态，无待交付事件，时间为 0。

## 公共值

```csharp
public readonly record struct EntityId(string Value);
public readonly record struct PartId(string Value);
public readonly record struct SectionId(string Value);
public readonly record struct BuildingId(string Value);
public readonly record struct SeamId(string Value);
public readonly record struct SharedValueId(string Value);
public readonly record struct OperationId(string Value);
public readonly record struct Vec2(double X, double Y);
public readonly record struct Vec3(double X, double Y, double Z);
public readonly record struct Vec4(double X, double Y, double Z, double W);
public readonly record struct Rotation4(Vec4 X, Vec4 Y, Vec4 Z, Vec4 W);
public readonly record struct Pose4(Vec4 Position, Rotation4 Rotation);
public readonly record struct Interval(double Min, double Max);
public readonly record struct Source(string NodePath, string Property);
public enum Axis3 { X, Y, Z }
public enum RotationPlane { XY, XZ, XW, YZ, YW, ZW }
```

ID 为非空、区分大小写的稳定字符串；实体 ID 在整个区域唯一，零件 ID 在所属实体内唯一，移除后不复用实体 ID。场景存档与脚本按 ID 引用，不按节点遍历顺序编号。`Source` 是诊断位置的纯文本，不是 Godot 节点引用。

所有输入数值有限。长度以米、角度以弧度、时间以秒；x、y、z 与 Godot 一致，Y 向上；W 增大表示更深。`Rotation4` 的四列是局部轴在世界坐标中的方向，正交、单位长、det4=+1，允许正交误差 `1e-10`。`Pose4` 把局部点变成 `Position + Rotation × point`。正角从平面的第一个轴转向第二个轴，例如 XW 的 +π/2 把 +X 转为 +W。`Interval` 为闭区间且 Min<Max；允许单点范围的装置当前位置另用标量表达。

运行时编排旋转使用引擎值类型的唯一具名工厂；初始场景的16元素矩阵仍是纯数据输入。

```csharp
public readonly record struct PlaneAngle(RotationPlane Plane, double Angle);
// Static member of Rotation4; signature only.
public static Rotation4 FromPlaneAngles(IReadOnlyList<PlaneAngle> rotations);
```

FromPlaneAngles 从单位矩阵开始，按列表顺序将各世界坐标平面的旋转左乘当前矩阵；例如列表[XW(a),YZ(b)]得到 R_YZ(b)×R_XW(a)。空列表得到单位矩阵；允许同一平面重复出现，保持给定顺序。角度必须有限，不要求限制在一周内；未知平面或非有限角度抛 ArgumentOutOfRangeException，null 集合抛 ArgumentNullException。该纯计算不读写 World、不改变修订，不由宿主实现正弦矩阵或矩阵插值。返回值保持正交且det4=+1，使用本页相同的数值误差约束。

姿态、旋转、有限数值、引用等在载入和写调用边界检查；校验通过的内部状态不重复验证。几何模型和外部输入必须符合各页约束，空集合只在该类别确实允许时有效。

```csharp
public sealed record WorldDescription(
    IReadOnlyList<SectionDefinition> Sections,
    IReadOnlyList<BuildingDefinition> Buildings,
    IReadOnlyList<EntityDefinition> Entities,
    IReadOnlyList<SeamDefinition> Seams,
    IReadOnlyList<SharedValueDefinition> SharedValues,
    IReadOnlyList<ExpectedOperation> ExpectedOperations);
public sealed record SectionDefinition(SectionId Id, double W, Source Source);
public sealed record BuildingDefinition(BuildingId Id, Interval WRange, Source Source);
public sealed record SharedValueDefinition(
    SharedValueId Id, Interval Range, double InitialValue, Source Source);
```

区域至少有一个 Section；W 各不相同，Sections 按 W 严格递增。世界不设统一截面数量或间距。楼房范围是其所有元素的外层范围。每个共享量在自己的闭区间内，更新影响引用它的装置/表现；不用四维水体积推算共享水位。

本轮白模的规则由引擎内部常量固定：ε=0.006m、碰撞容差=0.005m、扫掠采样距离=0.01m；竖起/放倒/原地翻面/绕障碍翻面时长依次为 1.4/1.0/1.2/1.4s。采样仍须严格小于场景最薄实体厚度，内容违反该条件时载入失败，不通过放宽配置绕过。没有当前需要逐区域修改这些值的调用，因此公共描述不设数值配置对象。

慢起步角固定为 π/36（5°），消耗总时长的 0.25；这两个手感参数是待批准的白模初值。TurnableDefinition 和 ItemDefinition 直接携带所属 BuildingId，载入与动态添加都由同一份定义校验范围，禁止从相交范围猜归属。

铰链在长边下端向内至少 0.02m，竖起后源 Section 和其余 Section 统一保持至少 0.02m 的零件端面间距，不设 ε 豁免。此项为 2026-10-26 用户已定；有效伸出长度按实际铰链计算。其余硬性尺寸见[尺寸规范](rules.md#尺寸规范)。

## 调用顺序、状态所有权与失败

宿主在同一线程上有序调用世界；世界不回调宿主、不自行计时。相同初始描述、相同配置和相同有序调用序列，在相同引擎版本下产生相同结果。输入集合和其嵌套数组在接受时复制；返回集合是独立稳定的只读快照，后续写调用不能改变先前结果，调用者也不能通过结果改写世界。

实体定义描述初始形体，实体状态描述当前事实。PlaceEntity提供受控布置的完整pose终态写入，属于实体读写职责，不代替玩家动作。姿态、竖起来源、动作进度、接缝状态、共享量由引擎独占。宿主只同步三维生物的当前位置/朝向，以及四维生物编排输入；不能用任意姿态 setter 改写角色 W 或镜像状态。

每次成功写调用（即使目标值等于当前值）和每次成功 `Step` 都增加世界修订号；读调用、规划、失败调用不增加。拒绝保持世界、事件队列和修订号全部不变。允许部分位移是明确返回的成功结果，不属于拒绝。

```csharp
public enum FailureCode
{
    InvalidInput, UnknownId, DuplicateId, WrongEntityKind, Busy,
    NoSlice, WrongSection, NotHoldable, NotPackable, CapacityExceeded,
    RequiresPrism, SeamClosed, SeamSevered, NotConnected,
    PassageOccupied, RotationBlocked, TranslationBlocked,
    OutOfRange, StalePlan, ForeignPlan, EntityInUse
}
public sealed record Contact(
    EntityId Moving, PartId MovingPart, EntityId Obstacle, PartId ObstaclePart,
    Vec4 Point, Vec4 Normal, SectionId? Section);
public sealed record Failure(
    FailureCode Code, string Detail,
    EntityId? Entity, SeamId? Seam, IReadOnlyList<Contact> Contacts);
```

`Detail` 是开发诊断文本；游戏根据 Code、实体与碰撞信息选择中文台词，不解析文本。碰撞点是世界四维坐标，法线由障碍指向运动物；点若恰在可玩 Section 上附该 ID，否则 Section=null，表示夹层，不伪称另一页。多接触按运动进度、障碍 EntityId、零件号排序；同一进度多接触全部保留。以最早接触解释拒绝并标红后续预览。距离不超过容差的表面相贴不算阻挡；碰撞查询排除运动物自身与三维生物，不整体排除最初支撑面，只忽略其相贴接触。

无 ID、非法数字、越界目标等是明确失败；不要求调用者捕获异常来处理正常受阻。进程资源耗尽等运行环境异常不属于玩法结果。

## 待批准的契约选择

这些选择补足设计未写明的边界，随本契约一起交用户批准：

- 手性使用局部 XYZ 有序基在 Section XYZ 坐标中的投影定向；退化与无手性形体不可比较，详见[查询](contract-views.md)。不新增镜像布尔状态。
- 镜像缝遇到长物时，先把长物变为留在出发截面的贯穿物，再翻转角色与真正随身小物，详见[穿缝](contract-actions.md)。这解释了原文“手持物同步翻转”和长物卡缝的交集。
- 计划绑定世界及修订；成功写调用和推进使它过期，执行不能自行换路径。
- 白模啃食只移除预先编排的完整楼房元素并咬断指定缝，不提供实时布尔减法接口。
- 曲面数学定义与三角网格近似分开；网格采用固定离散规则，不承诺未证明的全局误差上界。具体四维元球场函数见[数据](contract-data.md)。
