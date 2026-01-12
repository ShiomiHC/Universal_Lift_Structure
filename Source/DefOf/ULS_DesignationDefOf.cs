//  【Designation = 指定/标记】
//  ▸ 玩家在游戏中"指定"某个对象执行特定操作时创建
//  【Designation 的作用】
//  ▸ 作为"待办任务"的标记，存储在 地图 上（Map.designationManager）
//  ▸ 小人空闲时，WorkGiver 会扫描 Designation，找到有任务的目标
//  ▸ 任务完成后，Designation 会被移除

namespace Universal_Lift_Structure;

[DefOf]
public static class ULS_DesignationDefOf
{
    public static DesignationDef ULS_FlickLiftStructure;

    // 静态：正确初始化DefOf
    static ULS_DesignationDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_DesignationDefOf));
    }
}