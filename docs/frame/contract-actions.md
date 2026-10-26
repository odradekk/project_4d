# 动作与推进契约

[契约入口](contract.md) · [实体数据](contract-data.md) · [几何与查询](contract-views.md)

以下方法都是 `World` 成员。写调用按[共同规则](contract.md#调用顺序状态所有权与失败)原子接受或拒绝；事件在下一次成功 `Step` 返回，不在写调用和 Step 各返回一遍。

## 翻转规划、预览与执行

```csharp
public abstract record RotationRequest(EntityId Actor, EntityId Entity);
public sealed record StandUp(EntityId Actor, EntityId Entity, SectionId Target)
    : RotationRequest(Actor, Entity);
public sealed record LayDown(EntityId Actor, EntityId Entity)
    : RotationRequest(Actor, Entity);
public sealed record Flip(EntityId Actor, EntityId Entity)
    : RotationRequest(Actor, Entity);
public sealed class RotationPlan
{
    private RotationPlan() { } // World creates this immutable opaque value.
}
public abstract record RotationPlanResult
{
    public sealed record Allowed(RotationPlan Plan) : RotationPlanResult;
    public sealed record Blocked(RotationPlan Plan, Failure Failure) : RotationPlanResult;
    public sealed record Rejected(Failure Failure) : RotationPlanResult;
}
public abstract record StartRotationResult
{
    public sealed record Started(EntityId Entity, double Duration) : StartRotationResult;
    public sealed record Rejected(Failure Failure) : StartRotationResult;
}
public RotationPlanResult PlanRotation(RotationRequest request);
public StartRotationResult StartRotation(RotationPlan plan);
```

PlanRotation 是只读。调用者提供角色和物体 ID，角色当前 Section/位置、物体姿态及障碍均从世界读取，不能在请求中伪造。目标必须在角色当前 Section 有切片并可操作；固定物、装置和生物返回 WrongEntityKind；不可拿物返回 NotHoldable；正在旋转返回 Busy。StandUp 的目标 Section 必须存在且不同于角色当前 Section；较深转 +W，较浅转 −W。已竖起的物体再次竖起无效；LayDown 仅在 UprightOrigin 执行，否则 WrongSection。Flip 对已竖起物返回 InvalidInput，先放倒后翻面。

F 的铰链和长轴来自物体定义；R 的翻转面由引擎选：角色与目标间若有挡住其连线的竖直建筑/固定物面，选距离角色最近的一面；同距按实体 ID、零件 ID 排序。没有该面则使用通过物体中心、水平法线朝向角色的竖直面；角色水平位置与中心重合时用物体局部长轴的水平投影，仍退化则 +X。障碍绕行与原地翻面均转 π；两种方向等价时固定为正角。四维球绕自身中心旋转，成功且外观不变，不令球绕侧面的障碍产生位移。

规划检查整段轨迹，包括夹层、隐藏零件与终点。采样按每一步最远表面点移动不超过 0.01m 自适应划分，不把角度当作米。Blocked 仍含同一条轨迹，供绘制红色预览；Rejected 表示请求本身无有效轨迹。Start 对 Blocked 计划返回 RotationBlocked，不推进一点再停。可用计划在启动时必须仍属于同一 World 与修订，分别以 ForeignPlan/StalePlan 拒绝；不能悄悄重算另一条路径。

预览可在输入未变时缓存。点击时先重新 PlanRotation，再立即 StartRotation；中间不调用写操作。开始后引擎持有轨迹，后续 Step 推进；宿主不能直接改该实体。曲线前5°消耗总时长的25%，两段各匀速、角度连续；后段到达精确终点，跨过结束点的余下 delta 用于后续支撑求解。新进入轨迹的动态障碍不改变已接受路径：动作进行中，其他移动/装置写调用也必须把正在执行的扫掠视为保留空间并拒绝相交请求。

手算：总长0.48m、长轴+X、铰链向内0.02m，从铰链量起的末端点为(0.46,0,0,0)；XW转+π/2后变为(0,0,0,0.46)。来源W=0、目标W=0.42，完整长轴范围由[0,0.48]变为W∈[−0.02,0.46]；原Section距最近端面0.02m，目标距末端0.04m，均通过；相较原型6mm内缩，有效伸出少0.014m但仍够得到目标。总长0.20m、同样铰链时只能伸出0.18m，不能卡缝到Ⅱ。原页 LayDown 使用精确逆变换，恢复原姿态与位置，不累加另一个近似 90°。

## 抓取、携带和平移

```csharp
public abstract record CarryResult
{
    public sealed record Changed(EntityState Item) : CarryResult;
    public sealed record Rejected(Failure Failure) : CarryResult;
}
public CarryResult HoldEntity(EntityId actor, EntityId item);
public CarryResult PackEntity(EntityId actor, EntityId item, int freeSlots);
public CarryResult ReleaseEntity(EntityId item);
public sealed record MoveRequest(EntityId Entity, Vec3 Delta);
public abstract record MoveEntityResult
{
    public sealed record Moved(Vec3 Applied, IReadOnlyList<Contact> Contacts)
        : MoveEntityResult;
    public sealed record Rejected(Failure Failure) : MoveEntityResult;
}
public MoveEntityResult MoveEntity(MoveRequest request);
```

HoldEntity 只抓角色当前 Section 中可拿物的现存切片，每个角色最多手持一件，物品只能一个 Holder；竖起物可在任意有切片的 Section 抓取。PackEntity 要求未竖起、Packable、与角色同 Section，且 freeSlots>0；Sphere4始终拒绝NotPackable，非对称HiddenRingItem按其Packable及本体姿态判定，不因ItemDefinition类别一律拒绝。收入背包后整件形体（包括隐藏结构）不参与世界切片/碰撞。freeSlots=0 返回 CapacityExceeded，负值返回 InvalidInput。引擎记录随身关系，不保存背包容量；#15 的四格是游戏常量，宿主传真实空位。ReleaseEntity 从手/背包释放到角色当前 Section 的当前物品姿态；竖起物保持贯穿物姿态及来源。背包取出先 HoldEntity，可在同一角色已有背包关系上转换；释放不能让小物凭空换 W。宿主负责交互距离与背包 UI，引擎负责几何切片及归属不变量。

MoveEntity 只接受被手持的动态可拿物，不能用它挪角色、装置或生物。Delta 为世界 XYZ 位移；普通平放物随角色的常规三维携带位置同步，仍保持所属 W；有 W 跨度的物体进行四维平移扫掠。按 X、Z、Y 顺序逐分量尝试，受阻分量到最早容差内接触位置，其他分量继续，因此可以滑动；返回 Applied 和实际接触。全部零位移且有阻挡返回 TranslationBlocked，状态不变；无阻挡的零请求为成功。正在旋转返回 Busy。

例如 Delta=(0.1,0,0.2)，X 方向整段受墙阻挡而 Z 畅通，Applied=(0,0,0.2)，结果 Moved 附 X 接触，供宿主刮擦音和镜片标点。多个 Section 的切片与夹层几何共同参与检测。

## 装置、共享量与宿主同步

```csharp
public abstract record DriveDeviceResult
{
    public sealed record Driven(double Position, IReadOnlyList<Contact> Contacts)
        : DriveDeviceResult;
    public sealed record Rejected(Failure Failure) : DriveDeviceResult;
}
public DriveDeviceResult DriveDevice(EntityId device, double target);
public abstract record UpdateResult
{
    public sealed record Updated : UpdateResult;
    public sealed record Rejected(Failure Failure) : UpdateResult;
}
public UpdateResult PlaceEntity(EntityId entity, Pose4 pose);
public UpdateResult SetSharedValue(SharedValueId id, double value);
public UpdateResult SynchronizeActor(EntityId actor, Vec3 position, double yaw);
public UpdateResult SetGrazerFrame(EntityId grazer, Pose4 pose, MetaballShape shape);
public UpdateResult SetSeamOpen(SeamId seam, bool open);
public UpdateResult ConsumeStructure(EntityId grazer,
    IReadOnlyList<EntityId> elements, IReadOnlyList<SeamId> seams);
```

DriveDevice 的 target 对旋转为弧度，对轨道为米，相对初始配置。越限拒绝 OutOfRange，不静默 clamp。旋转完整预检后同步到达 target，受阻整次拒绝；动画由宿主每帧请求插值目标，不能跨过中间碰撞。轨道按从当前位置到 target 的连续线段检测，到最早接触处停止并返回 Driven；全无可行位移返回 TranslationBlocked。每次成功都同步所有切片，按顺序排入 DeviceMotionStarted、如有接触则 MovementBlocked、DeviceMotionEnded 事件；宿主得到实际 Position，以此显示窗是否关闭。

PlaceEntity接受未被抓持、未在旋转中的Turnable/Item的完整四维位置和4×4旋转矩阵。它是宿主布置、测试与调试的原子终态写入：校验目标姿态、目标碰撞、所属楼房范围和尺寸边界，成功立即更新，失败全不变。它不表示玩家动作，不承诺两矩阵间一条未定义的SO(4)动画或扫掠路径；当前玩家适配服务只公开MoveEntity、PlanRotation/StartRotation、CrossSeam等具名操作，不把PlaceEntity暴露为玩家输入的执行方式。

普通物件和隐藏圆环本体按以下规则同时更新Section与UprightOrigin。平放指局部W轴平行世界W轴，误差使用公共姿态精度；倾入W指其余合法旋转。

| 写入前状态 | 目标约束与成功后的状态 |
|---|---|
| 未竖起、目标仍平放 | 目标W必须等于旧Section.W；保留Section，UprightOrigin仍为空。可写完整合法旋转，包括不同XYZ朝向和镜像姿态 |
| 未竖起、目标倾入W | 来源固定取旧Section，不从新姿态猜；目标在该来源Section必须有非退化本体切片，成功保存UprightOrigin=旧Section，Section为空 |
| 已有UprightOrigin、目标仍倾入W | 保留原来源，目标仍须在来源Section有有效本体切片；允许合法W与完整旋转写入，但不能丢失来源约束 |
| 已有UprightOrigin、目标平放 | 目标W必须等于UprightOrigin.W，成功后Section回到该来源并清除UprightOrigin；在其他页平放拒绝WrongSection |

四维球没有平放来源约束，允许写入所属楼房范围内的合法W和完整Rotation，Section/UprightOrigin仍为空。其他不满足表中来源/切片条件的输入返回InvalidInput或NoSlice；角色、装置、四维生物返回WrongEntityKind，使用各自具名操作。玩家仍只能在来源页通过LayDown放倒；受控布置入口也不能把物件平放到另一页。

例：来源W=0的竖起铅笔被请求放置为单位旋转、W=0.42时拒绝WrongSection；相同目标旋转、W=0且终态无碰撞时可成功回到来源并清除Origin。球从W=0.21改为W=0.25且换合法旋转时，若目标范围/碰撞合法则成功，不因球的Section=null被拒绝。


SetSharedValue 越界拒绝；不会自行推导水量或剧情。SynchronizeActor 仅接受三维生物且 position/yaw 有限；更新 XYZ 与普通朝向，保留 Section、W 和四维旋转，无法绕过穿缝改变镜像。背包物随角色同步 XYZ，手持世界物由 MoveEntity 单独请求。SetGrazerFrame 只接受四维生物，提交完整姿态与形体参数；其不受建筑阻挡，下一次切片立即反映新输入。

SetSeamOpen 改变未咬断缝的开启状态；咬断后请求开启返回 SeamSevered。ConsumeStructure 仅接受存在的 Grazer、楼房元素列表和接缝列表，全部引用有效才一次移除完整元素并永久咬断指定缝；重复消费已消失元素或已断缝视为成功但不重复事件。此操作是预先编排的白模啃食，不按元球接触临时猜咬哪一块。下一次 Step 重算支撑。没有通用任意实体姿态 setter，也没有实时四维布尔减法。

#17 的真实调用如下。时间轴只对位置分量、平面角度、元球中心/半径等标量插值，四维旋转矩阵由引擎工厂计算，不对矩阵元素做 lerp。列表顺序是编排数据的一部分；身体朝向改变用角度标量表示，不添加宿主四维动画框架。

```csharp
// Host timeline has already interpolated these scalar parameters.
var rotation = Rotation4.FromPlaneAngles(new PlaneAngle[]
{
    new(RotationPlane.XW, bodyXwAngle),
    new(RotationPlane.YZ, bodyYzAngle)
});
var frame = new Pose4(new Vec4(x, y, z, w), rotation);
var updated = world.SetGrazerFrame(grazerId, frame, bodyShape);
// The host handles UpdateResult.Rejected before requesting this frame's slices.
```

手算：XW角从0插到π/2、YZ角保持0，末帧的局部+X变为+W；工厂输出正交矩阵，主画面与镜片随后读取同一姿态的切片。一次Step前提交的所有编排参数保持调用顺序确定性。

## 穿缝的原子变换

```csharp
public sealed record CrossSeamRequest(
    EntityId Actor, SeamId Seam, SectionId Target,
    bool HasPrism, int FreeBackpackSlots);
public abstract record CrossSeamResult
{
    public sealed record Crossed(SectionId From, SectionId To,
        Handedness ActorHandedness, IReadOnlyList<EntityId> Packed,
        EntityId? WedgedItem) : CrossSeamResult;
    public sealed record Rejected(Failure Failure) : CrossSeamResult;
}
public CrossSeamResult CrossSeam(CrossSeamRequest request);
```

检查次序固定：ID/类别及数值；Actor 当前 Section 与 Target 均连接该缝且不同；Severed；Open；棱镜资格；携带限制/容量；长物整段旋转；出入口占据。角色到缝中心的水平距离须不超过 PassageRadius；否则 WrongSection。通行检测使用角色实际当前位置和按下述普通/镜像变换算出的实际落点，分别在两端检查半径PassageRadius、高度PassageHeight的竖直胶囊；各胶囊以该端实际位置Y为底、以实际XYZ为轴线，不把角色偏移吸附到缝中心后再测。任一端占据则PassageOccupied附接触。一般角色身高/动画碰撞仍属于 Godot，不将角色当四维阻挡物。

例：缝中心X=0、角色X=0.15、半径0.2，普通缝出口墙占X≥0.3时，实际胶囊到X=0.35，会嵌入0.05，必须拒绝；缝中心胶囊空闲不能证明该实际落点可用。

普通缝保留角色的 XYZ 及四维旋转，只把 W 改为目标 Section.W。镜像缝绕缝的法线—W 平面转 π：XYZ 位置关于竖直缝平面反射，W 平移到目标值，四维旋转左乘该半圈矩阵，普通 yaw 由宿主保留。背包及实际随身小物跟随同一变换；返回派生 ActorHandedness，适配层据此翻转屏幕/输入。

未竖起可转长物且 Reach≥两 Section 的 W 距离时先以出发截面接缝处为铰链竖起；Reach 恰等于目标距离时末端距目标Section为0，违反统一0.02m间距，载入应报PartBoundaryTooClose；运行时请求也须拒绝InvalidInput，不把端面当有效抓取Slice。它脱离真正随身集合，变成 UprightOrigin=出发 Section 的世界贯穿物；角色穿缝后 Holder 自动抓住目标 Slice，不再对长物额外乘镜像矩阵。短小且 Packable 的手持物自动入背包，空位不足拒绝；非小物、Sphere4四维对称物、已竖起物拒绝NotPackable；非对称HiddenRingItem未竖起且Packable时可作为短小物入包并随身穿缝，镜像变换作用于其完整姿态。猫的免棱镜资格不免携带/碰撞规则。

上述全部检查先完成，失败时角色、物品、背包、缝和事件全部不变；成功一次提交。穿回只换 Section，不自动放倒卡缝长物，回来源页后另请求 LayDown。

手算镜像例：缝平面 YZ，半圈 `M=diag(−1,1,1,−1)`，det4=+1、XYZ 投影 det3=−1。长轴 +X 的长物先 XW 转 +π/2，+X→+W；再把角色 I→II 并左乘 M。长物仍为 +W，不能再乘 M 变成 −W。背包小物与角色一起镜像。这是本契约对原文泛称“携带”的明确解释，待用户批准。

## Step 与一次事件交付

```csharp
public abstract record SpaceEvent;
public sealed record EntityAdded(EntityId Entity) : SpaceEvent;
public sealed record EntityRemoved(EntityId Entity) : SpaceEvent;
public sealed record RotationStarted(EntityId Entity) : SpaceEvent;
public sealed record RotationEnded(EntityId Entity) : SpaceEvent;
public sealed record DeviceMotionStarted(EntityId Entity, double From, double Target) : SpaceEvent;
public sealed record DeviceMotionEnded(EntityId Entity, double Position) : SpaceEvent;
public sealed record MovementBlocked(EntityId Entity,
    IReadOnlyList<Contact> Contacts) : SpaceEvent;
public sealed record EntityFell(EntityId Entity, double FromY, double ToY) : SpaceEvent;
public sealed record SeamCrossed(EntityId Actor, SeamId Seam,
    SectionId From, SectionId To) : SpaceEvent;
public sealed record StructureConsumed(EntityId Grazer, EntityId Element) : SpaceEvent;
public sealed record SeamSevered(EntityId Grazer, SeamId Seam) : SpaceEvent;
public sealed record EventOccurrence(double Time, long Sequence, SpaceEvent Event);
public abstract record StepResult
{
    public sealed record Advanced(double Time, IReadOnlyList<EventOccurrence> Events)
        : StepResult;
    public sealed record Rejected(Failure Failure) : StepResult;
}
public StepResult Step(double delta);
```

delta 必须有限且>0；失败不改变时间、状态或待交付队列。成功从 t 推进到 t+delta：先推进已开始旋转到该时刻或终点，再求解未被抓持实体的支撑并直接竖直落到最高有效支撑；没有任何支撑时本次不施加无界下落，保持位置并由支撑查询返回无支撑，宿主按关卡边界处理失落物。没有惯性、翻滚或堆叠模拟；三维生物移动与重力仍由 Godot 负责。

写调用形成的事件记在接受时刻 t，排入队列；Step 中的结束事件记实际完成时刻，下落记此次 Step 终点。按 Time、Sequence 递增返回全部待交付事件并清空；Sequence 是世界从 1 开始的递增序号，同刻按成功写调用顺序，单次调用内按实体 ID/接缝 ID 序；Step 同刻先旋转结束再下落，同类按 ID。读和拒绝不入队，因此失败的预检通过返回 Failure 提示，MovementBlocked 仅用于成功部分滑动/装置停止的接触。

例：t=0 启动 1.4s 竖起，Step(1) 返回 RotationStarted@0；Step(0.5) 返回 RotationEnded@1.4（及如有的下落@1.5）；下一次 Step 不重复这两个事件。成功写操作已经可被读取，事件交付延迟不表示状态延迟。
