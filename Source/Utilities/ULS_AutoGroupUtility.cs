namespace Universal_Lift_Structure;

public static class ULS_AutoGroupUtility
{
    public static bool IsAutoController(Building_WallController controller)
    {
        return controller?.GetComp<ULS_AutoGroupMarker>() != null;
    }

    // 取分组中第一个有效控制器的类型代表整组类型；
    // 假设同一分组内所有控制器类型一致（都是自动或都是手动）
    public static bool IsAutoGroup(Map map, int groupId)
    {
        if (map == null || groupId < 1)
        {
            return false;
        }

        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp == null || !groupComp.TryGetGroupControllerCells(groupId, out var cells) || cells == null ||
            cells.Count == 0)
        {
            return false;
        }

        foreach (var t in cells)
        {
            if (ULS_Utility.TryGetControllerAt(map, t, out Building_WallController controller))
            {
                return IsAutoController(controller);
            }
        }

        return false;
    }

    // 合并分组前检查类型兼容性，防止自动分组和手动分组混合
    public static bool IsGroupCompatibleForAutoMerge(Map map, int groupId, bool wantAuto)
    {
        if (map == null || groupId < 1)
        {
            return false;
        }

        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp == null || !groupComp.TryGetGroupControllerCells(groupId, out var cells) || cells == null ||
            cells.Count == 0)
        {
            return false;
        }

        foreach (var t in cells)
        {
            if (!ULS_Utility.TryGetControllerAt(map, t, out Building_WallController c))
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

    // 验证控制器列表是否可以分配到目标分组：
    // - 不允许自动和手动控制器混合
    // - 手动控制器不能分配到自动分组
    public static bool CanAssignControllersToGroup(Map map, List<Building_WallController> selectedControllers,
        int targetGroupId, out string rejectKey)
    {
        rejectKey = null;
        if (map == null || selectedControllers == null || selectedControllers.Count == 0 || targetGroupId < 1)
        {
            return true;
        }

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
        }

        return true;
    }

    // Hostile：敌对玩家；Friendly：有阵营且不敌对；其余为 Wildlife（无阵营且不敌对）
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
            _ => !hostileToPlayer && pawn.Faction is null
        };
    }
}
