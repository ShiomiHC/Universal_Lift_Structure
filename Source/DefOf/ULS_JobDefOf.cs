namespace Universal_Lift_Structure;

// ============================================================
// 【JobDef 引用容器】
// ============================================================
// 此类使用 RimWorld 的 DefOf 系统，提供对 XML 定义的 JobDef 的静态引用
//
// 【Job 系统说明】
// Job（工作/任务）是 RimWorld 中小人执行具体行为的核心系统
//
// 【Job 的组成】
// - JobDef：定义工作类型（如"扳动升降机"）
// - JobDriver：定义工作的具体执行步骤（如"走过去 → 等待 → 触发"）
// - Job 实例：包含目标对象、参数等具体信息
//
// 【完整工作流程】
// 以"扳动升降机"为例，说明从玩家点击到小人执行的完整流程：
//
// 1. 玩家点击控制器的"扳动升降机"按钮
//    → 系统创建 Designation（ULS_FlickLiftStructure）
//    → Designation 存储在 Map.designationManager 中
//
// 2. 小人空闲时，工作系统开始扫描
//    → WorkGiver_FlickWallController.HasJobOnThing() 检查前置条件
//       检查项：是否有对应的 Designation、小人是否可达、控制器是否可用等
//
// 3. 条件满足后创建 Job
//    → WorkGiver_FlickWallController.JobOnThing() 创建 Job 实例
//    → Job(ULS_FlickLiftStructure, targetController) 返回给小人
//
// 4. 小人选择并开始执行此 Job
//    → 游戏引擎调用对应的 JobDriver_FlickWallController
//
// 5. JobDriver 定义具体执行步骤（Toils）
//    → JobDriver_FlickWallController.MakeNewToils() 返回步骤序列
//    → Toil 1: 走到控制器旁边（Toils_Goto.GotoThing）
//    → Toil 2: 等待一段时间（模拟扳动动作）
//    → Toil 3: 触发升降逻辑（调用控制器的升降方法）
//
// 6. 任务完成
//    → Designation 被移除
//    → 控制器开始执行升降流程
//
// 【与 Designation 的关系】
// - Designation：玩家的"指令"，告诉系统"我想让小人做这件事"
// - Job：小人的"任务"，定义"小人具体要怎么做这件事"
// - 流程：Designation → WorkGiver 检测 → 创建 Job → JobDriver 执行
// ============================================================
[DefOf]
public static class ULS_JobDefOf
{
    // ============================================================
    // 【静态构造函数】
    // ============================================================
    // 在类首次被访问时调用，确保 DefOf 系统正确初始化
    // ============================================================
    static ULS_JobDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_JobDefOf));
    }

    // ============================================================
    // 【JobDef 引用字段】
    // ============================================================

    // 扳动升降结构的工作
    // 用途：定义小人"扳动升降机"的工作类型
    // 关联驱动：JobDriver_FlickWallController - 定义具体执行步骤
    // 对应 XML：Defs/JobDefs/ULS_FlickLiftStructure.xml
    // XML 配置：<driverClass>Universal_Lift_Structure.JobDriver_FlickWallController</driverClass>
    public static JobDef ULS_FlickLiftStructure;
}