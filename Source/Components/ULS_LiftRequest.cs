namespace Universal_Lift_Structure;

/// 枚举意图：定义一次“待执行的升降请求”的类型。
public enum ULS_LiftRequestType
{
    RaiseGroup,
    LowerGroup
}

/// 数据结构意图：可存档的升降请求。
/// 说明：请求会挂在控制台或控制器的 `ULS_FlickTrigger` 上，并在 flick 完成信号到达时执行。
public class ULS_LiftRequest : IExposable
{
    public ULS_LiftRequestType type;
    public Building_WallController controller;

    // LowerGroup 使用的起点控制器格：用于解析显式 GroupId 并执行组降下；RaiseGroup 时可为 Invalid。
    public IntVec3 startCell = IntVec3.Invalid;

    public ULS_LiftRequest()
    {
    }

    public ULS_LiftRequest(ULS_LiftRequestType type, Building_WallController controller, IntVec3 startCell)
    {
        this.type = type;
        this.controller = controller;
        this.startCell = startCell;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref type, "type");
        Scribe_References.Look(ref controller, "controller");
        Scribe_Values.Look(ref startCell, "startCell", IntVec3.Invalid);
    }
}
