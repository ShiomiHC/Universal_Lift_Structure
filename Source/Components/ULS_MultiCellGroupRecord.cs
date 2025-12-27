namespace Universal_Lift_Structure;

/// 文件意图：多格隐组持久化 —— Record：可存档的纯数据类，记录隐组的根格（Key）、主控制器格、以及所有成员控制器格，
/// 保证多格建筑在收纳态下的结构完整性与可追溯性。
public class ULS_MultiCellGroupRecord : IExposable
{
    public IntVec3 rootCell = IntVec3.Invalid;
    public IntVec3 masterControllerCell = IntVec3.Invalid;
    public List<IntVec3> memberControllerCells = new();

    public ULS_MultiCellGroupRecord()
    {
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
