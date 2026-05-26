namespace Universal_Lift_Structure;

public class ULS_MultiCellGroupRecord : IExposable
{
    // 唯一标识一个多格建筑，通常是占据单元格中坐标最小的那个
    public IntVec3 rootCell;

    // 负责统筹整个多格建筑升降的控制器位置
    public IntVec3 masterControllerCell;

    // 其他成员控制器位置；不包含主控制器自己
    public List<IntVec3> memberControllerCells;

    // 无参构造函数供 Scribe 反序列化使用
    public ULS_MultiCellGroupRecord()
    {
        memberControllerCells = new();
    }

    public ULS_MultiCellGroupRecord(IntVec3 rootCell, IntVec3 masterControllerCell, List<IntVec3> memberControllerCells)
    {
        this.rootCell = rootCell;
        this.masterControllerCell = masterControllerCell;
        this.memberControllerCells = memberControllerCells ?? new();
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref rootCell, "rootCell", IntVec3.Invalid);
        Scribe_Values.Look(ref masterControllerCell, "masterControllerCell", IntVec3.Invalid);
        Scribe_Collections.Look(ref memberControllerCells, "memberControllerCells", LookMode.Value);
    }
}
