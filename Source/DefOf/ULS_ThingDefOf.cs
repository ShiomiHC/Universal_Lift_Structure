namespace Universal_Lift_Structure;

[DefOf]
public static class ULS_ThingDefOf
{
    static ULS_ThingDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_ThingDefOf));
    }

    // 升降过程中临时占位的隐藏建筑，防止其他建筑或小人进入
    public static ThingDef ULS_LiftBlocker;

    public static ThingDef ULS_LiftConsole;

    public static ThingDef ULS_WallController;

    // public static ThingDef ULS_WallController_Auto;
}
