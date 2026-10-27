# 截面输出与通用查询

[契约入口](contract.md) · [数据](contract-data.md) · [动作](contract-actions.md)

本页的方法都是 `World` 只读成员。所有结果来自同一个当前世界，不推进时间、不改变计划修订，也不携带 Godot 类型。未找到实体的查询以各结果的 Rejected(UnknownId) 返回；空切片是有效结果，不等同于未知实体。

## 切片、轮廓与 Godot 转换

```csharp
public sealed record SliceGeometry(
    EntityId Entity, IReadOnlyList<double> Positions,
    IReadOnlyList<double> Normals, IReadOnlyList<double> Uvs,
    IReadOnlyList<PartId> Parts, bool ThroughObject);
public abstract record SectionGeometryResult
{
    public sealed record Geometry(double W, IReadOnlyList<SliceGeometry> Entities)
        : SectionGeometryResult;
    public sealed record Rejected(Failure Failure) : SectionGeometryResult;
}
public SectionGeometryResult GetSectionGeometry(SectionId section);
public SectionGeometryResult GetGeometryAtW(double w);
public sealed record Polyline(IReadOnlyList<double> Positions, bool Closed);
public sealed record PreviewSample(double Progress,
    IReadOnlyList<Polyline> Contours, bool Blocked);
public sealed record RotationPreview(
    double W, IReadOnlyList<PreviewSample> Samples,
    IReadOnlyList<Polyline> FoldPlane, IReadOnlyList<Contact> Contacts);
public abstract record PreviewResult
{
    public sealed record Preview(RotationPreview Value) : PreviewResult;
    public sealed record Rejected(Failure Failure) : PreviewResult;
}
public PreviewResult GetRotationPreview(RotationPlan plan, SectionId section);
public PreviewResult GetHiddenStructurePreview(RotationPlan plan, SectionId section);
```

GetGeometryAtW 接受任意有限 W，供 `[Tool]` 查看夹层；本轮只支持 W=常量的 Section，不扩展任意四维超平面。输出含有体积的切片，按 EntityId 排序；每件的三角形按稳定零件号分组。相切为点/线/面、没有三维内部的退化交集不输出可碰撞实体；`HasSlice` 使用同一规则。普通平放物输出原盒子三维几何；旋转途中和竖起后由四维交集计算。背包物和已移除元素不输出；三维生物由 Godot 渲染，不在切片网格内。

几何是无索引三角形表：每三个顶点一个三角形。若顶点数 N，N%3=0；Positions 和 Normals 都是 3N 个 double，依次 xyz；Uvs 为 2N 个 double，依次 uv；Parts 为 N 个 PartId，同一三角形三个值相同。不使用 `float[]`、共享缓存或内部面/边对象。坐标和法线均位于世界 XYZ，适配层生成节点用单位变换，避免再次乘实体变换。ThroughObject 表示该实体的当前四维形体跨出一个普通 ±ε 层，供镜片发光描边；它不是“当前恰有两个可玩切片”的计数。

三角形从外部看为逆时针，法线单位长、朝外，满足 `dot(cross(b−a,c−a),normal)>0`；盒子/放样棱边顶点分开，保留平面法线。球、圆环和元球低多边形表面同样逐三角形平面法线。退化三角形不输出。前两类解析形体与元球场的截面定义精确，球按64经线、32纬线离散；圆环的中心圆整个落在同一 W 时切片是环体，中心圆取64段、管截面取24段，中心圆倾入 W 时（旋转途中）改用与元球相同的六四面体划分：网格以圆环中心为基准，格长取 0.02m 与截面里最粗管半径之半中较小者，每边不超过64格，立方体边上的顶点用二分法落在曲面上；元球在其截面有限包围盒内使用世界坐标0.2m立方网格、固定六四面体划分和线性等值插值输出三角形：节点在世界坐标 0.2m 的整数倍上，不随啃食者移动；场值高于阈值的节点在体内，其余（含恰等于阈值的）在体外；一条棱的两端一内一外时，顶点按两端的场值减阈值线性插值落在棱上。没有节点落进体内的切片（小于一个格的团块）不输出。格长是固定常量，不做每个形体可设的参数。0.2m 接近原型的 0.22m，是按序章原型规模的形体（59 个球，约 3×3×5m）定的：主画面和镜片每帧各切一次，每次要在数毫秒内完成，同样的形体用 0.02m 要几百毫秒。包围盒在某个方向上会超过 128 个节点时，格长取 0.2m 的整数倍，使每边不超过 128 个节点，所以特别宽的形体网格更粗，内存有上限。元球网格不承诺未经证明的全局弦差或Hausdorff上界；不得据网格误差改变支撑/碰撞合同。啃食者的切片零件号是 `body`；ThroughObject 看各球场的支撑在 W 上的跨度（最低球心 W−半径到最高球心 W+半径）是否大于 2ε。

盒子面 UV 用 FaceUv 的轴面坐标：±X 用 (z,y)，±Y 用 (x,z)，±Z 用 (x,y)，负面把第一坐标取负，再逐分量乘 Scale 加 Offset。FaceUvs 恰含六种轴/正负组合，Scale 为有限非零分量。W 端面切出的新面用物体局部 (x,z)；坐标由切片顶点逆变换求出，翻面后图案随局部基镜像，不把 UV 人为恢复为正常。放样面使用其 FaceUv 同一规则。球/圆环/元球用截面世界 (x,z) 平面投影为 UV，单位 1m；白模不要求连续无缝曲面贴图。材料由 EntityId/PartId 映射到宿主资源。

Godot 三角形正面使用顺时针，适配层对每个三角形交换第二、第三顶点，同时交换它们的法线、UV、零件号；坐标不变、不翻法线，再把 double 转为 float。分别填入 `Mesh.ArrayType.Vertex/Normal/TexUV`，使用 `ArrayMesh.AddSurfaceFromArrays(Triangles, arrays)`；碰撞形状用同样顶点。Godot 官方 [ArrayMesh 文档](https://docs.godotengine.org/en/stable/classes/class_arraymesh.html)说明了正面绕序；本契约明确转换，避免渲染器约定泄漏到引擎类型。

Polyline 的 Positions 是 3K 个 double，K≥2；Closed=true 时隐含最后一点连第一点，不重复首点。预览 Samples 从 Progress=0 到 1 严格递增，包含旋转碰撞采样点及终点；轮廓是该进度切片的真实外边线。最早受阻进度及之后的 Blocked=true，此前 false；Contacts 使用相同计划的碰撞点。FoldPlane 是翻转面与目标 Section/运动包围盒相交的线框，不是无穷平面。未扫过目标 Section 时 Samples 的轮廓可全空，宿主不画跨页线。

GetHiddenStructurePreview 只接受隐藏圆环物的 StandUp 计划：返回最终进度 1 中隐藏零件的真实轮廓，即使原始计划 Blocked 也可看最终落点，由宿主画虚线。结果只有进度 1 一个样本，其 Blocked 与计划是否受阻一致，FoldPlane 为空，Contacts 取计划的碰撞点。球和圆环（含旋转预览里的）没有棱边，轮廓取切片曲面与过圆环中心、同中心圆所在平面重合的平面的交线，每段两点；环体因此得到内外两圈。球没有中心圆，用过球心的水平面。圆环中心圆倾入 W 时，它所在的是四维平面，轮廓用该平面在 XYZ 里的投影，投影退化成一条线时也用过圆环中心的水平面。其他计划返回 WrongEntityKind。过期/其他世界计划都分别返回 StalePlan/ForeignPlan；读取不能偷偷重新规划。

## 支撑、占据、切片与方向

```csharp
public sealed record SupportContact(
    EntityId Support, PartId Part, Vec4 Point, Vec4 Normal);
public abstract record SupportResult
{
    public sealed record Supported(double RestingOriginY,
        IReadOnlyList<SupportContact> Contacts) : SupportResult;
    public sealed record Unsupported : SupportResult;
    public sealed record Rejected(Failure Failure) : SupportResult;
}
public SupportResult FindSupport(EntityId entity);
public abstract record OccupancyShape;
public sealed record OccupancyBox(Pose4 Pose, Vec4 HalfSize) : OccupancyShape;
public sealed record OccupancySphere(Vec4 Center, double Radius) : OccupancyShape;
public sealed record OccupancyHit(EntityId Entity, PartId Part, Vec4 Point);
public abstract record OccupancyResult
{
    public sealed record Occupancy(IReadOnlyList<OccupancyHit> Hits) : OccupancyResult;
    public sealed record Rejected(Failure Failure) : OccupancyResult;
}
public OccupancyResult QueryOccupancy(OccupancyShape shape,
    IReadOnlyList<EntityId> excluded);
public abstract record SlicePresenceResult
{
    public sealed record Present(bool HasVolume) : SlicePresenceResult;
    public sealed record Rejected(Failure Failure) : SlicePresenceResult;
}
public SlicePresenceResult HasSlice(EntityId entity, SectionId section);
public abstract record SliceBoundsResult
{
    public sealed record Bounds(Vec3 Min, Vec3 Max) : SliceBoundsResult;
    public sealed record Empty : SliceBoundsResult;
    public sealed record Rejected(Failure Failure) : SliceBoundsResult;
}
public SliceBoundsResult GetSliceBounds(
    EntityId entity, SectionId section, PartId? part = null);
public abstract record SliceDirectionResult
{
    public sealed record Direction(Vec3 UnitDirection) : SliceDirectionResult;
    public sealed record Degenerate : SliceDirectionResult;
    public sealed record Rejected(Failure Failure) : SliceDirectionResult;
}
public SliceDirectionResult GetSlicePlaneNormal(
    EntityId entity, SectionId section, Axis3 first, Axis3 second,
    PartId? part = null);
```

四维盒子 W 端盖没有三维原始面对应的 FaceUv；其 UV 使用零件局部原始 `(x,z)` 坐标，比例为 1、偏移为 0。其他可见面继续使用对应原始面的 FaceUv。

FindSupport 求固定 X/Z/W 与旋转时，实体沿 −Y 平移能落到的最高有效支撑，返回实体原点最终 Y，不把支撑表面 Y 与物体中心 Y 混淆。高于当前姿态、需把实体向上穿过障碍才到达的面不算。Contacts 是该落点实际四维接触，可能在两个可玩 Section 之间；自身、背包物、三维生物与元球不提供四维支撑。接触法线有正 Y 分量，物体与支撑不允许超过容差的嵌入。薄片可挡/卡但不可供三维生物踩踏；适配层对竖起普通薄片只做水平阻挡，不生成可行走地面标记。

只有沿竖直侧面擦边且继续下落不会进入障碍内部时，该侧面不提供支撑。初始姿态已嵌入不超过 0.005m 时，保持当前 Y，并返回双方形体共有的真实接触点；不把实体向上移出障碍。有效落点使用未扩大形体计算，不因碰撞容差而悬停。

Step 同时结算物件支撑，使结果不依赖实体 ID；事件仍按 ID 排序。Step 开始时已有的容差内接触可随支撑共同下降并保留原有嵌入；正间隙和新接触仍按真实形体求落点，不因容差悬停。互相依赖且没有外部支撑的终端物件组保持原位，其上方物件仍可落到该组上。

例：平地 y=0，R=0.25 的四维球放置后 RestingOriginY=0.25，接触 W=0.21。Ⅱ的洞不包含该 W，因此 Supported。要画Ⅱ的球点状投影，宿主查询Ⅱ内球正下方一块薄 OccupancyBox 是否有地板；四维整体受托住不表示该 Section 正下方也有地板，不用专用 `ballBridges`。

缺口端面支撑验例：地板XYZ范围为[−2,2]×[−0.1,0]×[−2,2]，WRange=[−1,1]、Gap=[0.3,0.6]，按楼房截取规则生成闭段[−1,0.3]∪[0.6,1]。球R=0.25，中心X=Z=0、W=0.42，从足够高处下落；最近地板端面W=0.3与球心相差0.12，因此RestingOriginY=√(0.25²−0.12²)=0.2193171219946…，实际接触点为(0,0,0,0.3)。W=0.42处没有地板，但该闭合端面确实承托球；不能返回只有下确界、没有实际接触的悬停高度。另一端W=0.6距离0.18，所需高度√(0.25²−0.18²)更低，不决定首次落点。

QueryOccupancy 返回实际与体积相交的碰撞实体零件，按 EntityId/PartId 排序；容差内相贴不算占据。HalfSize 四分量>0，球半径>0；excluded ID 必须存在且无重复。它排除三维生物与元球，包含楼房/固定/可转/四维物品/装置。Hits 为空表示空间可用。示例：在 W=0.42 的轨道位置放厚度 2ε 的检测盒，竖起铅笔有体积交集时返回其 ID/零件；放倒后返回空。

GetSliceBounds 给当前切片的世界XYZ包围范围。part=null时取整件实体，指定PartId时仅取该零件；未知零件返回UnknownId，已知零件在该Section无体积切片返回Empty。零件筛选同样可用SliceGeometry.Parts读取对应三角形。范围中点只是候选目标点，不是解析环心；隐藏圆环物的本体可能偏离圆环，脚本不能拿整体AABB中心当圈心。

GetSlicePlaneNormal 返回局部指定两个轴所张平面在世界XYZ投影后的无向法线轴。普通零件使用零件局部基，圆环零件使用Ring4局部基；part=null使用实体基。取合成四维旋转后的两轴U/V的XYZ投影u/v，计算c=cross(u,v)；first与second相同返回InvalidInput，||c||≤1e-10时Degenerate，否则归一化。为使结果确定，令归一化向量中按X/Y/Z顺序首个绝对值>1e-10的分量为正；这个符号只是统一表示，不赋予圆环“正面”。无切片返回NoSlice，未知零件返回UnknownId。

圈方向调用指定圆环PartId、first=X、second=Y。圆环自身ZW平面旋转不改变中心圆U/V，返回相同法线轴；即使局部Z转为W也不能因此报告方向退化。中心圆平面投影秩不足才Degenerate，不把方向非退化等同于目标Section存在完整可钻孔；轻微倾斜也不能直接判为无孔。

猫脚本从圆环零件切片范围选候选跳点，并使用该零件真实三角形/轮廓识别孔的区域。实际跳跃前，通过QueryOccupancy检查包含整段三维身体扫过体积的保守检测盒（在猫Section处给W方向±ε），不能把方向或包围盒中点当作孔存在的证明。Hits为空才证明检测盒内通路无障碍；有Hits只表示该候选不获保证，可尝试另一个候选。检测不能排除圆环本身，需排除的仅为猫或已持有且按规则不阻挡的实体。宿主的候选选择及三维跳跃规划属于常规三维逻辑，不重新计算四维形状。

## 四维距离与手性

```csharp
public double Distance4(Vec4 a, Vec4 b);
public abstract record HandednessResult
{
    public sealed record Value(Handedness Handedness) : HandednessResult;
    public sealed record Rejected(Failure Failure) : HandednessResult;
}
public HandednessResult ReadHandedness(EntityId entity);
public enum HandednessComparison { Same, Different, NotComparable }
public abstract record HandednessComparisonResult
{
    public sealed record Comparison(HandednessComparison Value)
        : HandednessComparisonResult;
    public sealed record Rejected(Failure Failure) : HandednessComparisonResult;
}
public HandednessComparisonResult CompareHandedness(EntityId a, EntityId b);
```

Distance4 为 `sqrt(dx²+dy²+dz²+dw²)`，数值算法避免中间平方溢出；非有限输入抛 ArgumentOutOfRangeException，这是无世界写状态的纯数值函数前置条件。示例 (0,0,0,0) 与 (3,0,0,4) 距离为 5；同 XYZ、W 相差 0.42 时距离为 0.42。声源 XYZ 方向由宿主三维音频处理，四维距离仅用于跨页衰减及音乐阈值。

三维手性取实体局部 X/Y/Z 有序基投影到世界 XYZ 的 3×3 行列式 d：d>1e-10 为 Normal，d<−1e-10 为 Mirrored，其他为 NotComparable；旋转中投影退化不编造正常/镜像。三维生物的普通 yaw 属于三维旋转，不改变这个符号。四维球、装置、楼房和元球本身没有当前玩法所需的三维手性，返回 NotComparable；隐藏圆环物使用普通本体基。手性不单独保存布尔值，也不从 det4 得到。

CompareHandedness 只有双方均可比较时才返回 Same 或 Different；任一 NotComparable 都返回 NotComparable，两个不可比较也不匹配。食物/钥匙脚本仅在 Same 时允许；啃食者识别正常物质时读取目标 Handedness==Normal，不拿元球自身做比较。

手算：单位矩阵 d=1；YZ 缝对应 M=diag(−1,1,1,−1)，d=−1而det4=1；再穿同类镜像缝一次，M²=I、d=1。XW 竖起 π/2 时 d=cos(π/2)=0，所以不可比较。屏幕/输入镜像只读三维生物派生结果，宿主不自行计算 det3。
