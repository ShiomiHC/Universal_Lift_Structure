namespace Universal_Lift_Structure;

public static class ULS_AutoGroupUtility
{
    public static bool IsAutoController(Building_WallController controller)
    {
        return controller?.GetComp<ULS_AutoGroupMarker>() != null;
    }


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
            if (ULS_Utility.TryGetControllerAt(map, t, out Building_WallController controller) &&
                controller != null)
            {
                return IsAutoController(controller);
            }
        }

        return false;
    }


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
            if (!ULS_Utility.TryGetControllerAt(map, t, out Building_WallController c) || c == null)
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

            return true;
        }

        if (anyAuto)
        {
        }


        return true;
    }


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