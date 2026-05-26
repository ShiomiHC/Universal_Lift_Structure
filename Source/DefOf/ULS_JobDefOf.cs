namespace Universal_Lift_Structure;

[DefOf]
public static class ULS_JobDefOf
{
    static ULS_JobDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_JobDefOf));
    }

    // 对应 JobDriver_FlickLiftStructure
    public static JobDef ULS_FlickLiftStructure;
}
