namespace Universal_Lift_Structure;

public class ULS_MultiCellGroupRecord : IExposable
{
    public IntVec3 rootCell;
    public IntVec3 masterControllerCell;
    public List<IntVec3> memberControllerCells;

    public ULS_MultiCellGroupRecord()
    {
        this.memberControllerCells = new();
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