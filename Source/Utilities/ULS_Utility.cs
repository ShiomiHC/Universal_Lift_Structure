namespace Universal_Lift_Structure;

// ============================================================
// 【通用工具类】
// ============================================================
// 此类提供与控制器相关的通用工具方法
//
// 【核心职责】
// 1. 控制器查询：在指定位置或建筑下查找控制器
// 2. 黑名单检查：判断建筑物是否在黑名单中
// 3. Gizmo 注入判断：判断是否可以为建筑注入降下按钮
// 4. 控制台查询：查找最近的可用升降控制台
// 5. 哈希计算：计算单元格集合的成员哈希值
//
// 【设计模式】
// - 静态工具类：所有方法都是静态方法，无需实例化
// - 纯函数设计：大部分方法无副作用，可安全并发调用
// ============================================================

public static class ULS_Utility
{
    // ============================================================
    // 【控制器查询方法】
    // ============================================================

    // ============================================================
    // 【控制器查询方法】
    // ============================================================
    // 尝试在指定地图的指定单元格获取控制器
    //
    // 【判断逻辑】
    // 遍历单元格上的所有物体，找到第一个 Building_WallController 实例
    //
    // 【参数说明】
    // - map: 目标地图
    // - cell: 目标单元格
    // - controller: 输出参数：找到的控制器（未找到时为 null）
    //
    // 【返回值】
    // - whether found: 是否找到控制器
    // ============================================================
    public static bool TryGetControllerAt(Map map, IntVec3 cell, out Building_WallController controller)
    {
        controller = null;
        // 地图无效则直接返回
        if (map == null)
        {
            return false;
        }

        // 获取单元格上的所有物体
        var things = map.thingGrid.ThingsListAtFast(cell);
        foreach (var t in things)
        {
            // 找到第一个控制器实例
            if (t is Building_WallController found)
            {
                controller = found;
                return true;
            }
        }

        return false;
    }


    // ============================================================
    // 【查找建筑下的控制器】
    // ============================================================
    // 尝试在建筑物占据的任意单元格下查找控制器
    //
    // 【典型用途】
    // 用于多格建筑，检查其占据的任意单元格是否有控制器
    //
    // 【参数说明】
    // - building: 目标建筑物
    // - controller: 输出参数：找到的控制器
    // - controllerCell: 输出参数：控制器所在的单元格
    //
    // 【返回值】
    // - whether found: 是否找到控制器
    // ============================================================
    public static bool TryGetAnyControllerUnderBuilding(Building building, out Building_WallController controller,
        out IntVec3 controllerCell)
    {
        controller = null;
        controllerCell = IntVec3.Invalid;

        Map map = building?.Map;
        if (map == null)
        {
            return false;
        }

        // 遍历建筑占据的所有单元格
        CellRect rect = building.OccupiedRect();
        foreach (IntVec3 cell in rect)
        {
            if (TryGetControllerAt(map, cell, out controller))
            {
                controllerCell = cell;
                return true;
            }
        }

        return false;
    }

    // ============================================================
    // 【检查重复控制器】
    // ============================================================
    // 检查建筑物下是否存在除指定控制器外的其他控制器
    //
    // 【典型用途】
    // 用于防止多个控制器重叠管理同一建筑
    //
    // 【参数说明】
    // - building: 目标建筑物
    // - except: 需排除的控制器（通常是当前控制器）
    //
    // 【返回值】
    // - whether exists: 是否存在其他控制器
    // ============================================================
    public static bool AnyControllerUnderBuildingExcept(Building building, Building_WallController except)
    {
        if (building?.Map == null) return false;
        CellRect rect = building.OccupiedRect();
        foreach (IntVec3 cell in rect)
        {
            // 找到控制器且不是排除的那个
            if (TryGetControllerAt(building.Map, cell, out Building_WallController controller) && controller != except)
            {
                return true;
            }
        }

        return false;
    }


    // ============================================================
    // 【黑名单检查方法】
    // ============================================================

    // ============================================================
    // 【黑名单检查方法】
    // ============================================================
    // 检查建筑物是否在黑名单中（不可被控制器管理）
    //
    // 【黑名单规则】
    // 1. 门类建筑：所有门都在黑名单中（无论是否可升降）
    // 2. 天然岩石：根据设置决定是否排除
    // 3. defName 黑名单：用户自定义的黑名单列表
    //
    // 【参数说明】
    // - edifice: 待检查的建筑物
    //
    // 【返回值】
    // - is blacklisted: 是否在黑名单中
    // ============================================================
    public static bool IsEdificeBlacklisted(Building edifice)
    {
        // null 视为在黑名单中（安全处理）
        if (edifice is null)
        {
            return true;
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        if (settings is null)
        {
            return false;
        }

        // 门类建筑始终排除
        if (edifice is Building_Door || edifice.def?.IsDoor == true)
        {
            return true;
        }

        // 根据设置排除天然岩石
        if (settings.excludeNaturalRock && edifice.def?.building is { isNaturalRock: true })
        {
            return true;
        }

        // 检查 defName 黑名单
        if (settings.IsDefNameBlacklisted(edifice.def?.defName))
        {
            return true;
        }

        return false;
    }


    // ============================================================
    // 【Gizmo 注入判断方法】
    // ============================================================

    // ============================================================
    // 【Gizmo 注入判断方法】
    // ============================================================
    // 检查是否可以为建筑注入"降下"Gizmo 按钮
    // \n
    // 【注入条件】
    // 1. 建筑物存在且未被摧毁
    // 2. 不是框架（Frame）
    // 3. 是建筑类物体（isEdifice）
    // 4. 可被摧毁（destroyable）
    // 5. 不在黑名单中
    //
    // 【参数说明】
    // - edifice: 待检查的建筑物
    //
    // 【返回值】
    // - can inject: 是否可以注入 Gizmo
    // ============================================================
    public static bool CanInjectLowerGizmo(Building edifice)
    {
        // 无效或已摧毁或是框架，不能注入
        if (edifice is null or { Destroyed: true } or Frame)
        {
            return false;
        }

        ThingDef def = edifice.def;
        if (def is null)
        {
            return false;
        }

        // 必须是建筑类物体
        if (def.building is null || !def.building.isEdifice)
        {
            return false;
        }

        // 必须可被摧毁
        if (!def.destroyable)
        {
            return false;
        }

        // 不在黑名单中
        if (IsEdificeBlacklisted(edifice))
        {
            return false;
        }

        return true;
    }


    // ============================================================
    // 【控制台查询方法】
    // ============================================================

    // ============================================================
    // 【控制台查询方法】
    // ============================================================
    // 尝试获取距离指定位置最近的可用升降控制台
    //
    // 【筛选条件】
    // 1. 已生成在地图上
    // 2. 属于玩家阵营
    // 3. 已供电（如果需要电力）
    //
    // 【距离计算】
    // 使用平方距离（DistanceToSquared）避免开方运算，提高性能
    //
    // 【参数说明】
    // - map: 目标地图
    // - origin: 起始位置
    // - console: 输出参数：找到的控制台
    //
    // 【返回值】
    // - whether found: 是否找到可用控制台
    // ============================================================
    public static bool TryGetNearestLiftConsoleByDistance(Map map, IntVec3 origin, out ThingWithComps console)
    {
        console = null;
        if (map == null)
        {
            return false;
        }

        // 获取控制台的 Def 定义
        ThingDef consoleDef = ULS_ThingDefOf.ULS_LiftConsole;
        if (consoleDef == null)
        {
            return false;
        }

        // 获取地图上所有控制台实例
        List<Thing> consoles = map.listerThings.ThingsOfDef(consoleDef);
        if (consoles == null || consoles.Count <= 0)
        {
            return false;
        }

        // 查找最近的可用控制台
        int bestDistSq = int.MaxValue;
        foreach (var t in consoles)
        {
            if (!(t is ThingWithComps twc) || !twc.Spawned)
            {
                continue;
            }

            // 必须是玩家阵营
            if (twc.Faction != Faction.OfPlayer)
            {
                continue;
            }

            // 计算平方距离（避免开方提高性能）
            int distSq = origin.DistanceToSquared(twc.Position);
            if (distSq < bestDistSq)
            {
                // 检查电力状态（如果有电力组件）
                var power = twc.GetComp<CompPowerTrader>();
                if (power is { PowerOn: false })
                {
                    continue;
                }

                // 更新最近距离和控制台
                bestDistSq = distSq;
                console = twc;
            }
        }

        return console != null;
    }


    // ============================================================
    // 【哈希计算方法】
    // ============================================================

    // ============================================================
    // 【哈希计算方法】
    // ============================================================
    // 计算一组单元格坐标的成员哈希值（用于缓存校验）
    //
    // 【用途】
    // 用于快速判断两个单元格集合是否相同（例如分组成员是否变化）
    //
    // 【算法】
    // 使用简单的多项式滚动哈希：hash = hash * 31 + value
    //
    // 【注意】
    // - 此哈希不是加密哈希，仅用于快速比较
    // - 不同的单元格顺序会产生不同的哈希值
    // - unchecked 允许整数溢出（这在哈希计算中是安全的）
    //
    // 【参数说明】
    // - cells: 单元格列表
    //
    // 【返回值】
    // - hash: 哈希值
    // ============================================================
    public static int ComputeMembershipHash(List<IntVec3> cells)
    {
        unchecked
        {
            int h = 17; // 初始种子值
            if (cells != null)
            {
                foreach (var c in cells)
                {
                    // 对每个坐标的 x 和 z 分量进行哈希
                    h = h * 31 + c.x;
                    h = h * 31 + c.z;
                }
            }

            return h;
        }
    }
}