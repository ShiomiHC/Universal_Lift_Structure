namespace Universal_Lift_Structure;

// ============================================================
// 【自动分组工具类】
// ============================================================
// 此类提供自动分组相关的判断和验证方法
//
// 【核心职责】
// 1. 自动控制器判断：检查控制器是否带有自动分组标记
// 2. 自动分组判断：检查分组是否为自动分组
// 3. 兼容性检查：验证分组是否可以与自动分组合并
// 4. 分配验证：验证控制器列表是否可以分配到目标分组
// 5. Pawn 类型匹配：判断 Pawn 是否匹配自动分组类型
//
// 【设计模式】
// - 静态工具类：所有方法都是静态方法，无需实例化
// - 验证职责分离：每个方法专注于一种验证逻辑
//
// 【自动分组机制】
// 自动分组是通过 ULS_AutoGroupMarker 组件标记的分组
// 自动分组和手动分组不能混合，必须保持类型一致性
// ============================================================

public static class ULS_AutoGroupUtility
{
    // ============================================================
    // 【自动控制器判断】
    // ============================================================

    // ============================================================
    // 【自动控制器判断】
    // ============================================================
    // 检查控制器是否为自动控制器（带有自动分组标记）
    //
    // 【判断依据】
    // 控制器是否带有 ULS_AutoGroupMarker 组件
    //
    // 【参数说明】
    // - controller: 待检查的控制器
    //
    // 【返回值】
    // - true: 是自动控制器
    // ============================================================
    public static bool IsAutoController(Building_WallController controller)
    {
        return controller?.GetComp<ULS_AutoGroupMarker>() != null;
    }


    // ============================================================
    // 【自动分组判断】
    // ============================================================

    // ============================================================
    // 【自动分组判断】
    // ============================================================
    // 检查分组是否为自动分组
    //
    // 【判断逻辑】
    // 1. 获取分组中的所有控制器单元格
    // 2. 检查第一个有效控制器是否为自动控制器
    // 3. 只要有一个控制器是自动控制器，整个分组就是自动分组
    //
    // 【注意】
    // 此方法假设同一分组中的所有控制器类型一致（都是自动或都是手动）
    //
    // 【参数说明】
    // - map: 目标地图
    // - groupId: 分组 ID
    //
    // 【返回值】
    // - true: 是自动分组
    // ============================================================
    public static bool IsAutoGroup(Map map, int groupId)
    {
        // 参数验证
        if (map == null || groupId < 1)
        {
            return false;
        }

        // 获取分组管理组件
        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp == null || !groupComp.TryGetGroupControllerCells(groupId, out var cells) || cells == null ||
            cells.Count == 0)
        {
            return false;
        }

        // 检查第一个有效控制器的类型
        foreach (var t in cells)
        {
            if (ULS_Utility.TryGetControllerAt(map, t, out Building_WallController controller))
            {
                return IsAutoController(controller);
            }
        }

        return false;
    }


    // ============================================================
    // 【兼容性检查】
    // ============================================================

    // ============================================================
    // 【兼容性检查】
    // ============================================================
    // 检查分组是否与自动分组兼容（可以合并）
    //
    // 【兼容规则】
    // - 如果 wantAuto=true：分组中所有控制器都必须是自动控制器
    // - 如果 wantAuto=false：分组中所有控制器都必须是手动控制器
    //
    // 【典型用途】
    // 在合并分组前检查类型兼容性，防止自动分组和手动分组混合
    //
    // 【参数说明】
    // - map: 目标地图
    // - groupId: 分组 ID
    // - wantAuto: 期望的自动分组类型（true=自动，false=手动）
    //
    // 【返回值】
    // - true: 兼容
    // ============================================================
    public static bool IsGroupCompatibleForAutoMerge(Map map, int groupId, bool wantAuto)
    {
        // 参数验证
        if (map == null || groupId < 1)
        {
            return false;
        }

        // 获取分组管理组件
        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp == null || !groupComp.TryGetGroupControllerCells(groupId, out var cells) || cells == null ||
            cells.Count == 0)
        {
            return false;
        }

        // 检查所有控制器的类型是否一致
        foreach (var t in cells)
        {
            if (!ULS_Utility.TryGetControllerAt(map, t, out Building_WallController c))
            {
                continue;
            }

            bool isAuto = IsAutoController(c);
            // 如果类型不匹配，返回不兼容
            if (isAuto != wantAuto)
            {
                return false;
            }
        }

        return true;
    }


    // ============================================================
    // 【分配验证】
    // ============================================================

    // ============================================================
    // 【分配验证】
    // ============================================================
    // 检查控制器列表是否可以分配到目标分组
    //
    // 【验证规则】
    // 1. 控制器列表不能同时包含自动控制器和手动控制器
    // 2. 手动控制器不能分配到自动分组
    //
    // 【拒绝原因】
    // - "ULS_AutoGroup_MixAutoAndManual"：混合了自动和手动控制器
    //
    // 【典型用途】
    // 在执行分组操作前验证操作的合法性
    //
    // 【参数说明】
    // - map: 目标地图
    // - selectedControllers: 待分配的控制器列表
    // - targetGroupId: 目标分组 ID
    // - rejectKey: 输出参数：拒绝原因的翻译键（如果不可分配）
    //
    // 【返回值】
    // - true: 可以分配
    // ============================================================
    public static bool CanAssignControllersToGroup(Map map, List<Building_WallController> selectedControllers,
        int targetGroupId, out string rejectKey)
    {
        rejectKey = null;
        // 参数验证
        if (map == null || selectedControllers == null || selectedControllers.Count == 0 || targetGroupId < 1)
        {
            return true;
        }


        // 检查选中的控制器列表中是否混合了自动和手动控制器
        bool anyAuto = false;
        bool anyManual = false;

        foreach (var c in selectedControllers)
        {
            if (c == null || c.Map != map)
            {
                continue;
            }

            if (IsAutoController(c))
            {
                anyAuto = true;
            }
            else
            {
                anyManual = true;
            }
        }

        // 不允许自动和手动控制器混合
        if (anyAuto && anyManual)
        {
            rejectKey = "ULS_AutoGroup_MixAutoAndManual";
            return false;
        }

        // 检查目标分组的类型
        bool targetIsAuto = IsAutoGroup(map, targetGroupId);
        if (anyManual)
        {
            // 手动控制器不能分配到自动分组
            if (targetIsAuto)
            {
                rejectKey = "ULS_AutoGroup_MixAutoAndManual";
                return false;
            }
        }


        return true;
    }


    // ============================================================
    // 【Pawn 类型匹配】
    // ============================================================

    // ============================================================
    // 【Pawn 类型匹配】
    // ============================================================
    // 检查 Pawn 是否匹配指定的自动分组类型
    //
    // 【分组类型规则】
    // - Hostile（敌对）：Pawn 对玩家阵营敌对
    // - Friendly（友好）：Pawn 有阵营且不敌对玩家
    // - Wildlife（野生）：Pawn 无阵营且不敌对玩家
    //
    // 【验证条件】
    // Pawn 必须存在、未摧毁、未死亡、已生成在地图上
    //
    // 【典型用途】
    // 自动分组扫描时判断 Pawn 是否符合设定的过滤条件
    //
    // 【参数说明】
    // - pawn: 待检查的 Pawn
    // - type: 自动分组类型
    //
    // 【返回值】
    // - true: 匹配
    // ============================================================
    public static bool PawnMatchesGroupType(Pawn pawn, ULS_AutoGroupType type)
    {
        // Pawn 必须有效且存活
        if (pawn is null or { Destroyed: true } or { Dead: true } or { Spawned: false })
        {
            return false;
        }

        Faction playerFaction = Faction.OfPlayer;
        bool hostileToPlayer = pawn.HostileTo(playerFaction);

        // 根据分组类型匹配
        return type switch
        {
            ULS_AutoGroupType.Hostile => hostileToPlayer, // 敌对类型：Pawn 必须敌对玩家
            ULS_AutoGroupType.Friendly => !hostileToPlayer && pawn.Faction is not null, // 友好类型：有阵营且不敌对
            _ => !hostileToPlayer && pawn.Faction is null // 野生类型：无阵营且不敌对
        };
    }
}