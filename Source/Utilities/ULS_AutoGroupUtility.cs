namespace Universal_Lift_Structure;


/// 文件意图：封装“自动组”判定与过滤逻辑。
/// - 统一控制“混组禁止”的口径。
/// - 统一 Pawn 关系（敌/友/中立）判定口径。

public static class ULS_AutoGroupUtility
{
    public static bool IsAutoController(Building_WallController controller)
    {
        return controller?.GetComp<ULS_AutoGroupMarker>() != null;
    }

    
    /// 方法意图：判定目标组是否为“自动组”。
    /// 说明：该判定仅用于“是否允许混组”的口径；过滤类型由 ULS_AutoGroupMapComponent 维护。
    
    public static bool IsAutoGroup(Map map, int groupId)
    {
        if (map == null || groupId < 1)
        {
            return false;
        }

        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp == null || !groupComp.TryGetGroupControllerCells(groupId, out var cells) || cells == null || cells.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            if (ULS_Utility.TryGetControllerAt(map, cells[i], out Building_WallController controller) && controller != null)
            {
                return IsAutoController(controller);
            }
        }

        return false;
    }

    
    /// 方法意图：用于“放置时自动合并组”的候选过滤，避免自动/手动混组被放置逻辑扩散。
    /// 规则：候选组内只要存在任一控制器与期望类型（自动/手动）不一致，则视为不兼容。
    
    public static bool IsGroupCompatibleForAutoMerge(Map map, int groupId, bool wantAuto)
    {
        if (map == null || groupId < 1)
        {
            return false;
        }

        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp == null || !groupComp.TryGetGroupControllerCells(groupId, out var cells) || cells == null || cells.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            if (!ULS_Utility.TryGetControllerAt(map, cells[i], out Building_WallController c) || c == null)
            {
                continue;
            }

            bool isAuto = IsAutoController(c);
            if (isAuto != wantAuto)
            {
                return false;
            }
        }

        return true;
    }

    
    /// 方法意图：判定“将 selectedControllers 分配/并入 targetGroupId”是否允许。
    /// 规则：
    /// - 手动组与自动组不可混组。
    
    public static bool CanAssignControllersToGroup(Map map, List<Building_WallController> selectedControllers,
        int targetGroupId, out string rejectKey)
    {
        rejectKey = null;
        if (map == null || selectedControllers == null || selectedControllers.Count == 0 || targetGroupId < 1)
        {
            return true;
        }

        // 计算“被移动控制器”的类型（必须一致）。
        bool anyAuto = false;
        bool anyManual = false;

        for (int i = 0; i < selectedControllers.Count; i++)
        {
            Building_WallController c = selectedControllers[i];
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

        if (anyAuto && anyManual)
        {
            rejectKey = "ULS_AutoGroup_MixAutoAndManual";
            return false;
        }

        bool targetIsAuto = IsAutoGroup(map, targetGroupId);
        if (anyManual)
        {
            if (targetIsAuto)
            {
                rejectKey = "ULS_AutoGroup_MixAutoAndManual";
                return false;
            }

            return true;
        }

        if (anyAuto)
        {
            return true;
        }

        // 全部未知/无效：不拦截。
        return true;
    }

    
    /// 方法意图：判定 Pawn 是否满足指定自动组类型。
    /// 口径：
    /// - Hostile：HostileTo(player)
    /// - Friendly：Faction!=null 且 非 HostileTo(player)
    /// - Neutral：Faction==null 且 非 HostileTo(player)
    
    public static bool PawnMatchesGroupType(Pawn pawn, ULS_AutoGroupType type)
    {
        if (pawn is null or { Destroyed: true } or { Dead: true } or { Spawned: false })
        {
            return false;
        }

        Faction playerFaction = Faction.OfPlayer;
        bool hostileToPlayer = pawn.HostileTo(playerFaction);

        return type switch
        {
            ULS_AutoGroupType.Hostile => hostileToPlayer,
            ULS_AutoGroupType.Friendly => !hostileToPlayer && pawn.Faction is not null,
            _ => !hostileToPlayer && pawn.Faction is null // Neutral
        };
    }
}
