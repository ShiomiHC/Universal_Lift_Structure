namespace Universal_Lift_Structure;

[DefOf]
public static class ULS_JobDefOf
{
    //<driverClass> Universal_Lift_Structure.JobDriver_FlickLiftStructure
    public static JobDef ULS_FlickLiftStructure;

    // 静态：正确初始化DefOf
    static ULS_JobDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_JobDefOf));
    }
}

//  玩家点击"扳动升降机" → 创建 Designation
//        ↓
//  WorkGiver_FlickLiftStructure.HasJobOnThing() 检查条件
//        ↓
//  WorkGiver_FlickLiftStructure.JobOnThing() 创建 Job
//        ↓
//  Job(ULS_JobDefOf.ULS_FlickLiftStructure, targetThing) 返回
//        ↓
//  小人选择此 Job 并开始执行
//        ↓
//  JobDriver_FlickLiftStructure.MakeNewToils() 定义步骤
//        ↓
//  小人执行 Toil：走过去 → 等待时间 → 触发升降
//        ↓
//  任务完成，Designation 移除