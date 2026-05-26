namespace Universal_Lift_Structure;

[DefOf]
public static class ULS_DesignationDefOf
{
    static ULS_DesignationDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_DesignationDefOf));
    }

    // 玩家在手动模式下指定小人扳动控制器时创建；由 WorkGiver_FlickWallController 检测
    public static DesignationDef ULS_FlickLiftStructure;
}
