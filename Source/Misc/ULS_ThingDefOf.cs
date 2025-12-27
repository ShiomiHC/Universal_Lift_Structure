namespace Universal_Lift_Structure;


/// 文件意图：集中管理仅由代码引用的 ThingDef（临时阻挡物等）。

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
