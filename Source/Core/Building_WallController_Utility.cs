namespace Universal_Lift_Structure;

// ============================================================
// 【控制器专用工具方法】
// ============================================================
// 此 partial class 扩展了 Building_WallController 类
// 提供控制器相关的工具方法
//
// 【核心职责】
// 1. 消息发送：发送拒绝和中性消息给玩家
// 2. 升起阻挡检测：检查建筑是否可以升起（是否有阻挡）
// 3. 控制器识别：判断物体是否为控制器或控制器相关物体
// 4. 存储退款：退还存储在控制器中的建筑
// 5. 分组大小获取：从设置中获取分组最大尺寸
//
// 【设计模式】
// - partial class：将工具方法从主类分离，提高代码组织性
// - private 方法：这些方法仅供控制器内部使用
// ============================================================

public partial class Building_WallController
{
    // ============================================================
    // 【消息发送方法】
    // ============================================================

    // ============================================================
    // 【发送拒绝消息】
    // ============================================================
    // 发送拒绝输入类型的消息（红色感叹号）
    //
    // 【消息去重】
    // 使用 MessagesRepeatAvoider 防止短时间内重复显示相同消息（1秒冷却）
    //
    // 【参数说明】
    // - key: 消息翻译键
    // - lookTargets: 消息关联的目标（点击消息会聚焦）
    // - args: 翻译参数
    // ============================================================
    private static void MessageReject(string key, LookTargets lookTargets, params NamedArgument[] args)
    {
        // 检查是否允许显示消息（防止短时间内重复显示）
        if (MessagesRepeatAvoider.MessageShowAllowed(key, 1f))
        {
            Messages.Message(
                key.Translate(args),
                lookTargets,
                MessageTypeDefOf.RejectInput, // 拒绝输入类型（红色感叹号）
                historical: false); // 不记录到历史消息
        }
    }


    // ============================================================
    // 【发送中性消息】
    // ============================================================
    // 发送中性事件类型的消息（灰色信息图标）
    //
    // 【参数说明】
    // - key: 消息翻译键
    // - lookTargets: 消息关联的目标
    // - args: 翻译参数
    // ============================================================
    private static void MessageNeutral(string key, LookTargets lookTargets, params NamedArgument[] args)
    {
        if (MessagesRepeatAvoider.MessageShowAllowed(key, 1f))
        {
            Messages.Message(
                key.Translate(args),
                lookTargets,
                MessageTypeDefOf.NeutralEvent, // 中性事件类型（灰色信息图标）
                historical: false);
        }
    }


    // ============================================================
    // 【升起阻挡检测方法】
    // ============================================================

    // ============================================================
    // 【阻挡检测】
    // ============================================================
    // 检查存储的建筑是否可以升起（是否有阻挡）
    //
    // 【阻挡规则】
    // 1. 建筑占据的任意单元格超出地图边界
    // 2. 单元格上有其他建筑（除了升降阻挡器）
    // 3. 单元格上有 Pawn、框架、蓝图或其他建筑类物体
    //
    // 【忽略物体】
    // - 控制器本身
    // - 存储的建筑本身
    // - 升降阻挡器（ULS_LiftBlocker）
    // - 其他控制器及其相关物体
    //
    // 【参数说明】
    // - map: 目标地图
    // - spawnCell: 建筑的生成位置
    // - storedThing: 存储的建筑
    //
    // 【返回值】
    // - true: 有阻挡（不能升起）
    // ============================================================
    private bool IsBlockedForRaise(Map map, IntVec3 spawnCell, Thing storedThing)
    {
        // 遍历建筑占据的所有单元格
        foreach (IntVec3 cell in GenAdj.OccupiedRect(spawnCell, storedRotation, storedThing.def.size))
        {
            // 检查是否超出地图边界
            if (!cell.InBounds(map))
            {
                return true;
            }


            // 检查单元格上的建筑
            Building edifice = map.edificeGrid[cell];
            if (edifice != null && edifice.def != ULS_ThingDefOf.ULS_LiftBlocker)
            {
                // 有其他建筑（不是升降阻挡器），阻挡升起
                return true;
            }


            // 检查单元格上的所有物体
            List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
            foreach (var thing in things)
            {
                // 忽略控制器本身、存储的建筑、升降阻挡器和其他控制器
                if (thing == this ||
                    thing == storedThing ||
                    thing.def == ULS_ThingDefOf.ULS_LiftBlocker ||
                    IsWallControllerThing(thing))
                {
                    continue;
                }


                // 检查是否为阻挡物体
                if (thing is Pawn || // Pawn 会阻挡
                    thing is Frame || // 框架会阻挡
                    thing is Blueprint || // 蓝图会阻挡
                    (thing.def.category == ThingCategory.Building && // 建筑类物体
                     (thing.def.building == null || thing.def.building.isEdifice))) // 且是建筑实体
                {
                    return true;
                }
            }
        }

        return false;
    }


    // ============================================================
    // 【控制器识别方法】
    // ============================================================

    // ============================================================
    // 【控制器物体校验】
    // ============================================================
    // 检查物体是否为控制器或控制器相关物体
    //
    // 【识别规则】
    // 1. 物体本身是 Building_WallController 实例
    // 2. 物体的 def 是 ULS_WallController
    // 3. 物体的 entityDefToBuild 是 ULS_WallController（即构建框架或蓝图）
    //
    // 【典型用途】
    // 在升起检测时忽略控制器及其框架/蓝图
    //
    // 【参数说明】
    // - thing: 待检查的物体
    //
    // 【返回值】
    // - true: 是控制器相关物体
    // ============================================================
    private bool IsWallControllerThing(Thing thing)
    {
        if (thing == null)
        {
            return false;
        }

        // 检查是否为控制器实例
        if (thing is Building_WallController)
        {
            return true;
        }

        ThingDef defInstance = thing.def;
        if (defInstance == null)
        {
            return false;
        }

        // 检查 def 是否为控制器
        if (defInstance == ULS_ThingDefOf.ULS_WallController)
        {
            return true;
        }

        // 检查是否为控制器的框架或蓝图
        return defInstance.entityDefToBuild == ULS_ThingDefOf.ULS_WallController;
    }


    // ============================================================
    // 【存储退款方法】
    // ============================================================

    // ============================================================
    // 【存储退款】
    // ============================================================
    // 退还存储在控制器中的建筑
    //
    // 【工作流程】
    // 1. 从容器中移除存储的建筑
    // 2. 清空市场价值缓存
    // 3. 设置建筑的位置和旋转
    // 4. 调用 GenSpawn.Refund 退还建筑材料
    //
    // 【异常处理】
    // - 如果建筑已摧毁，仅清除存储信息
    // - 如果地图为 null，销毁建筑
    //
    // 【典型用途】
    // - 控制器被摧毁时退还存储的建筑
    // - 控制器无法升起时退还建筑材料
    //
    // 【参数说明】
    // - map: 目标地图
    // ============================================================
    internal void RefundStored(Map map)
    {
        // 如果没有存储，直接返回
        if (!HasStored)
        {
            return;
        }

        Thing storedThing = StoredThing;
        if (storedThing == null)
        {
            // 清除存储信息
            storedCell = IntVec3.Invalid;
            storedThingMarketValueIgnoreHp = 0f;
            return;
        }


        // 从容器中移除建筑
        innerContainer.Remove(storedThing);
        storedThingMarketValueIgnoreHp = 0f;

        // 如果建筑已摧毁，仅清除存储标记
        if (storedThing.Destroyed)
        {
            storedCell = IntVec3.Invalid;
            return;
        }


        // 设置建筑的位置和旋转
        IntVec3 position = storedCell.IsValid ? storedCell : Position;
        storedThing.Position = position;
        storedThing.Rotation = storedRotation;


        // 退还建筑材料
        if (map != null)
        {
            GenSpawn.Refund(storedThing, map, CellRect.Empty);
        }
        else
        {
            // 地图无效，销毁建筑
            storedThing.Destroy();
        }

        storedCell = IntVec3.Invalid;
    }


    // ============================================================
    // 【分组大小获取方法】
    // ============================================================

    // ============================================================
    // 【获取分组最大尺寸】
    // ============================================================
    // 从 Mod 设置中获取分组最大尺寸
    //
    // 【默认值处理】
    // - 如果设置为 null，返回 20
    // - 如果设置值小于 1，返回 20
    //
    // 【典型用途】
    // 在分组操作前验证分组大小是否超过限制
    //
    // 【返回值】
    // - 分组最大尺寸（默认 20）
    // ============================================================
    private static int GetGroupMaxSize()
    {
        int maxSize = UniversalLiftStructureMod.Settings?.groupMaxSize ?? 20;
        if (maxSize < 1)
        {
            return 20;
        }

        return maxSize;
    }
}