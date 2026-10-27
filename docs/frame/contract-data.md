# 实体数据与读取

[契约入口](contract.md) · [动作](contract-actions.md) · [场景标注](scene-authoring.md)

本页定义公共输入与状态。代码中的 `IReadOnlyList<T>` 表示有序只读集合；载入后由世界保存副本。所有定义都有 `Id`、`Pose`、`Source`，下列封闭变体外不接受新类别。

```csharp
public abstract record EntityDefinition(EntityId Id, Pose4 Pose, Source Source);
public sealed record BuildingElementDefinition(
    EntityId Id, Pose4 Pose, Source Source, BuildingId Building,
    Interval WRange, IReadOnlyList<Interval> Gaps, BoxAssembly Shape)
    : EntityDefinition(Id, Pose, Source);
public sealed record FixedDefinition(
    EntityId Id, Pose4 Pose, Source Source, SectionId Section, BoxAssembly Shape)
    : EntityDefinition(Id, Pose, Source);
public sealed record TurnableDefinition(
    EntityId Id, Pose4 Pose, Source Source, SectionId Section, BuildingId Building,
    BoxAssembly Shape, TurnableProperties Interaction)
    : EntityDefinition(Id, Pose, Source);
public sealed record ItemDefinition(
    EntityId Id, Pose4 Pose, Source Source, BuildingId Building, ItemShape Shape,
    TurnableProperties Interaction)
    : EntityDefinition(Id, Pose, Source);
public sealed record DeviceDefinition(
    EntityId Id, Pose4 Pose, Source Source, LoftShape Shape, DeviceMotion Motion)
    : EntityDefinition(Id, Pose, Source);
public sealed record Creature3Definition(
    EntityId Id, Pose4 Pose, Source Source, SectionId Section,
    double PassageRadius, double PassageHeight, bool CanUseSeamsWithoutPrism)
    : EntityDefinition(Id, Pose, Source);
public sealed record GrazerDefinition(
    EntityId Id, Pose4 Pose, Source Source, MetaballShape Shape)
    : EntityDefinition(Id, Pose, Source);
```

楼房元素只摆一次。`WRange` 和 `Gaps` 是世界 W 区间，缺口对该元素整个三维形状有效；局部地洞把地板拆成周边元素与有缺口的中间元素。缺口有序、内部不重叠、允许端点相接，且位于元素范围内。Interval仍表示闭数值范围；生成楼房实体时，先按缺口分割WRange，只保留有正W厚度的剩余段，再闭合每个剩余段，切片、碰撞与支撑统一使用这些闭段的并集。剩余端面属于实体，不是直接相减后留下的开边界。

例如WRange=[−1,1]、Gap=[0.3,0.6]生成实体段[−1,0.3]∪[0.6,1]：W=0.42为空，W=0.3与0.6各有地板端面切片。Gap等于整个WRange时实体为空，不生成孤立零厚端盖；Gap=[−1,0.6]时只生成[0.6,1]，不保留W=−1的孤立面。此规则只定义楼房沿W截取后的剩余端面，不增加通用几何布尔接口。

楼房元素姿态仅允许 XYZ 平移/旋转，W=0，沿世界 W 延伸；固定物和普通物件的初始 W 必须等于所属 Section，初始局部 W 轴平行世界 W 轴。普通物件初始镜像可由合法半圈旋转表示。

三维生物初始 W 等于 Section.W，PassageRadius>0 且 PassageHeight≥2×PassageRadius。黑猫设 `CanUseSeamsWithoutPrism=true`，人类为 false。生物没有可转交互属性，不参与物体的四维扫掠碰撞。

## 盒子、放样与四维形体

```csharp
public sealed record BoxAssembly(IReadOnlyList<BoxPart> Parts);
public sealed record BoxPart(
    PartId Id, Vec3 Center, Vec3 HalfSize, Rotation3 Rotation,
    IReadOnlyList<FaceUv> FaceUvs);
public readonly record struct Rotation3(Vec3 X, Vec3 Y, Vec3 Z);
public sealed record FaceUv(Axis3 Axis, bool Positive, Vec2 Scale, Vec2 Offset);
public sealed record TurnableProperties(
    bool Holdable, bool Packable, double HorizontalRadius,
    Axis3 LongAxis, Vec3 Hinge, double Reach);
public abstract record ItemShape;
public sealed record Sphere4(PartId Part, double Radius) : ItemShape;
public sealed record HiddenRingItem(
    BoxAssembly Body, Ring4 Ring) : ItemShape;
public sealed record Ring4(
    PartId Part, Vec4 Center, Rotation4 Rotation,
    double MajorRadius, double TubeRadius);
public sealed record LoftShape(IReadOnlyList<LoftSegment> Segments);
public sealed record LoftSegment(IReadOnlyList<LoftKey> Keys);
public sealed record LoftKey(double W, IReadOnlyList<ConvexPart> Parts);
public sealed record ConvexPart(
    PartId Id, IReadOnlyList<Vec3> Vertices, IReadOnlyList<ConvexFace> Faces);
public sealed record ConvexFace(IReadOnlyList<int> Vertices, FaceUv Uv);
public abstract record DeviceMotion;
public sealed record DeviceRotation(
    RotationPlane Plane, Vec4 Pivot, Interval Limits, double Initial)
    : DeviceMotion;
public sealed record DeviceTranslation(
    Axis3 Axis, Interval Limits, double Initial) : DeviceMotion;
public sealed record MetaballShape(
    IReadOnlyList<Metaball> Balls, double Threshold);
public sealed record Metaball(PartId Id, Vec4 Center, double Radius, double Weight);
```

`Rotation3` 为右手正交单位基，det3=+1。BoxPart 在局部 XYZ 中定义非退化盒子，各半长>0，局部 W∈[−ε,+ε]；普通本体由至少一个零件组成。楼房使用同样盒子横截面，但 W 范围来自元素标注。盒子六面各有一个 FaceUv；UV 规则见[截面输出](contract-views.md)。

可转交互的半径和 Reach>0；Reach 是铰链朝 LongAxis 正向到可抓末端的实际长度，由形状验证，不是任意传送距离。铰链位于长边下端向内至少 0.02m；面端距 Section 的 0.02m 检查对竖起后的实际各零件端面执行，包括原 Section。Reach 按实际铰链计算；例如总长0.48m、内缩0.02m得到Reach=0.46m，不能继续填写0.48m。`Packable` 表示能收入背包的小物；Sphere4四维对称物和竖起物永远不能放入背包。非对称HiddenRingItem按本体小件属性配置Packable，未竖起时可收入背包，不因其附有四维结构一律禁止；进入背包时整件形体随身收起。普通小物可以 Holdable=false，但此时不能请求玩家 F/R 操作。

球的局部定义是 `||p||≤Radius`，Radius>0。圆环中心圆位于 Ring4 自身局部 XY 平面：若 q 是逆变换后的四维点，形体为 `(sqrt(q.X²+q.Y²)−MajorRadius)²+q.Z²+q.W²≤TubeRadius²`。两半径>0且 TubeRadius<MajorRadius。隐藏圆环物品由普通本体和 Ring4 并集构成，随同一个实体姿态运动；PartId 在整个实体内唯一。本体的初始 Section 由初始姿态 W 对应 Section 决定，必须恰对应一个 Section。

球绕球心转动，Hinge 与 Reach 对它不起作用，载入只检查它们有限且 Reach>0；球的初始姿态可以是任意合法的四维旋转，W 不必等于某个 Section，但球的 W 范围 [W−Radius, W+Radius] 须在所属楼房内。球直径和圆环管径都须大于采样距离 0.01m。圆环按初始姿态校验：中心圆到每个 Section 的最近 W 距离不小于 TubeRadius+ε+0.02m，否则 HiddenStructureTooClose；所属楼房须包住向 +W、−W 两个方向竖起的全过程里本体和圆环所到的 W 范围，否则 BuildingCoverage。

放样包含至少一段 LoftSegment，每段至少两个严格递增 W 的关键形状，范围外为空；各段按 W 递增排列，W 内部不重叠，相邻段最多共享边界，并在该边界切换拓扑。物理形体、切片、碰撞与支撑统一取所有闭段的并集，刚好切在共享边界的 W 上时，两段的形状都在切片里，不删左段独有的端面；重叠的部分不合并，各自保留自己的零件号和 UV。零件号在整个装置内唯一，不同段之间也不重复，所以不需要右侧段优先来归属。每个零件至少四个非共面顶点，每面凸、闭合并朝外，每段每个零件的四维形体为非退化凸体；整个装置可由这些凸体组成非凸并集。相邻关键形状必须具有相同有序零件号、顶点数和面索引；每个对应顶点按 W 线性插值。为使每个 W 上的形状正是这样插出来的，且每块都是四维凸体，相邻关键形状中对应的两个面必须平行、朝向相同，对应面的顶点在四维里共处一个超平面（容差1e-6m，场景数据是单精度数）：盒子可以换大小、挪位置，不能在两个关键形状之间转个角度，旋转由装置的运动表达。违反时载入报 InvalidGeometry。段内有三个以上关键形状时，每相邻两个关键形状之间是一块凸体，整段是这些凸体的并集，共用的关键形状处只切一次；每块沿它每个面的法线都须比采样距离 0.01m 厚。需要拓扑变化时在分段边界换一组形状，同一个装置姿态和运动仍统一作用于全部段。每段内部不能增删零件。输出几何只移除同一零件在同一段内相邻两块凸体共用的关键形状面；同一关键形状里相贴的不同零件各自输出完整表面，它们相贴的面会保留，不同段之间重叠的面也不合并。不提供任意世界实体的通用布尔运算接口。零件 UV 在各关键形状中相同。装置仅一种运动自由度；Initial∈Limits，Limits 与 Initial 同 DriveDevice 的 target 用一个坐标：0 是 Shape 与 Pose 原样的状态。旋转 Pivot 与 Plane 属于装置初始局部坐标，运动以其为基准后再乘初始 Pose；平移沿初始姿态的 XYZ 轴，载入要求轨道方向与世界 X、Y、Z 轴之一重合（可反向），不带 W 分量（容差1e-6）。装置的初始 Pose 可以倾入 W，不要求平放。EntityState.Pose 是局部坐标系在当前位置的姿态，DevicePosition 是运动位置。

元球不参与场景阻挡碰撞，也不提供或接受四维支撑。元球用有限范围的场：`f(p)=Σ weight×max(0,1−||p−center||²/radius²)²`，实体是 `f(p)≥Threshold`。每个球只在自己的作用范围（以球心为中心、半径为 Radius 的四维球）里有贡献。Radius、Weight、Threshold>0 且有限，至少一个球；由参数确定有限包围范围。球心 `Center` 在啃食者的局部坐标里，世界球心是 `Pose.Position + Pose.Rotation × Center`；姿态可以是任意合法的四维旋转（包括倾入 W），半径和权重不随旋转变化。球 ID 在实体内非空且唯一，只用于编排匹配，引擎不要求相邻两帧的球集合相同。

形体非空按权重判断。场在一点的值不超过覆盖该点的各球的权重之和，而覆盖同一点的球，作用范围必然相交。所以对每个球，把它和所有与它作用范围相交（四维球心距小于半径之和）的球的权重加起来，若最大的和仍低于 Threshold，形体必然为空，载入报 InvalidGeometry，定位到 `Shape.Threshold`。这个检查只排除必然为空的形体；权重够、场值却凑不到阈值的形体可以通过，它的切片为空。张嘴动画通过更新球中心、半径/权重和阈值表达，不公开网格或内部采样缓存。多个球融合后的表面没有唯一“来源球”，因此切片零件号统一使用实体保留的 `PartId("body")`；输入球 ID 只用于稳定编排匹配。该场函数是已批准的契约约定，不从归档原型翻译。

## 接缝与实体状态

```csharp
public sealed record SeamDefinition(
    SeamId Id, Vec3 Position, Vec3 HorizontalNormal,
    IReadOnlyList<SectionId> Sections, bool Mirrored,
    bool Open, bool Severed, Source Source);
public enum Handedness { Normal, Mirrored, NotComparable }
public sealed record EntityState(
    EntityId Id, EntityDefinition Definition, Pose4 Pose,
    SectionId? Section, double? ActorYaw, SectionId? UprightOrigin,
    EntityId? Holder, bool InBackpack, IReadOnlyList<EntityId> CarriedItems,
    double? DevicePosition, bool Rotating, Handedness Handedness);
public sealed record SeamState(SeamId Id, bool Open, bool Severed);
public sealed record SharedValueState(SharedValueId Id, double Value);
```

接缝法线是 XYZ 内水平单位向量（Y=0）；它和世界 Y 定义竖直缝平面，位置在所有连接 Section 的 XYZ 相同。Sections 至少两个且无重复，列表顺序为 W 递增。普通缝只改变所在 Section；镜像缝还绕法线—W 平面转 π。接缝不作为阻挡实体，但通行空间要检查占据。

`EntityState.Section` 只对三维生物、未竖起的普通物及隐藏本体有意义；跨多个 Section 的四维球、装置、元球为 null。`UprightOrigin` 在竖起成功后直到原页放倒完成期间保存来源，旋转途中也保留正确来源；受控PlaceEntity布置按其来源转换表设置或清除，不从新姿态猜来源。`Definition` 是载入/添加时的不可变定义，当前姿态以 `Pose` 为准；物件当前所在的楼房是运行时状态，离开背包时会变，见[抓取与布置](contract-carry.md)；DevicePosition 仅装置有值。`Holder` 对背包和手持小物是持有人，对隔页抓取物也是抓取者；`InBackpack` 为 true 表示物件在 Holder 的背包里（没有切片，不阻挡，Pose 的 XYZ 没有意义，W 和旋转随穿缝更新），手里的、抓着的和世界里的物件都是 false。CarriedItems 仅列真正随身物（手里和背包里的），不包含已竖起的世界物。ActorYaw 仅三维生物有值，独立于四维旋转；Handedness 为引擎派生结果，算法见[查询](contract-views.md)。

```csharp
// Members of World.
public AddEntityResult AddEntity(EntityDefinition definition);
public RemoveEntityResult RemoveEntity(EntityId entity);
public EntityReadResult ReadEntity(EntityId entity);
public SeamReadResult ReadSeam(SeamId seam);
public SharedValueReadResult ReadSharedValue(SharedValueId value);
public abstract record AddEntityResult
{
    public sealed record Added(EntityState Entity) : AddEntityResult;
    public sealed record Rejected(Failure Failure) : AddEntityResult;
}
public abstract record RemoveEntityResult
{
    public sealed record Removed(EntityId Entity) : RemoveEntityResult;
    public sealed record Rejected(Failure Failure) : RemoveEntityResult;
}
public abstract record EntityReadResult
{
    public sealed record Found(EntityState Entity) : EntityReadResult;
    public sealed record Missing(EntityId Entity) : EntityReadResult;
}
public abstract record SeamReadResult
{
    public sealed record Found(SeamState Seam) : SeamReadResult;
    public sealed record Missing(SeamId Seam) : SeamReadResult;
}
public abstract record SharedValueReadResult
{
    public sealed record Found(SharedValueState Value) : SharedValueReadResult;
    public sealed record Missing(SharedValueId Value) : SharedValueReadResult;
}
```

动态添加接受 Turnable、Item、Device、Creature3、Grazer 五类，复用载入时的几何/引用/尺寸校验（Grazer 只校验它自己的 ID、姿态和形体，不涉及楼房）；Turnable/Item 的 Building 必须引用已载入楼房，正反竖起和隐藏结构范围也在添加前校验；静态楼房与固定物随世界载入。非法定义返回 InvalidInput，带 Source 的详情；已用 ID 返回 DuplicateId。成功后立即可读、可切片，事件由下一次 Step 交付。

RemoveEntity 只移除动态实体，Grazer 属于动态实体。不存在返回 UnknownId，静态类别返回 WrongEntityKind，正在旋转、被持有或携有物品时返回 EntityInUse；宿主先松手/放下并等动作结束。成功后切片和碰撞立即消失，其他实体的支撑在下一次 Step 重新求解。读取不存在的 ID 返回 Missing，不改变任何状态。

例：添加 R=0.25、Position.W=0.21 的球，在 W=0 和 0.42 得到相同球切片；把其 XYZ 平移 0.1m 后，两份切片都移动 0.1m；移除后两个截面的切片均不存在。
