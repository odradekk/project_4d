# 抓取、携带与布置契约

[契约入口](contract.md) · [实体数据](contract-data.md) · [动作与推进](contract-actions.md) · [几何与查询](contract-views.md)

以下方法都是 `World` 成员，共同规则同[动作与推进](contract-actions.md)：写调用原子接受或拒绝，事件在下一次成功 `Step` 返回。`PlaceEntity` 的签名在[动作与推进](contract-actions.md)的“装置、共享量与宿主同步”一节。

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

HoldEntity 只抓角色当前 Section 中可拿物的现存切片，每个角色最多手持一件，物品只能一个 Holder；竖起物可在任意有切片的 Section 抓取。ID 不存在或类别不对返回 UnknownId/WrongEntityKind，物品正在旋转返回 Busy，不可拿返回 NotHoldable，物品已被持有或角色已持一件返回 EntityInUse，角色当前 Section 无切片返回 NoSlice。PackEntity 要求未竖起、Packable、与角色同 Section，且 freeSlots>0；Sphere4始终拒绝NotPackable，非对称HiddenRingItem按其Packable及本体姿态判定，不因ItemDefinition类别一律拒绝。收入背包后整件形体（包括隐藏结构）不参与世界切片/碰撞。freeSlots=0 返回 CapacityExceeded，负值返回 InvalidInput。物件正在旋转返回 Busy，被别的角色持有返回 EntityInUse，已在背包里返回 InvalidInput，已竖起、不是 Packable 或是 Sphere4 返回 NotPackable，不在角色当前 Section 返回 WrongSection。物件可以躺在世界里，也可以在角色手里。引擎记录随身关系，不保存背包容量；#15 的四格是游戏常量，宿主传真实空位。ReleaseEntity 从手/背包释放到角色当前 Section 的当前物品姿态，未被持有返回 InvalidInput；竖起物保持贯穿物姿态及来源。背包取出先 HoldEntity，可在同一角色已有背包关系上转换；释放不能让小物凭空换 W。穿缝时手里的小物自动入背包（见[穿缝](contract-actions.md#穿缝的原子变换)），状态与 PackEntity 相同。背包物只有持有者能用 HoldEntity 取出，且要求手里没有别的物件；它不能 MoveEntity（InvalidInput）、不能规划旋转（NoSlice）、不能 PlaceEntity（EntityInUse）。

物件离开背包回到世界时（HoldEntity 取出，或 ReleaseEntity 放下），它在别的截面里的情形和载入时按初始截面做的检查不同，所以要重新判断。第一步，定位置和楼房。物件放到角色当前位置：XZ 取角色的 XZ，Y 使物件最低点落在角色脚下，旋转不变，和入包时离角色多远无关，所以背包物的 XYZ 没有意义，SynchronizeActor 也不用带着它平移。物件当前所在的楼房是运行时状态，改为角色当前 Section 所在的楼房：楼房之间在 W 上留空，所以楼房由 Section 的 W 落在哪个楼房区间里唯一确定，定义里的 Building 不变；Section 不在任何楼房里返回 OutOfRange。第二步，检查 PlaceEntity 对姿态的同一组条件：隐藏圆环距每个 Section 的间距不足返回 InvalidInput，超出新楼房的 W 范围返回 OutOfRange，姿态被占据或与正在执行的旋转扫掠相交（例如角色站在别的物件上）返回 TranslationBlocked 并附接触。任何一项不满足就拒绝，物件仍在背包里。宿主负责交互距离与背包 UI，引擎负责几何切片及归属不变量。

MoveEntity 只接受被手持的动态可拿物，不能用它挪角色、装置或生物。Delta 为世界 XYZ 位移；普通平放物随角色的常规三维携带位置同步，仍保持所属 W，只检查目标姿态是否与正在执行的旋转扫掠相交（宿主每帧传来的位移很小，所以不沿路径检查），相交返回 TranslationBlocked；有 W 跨度的物体进行四维平移扫掠。按 X、Z、Y 顺序逐分量尝试。一对形体的嵌入深度将超过起点深度和 ContactTolerance 中较大者时，该分量被挡住，停在嵌入深度不超过起点深度和 0 中较大者的最后位置，其他分量继续，因此可以滑动；新碰上的障碍停在刚接触处，而起点已相贴或嵌在容差内的障碍不挡住沿表面滑动或离开，齐平拼接的楼板和容差内的高差也不挡住。返回 Applied 和实际接触。全部零位移且有阻挡返回 TranslationBlocked，状态不变；无阻挡的零请求为成功。未被手持或 Delta 非有限数返回 InvalidInput，正在旋转返回 Busy。有接触的成功移动在下一次 Step 交付 MovementBlocked。

例如 Delta=(0.1,0,0.2)，X 方向整段受墙阻挡而 Z 畅通，Applied=(0,0,0.2)，结果 Moved 附 X 接触，供宿主刮擦音和镜片标点。多个 Section 的切片与夹层几何共同参与检测。

## 受控布置

PlaceEntity接受未被抓持、未在旋转中的Turnable/Item的完整四维位置和4×4旋转矩阵。它是宿主布置、测试与调试的原子终态写入：校验目标姿态、目标碰撞、所属楼房范围和尺寸边界，成功立即更新，失败全不变；被抓持返回 EntityInUse，平放的隐藏圆环物的圆环离每个 Section 仍须满足载入时的距离，否则 InvalidInput。它不表示玩家动作，不承诺两矩阵间一条未定义的SO(4)动画或扫掠路径；当前玩家适配服务只公开MoveEntity、PlanRotation/StartRotation、CrossSeam等具名操作，不把PlaceEntity暴露为玩家输入的执行方式。

普通物件和隐藏圆环本体按以下规则同时更新Section与UprightOrigin。平放指局部W轴平行世界W轴，误差使用公共姿态精度；倾入W指其余合法旋转。

| 写入前状态 | 目标约束与成功后的状态 |
|---|---|
| 未竖起、目标仍平放 | 目标W必须等于旧Section.W；保留Section，UprightOrigin仍为空。可写完整合法旋转，包括不同XYZ朝向和镜像姿态 |
| 未竖起、目标倾入W | 来源固定取旧Section，不从新姿态猜；目标在该来源Section必须有非退化本体切片，成功保存UprightOrigin=旧Section，Section为空 |
| 已有UprightOrigin、目标仍倾入W | 保留原来源，目标仍须在来源Section有有效本体切片；允许合法W与完整旋转写入，但不能丢失来源约束 |
| 已有UprightOrigin、目标平放 | 目标W必须等于UprightOrigin.W，成功后Section回到该来源并清除UprightOrigin；在其他页平放拒绝WrongSection |

四维球没有平放来源约束，允许写入所属楼房范围内的合法W和完整Rotation，Section/UprightOrigin仍为空。其他不满足表中来源/切片条件的输入返回InvalidInput或NoSlice；角色、装置、四维生物返回WrongEntityKind，使用各自具名操作。玩家仍只能在来源页通过LayDown放倒；受控布置入口也不能把物件平放到另一页。

例：来源W=0的竖起铅笔被请求放置为单位旋转、W=0.42时拒绝WrongSection；相同目标旋转、W=0且终态无碰撞时可成功回到来源并清除Origin。球从W=0.21改为W=0.25且换合法旋转时，若目标范围/碰撞合法则成功，不因球的Section=null被拒绝。
