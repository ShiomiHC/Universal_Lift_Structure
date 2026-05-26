namespace Universal_Lift_Structure;

public enum ULS_LiftRequestType
{
    RaiseGroup,
    LowerGroup
}

public class ULS_LiftRequest : IExposable
{
    public ULS_LiftRequestType type;
    public Building_WallController controller;

    // 请求发起时的控制器位置；若当前位置不匹配则请求无效
    public IntVec3 startCell = IntVec3.Invalid;

    // 无参构造函数供 Scribe 反序列化使用
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
        // 加载时若控制器已被销毁，controller 会被设为 null
        Scribe_References.Look(ref controller, "controller");
        Scribe_Values.Look(ref startCell, "startCell", IntVec3.Invalid);
    }
}
