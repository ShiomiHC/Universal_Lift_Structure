namespace Universal_Lift_Structure;

public enum ULS_AutoGroupType
{
    Friendly,
    Hostile,
    Neutral
}

public class CompProperties_ULS_AutoGroupMarker : CompProperties
{
    public ULS_AutoGroupType autoGroupType = ULS_AutoGroupType.Friendly;


    public int maxRadius = 2;


    public int checkIntervalTicks = 60;


    public int closeDelayTicks = 120;


    public int toggleCooldownTicks = 60;

    public CompProperties_ULS_AutoGroupMarker()
    {
        compClass = typeof(ULS_AutoGroupMarker);
    }
}

public class ULS_AutoGroupMarker : ThingComp
{
    public CompProperties_ULS_AutoGroupMarker Props => (CompProperties_ULS_AutoGroupMarker)props;
}