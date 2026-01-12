namespace Universal_Lift_Structure;

// "Def 引用容器"会自动填充其中的静态字段
[DefOf]
//非重复的自建defof
public static class ULS_ThingDefOf
{
    // 预构建的def
    static ULS_ThingDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_ThingDefOf));
    }

    //ThingDef
    public static ThingDef ULS_LiftBlocker; // 升降机临时阻挡物（隐藏建筑）

    public static ThingDef ULS_LiftConsole; // 升降控制台

    public static ThingDef ULS_WallController; // 升降机控制器

    // public static ThingDef ULS_WallController_Auto; // 自动模式的升降机控制器
}
