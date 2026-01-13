namespace Universal_Lift_Structure;

// ============================================================
// 【DesignationDef 引用容器】
// ============================================================
// 此类使用 RimWorld 的 DefOf 系统，提供对 XML 定义的 DesignationDef 的静态引用
//
// 【Designation 系统说明】
// Designation（指定/标记）是 RimWorld 中玩家下达命令的机制
//
// 【工作流程】
// 1. 玩家在游戏中对某个对象"指定"执行特定操作
//    例如：右键点击控制器 → 选择"扳动升降机"
//
// 2. 系统创建 Designation 并存储在地图上（Map.designationManager）
//    Designation 包含：目标对象、指定类型（DesignationDef）、相关参数
//
// 3. 小人空闲时，WorkGiver 会扫描 Designation 列表
//    查找符合条件的任务（例如：小人具有相应的工作技能）
//
// 4. WorkGiver 为小人创建 Job，小人开始执行任务
//
// 5. 任务完成后，Designation 被移除
//
// 【本项目中的应用】
// ULS_FlickLiftStructure：玩家指定小人去"扳动"升降机控制器
// 流程：玩家点击 → 创建 Designation → WorkGiver 检测 → 创建 Job → 小人执行
// ============================================================
[DefOf]
public static class ULS_DesignationDefOf
{
    // ============================================================
    // 【静态构造函数】
    // ============================================================
    // 在类首次被访问时调用，确保 DefOf 系统正确初始化
    // ============================================================
    static ULS_DesignationDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_DesignationDefOf));
    }

    // ============================================================
    // 【DesignationDef 引用字段】
    // ============================================================

    // 扳动升降结构的指定
    // 用途：当玩家在手动模式下点击控制器的升降按钮时创建
    // 关联工作：WorkGiver_FlickWallController 会检测此 Designation
    // 关联任务：JobDriver_FlickWallController 会执行具体的扳动动作
    // 对应 XML：Defs/DesignationDefs/ULS_FlickLiftStructure.xml
    public static DesignationDef ULS_FlickLiftStructure;
}