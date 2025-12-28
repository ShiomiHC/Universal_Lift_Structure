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