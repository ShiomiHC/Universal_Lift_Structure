namespace Universal_Lift_Structure;

[DefOf]
public static class ULS_ThingDefOf
{
    static ULS_ThingDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_ThingDefOf));
    }

    public static ThingDef ULS_LiftBlocker;

    public static ThingDef ULS_FlickProxy;
}