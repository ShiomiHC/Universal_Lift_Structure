namespace Universal_Lift_Structure;

// 缓存升起/降下按钮的禁用原因，避免每帧重复计算
public enum GizmoDisableReason
{
    // 未禁用，按钮可用
    None,

    // 无存储的建筑
    NoStored,

    // 组大小超过限制
    GroupTooLarge,

    // 升降进行中
    LiftInProcess,

    // 当前控制器断电（自身断电）
    PowerOff,

    // 组内存在未供电的控制器（部分断电）
    GroupPowerInsufficient,

    // 缺少控制台（Console 模式）
    ConsoleMissing,

    // 控制台断电（Console 模式）
    ConsolePowerOff,

    // 非玩家所有
    NotPlayerOwned,
}

public partial class Building_WallController
{

    // 升起按钮的禁用原因
    private GizmoDisableReason cachedRaiseDisableReason = GizmoDisableReason.None;

    // GroupTooLarge 时的组大小限制参数（用于翻译字符串）
    private int cachedGroupMaxSizeArg;

    // 缓存更新时的游戏 tick
    private int gizmoCacheTick = -1;


    // 升起按钮禁用原因（只读）
    public GizmoDisableReason CachedRaiseDisableReason => cachedRaiseDisableReason;

    // 组大小限制参数（只读）
    public int CachedGroupMaxSizeArg => cachedGroupMaxSizeArg;

    // 缓存是否有效（事件驱动模式：由 InvalidateGizmoCache 标记失效）
    public bool IsGizmoCacheValid => gizmoCacheTick >= 0;

    public void RefreshGizmoCache()
    {
        gizmoCacheTick = Find.TickManager?.TicksGame ?? 0;

        cachedRaiseDisableReason = GizmoDisableReason.None;
        cachedGroupMaxSizeArg = 0;

        Map currentMap = Map;
        if (currentMap == null)
        {
            cachedRaiseDisableReason = GizmoDisableReason.NoStored;
            return;
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        int groupMaxSize = settings?.groupMaxSize ?? 20;
        if (groupMaxSize < 1) groupMaxSize = 20;

        ULS_ControllerGroupMapComponent groupComp = cachedGroupComp;
        int groupId = controllerGroupId;

        if (groupComp == null || groupId < 1 ||
            !groupComp.TryGetGroupControllerCells(groupId, out List<IntVec3> groupCells) ||
            groupCells == null || groupCells.Count == 0)
        {
            cachedRaiseDisableReason = GizmoDisableReason.NoStored;
            return;
        }

        if (groupCells.Count > groupMaxSize)
        {
            cachedRaiseDisableReason = GizmoDisableReason.GroupTooLarge;
            cachedGroupMaxSizeArg = groupMaxSize;
            return;
        }

        bool hasStored = false;
        bool isBusy = false;
        bool selfPowerOff = false; // 当前控制器自身断电
        bool otherPowerIssue = false; // 组内其他控制器断电

        foreach (var cell in groupCells)
        {
            if (ULS_Utility.TryGetControllerAt(currentMap, cell, out Building_WallController controller))
            {
                if (controller.HasStored) hasStored = true;
                if (controller.InLiftProcess) isBusy = true;
                if (settings is { enableLiftPower: true } && !controller.IsReadyForLiftPower())
                {
                    // 区分自身断电和组内其他控制器断电
                    if (controller == this)
                    {
                        selfPowerOff = true;
                    }
                    else
                    {
                        otherPowerIssue = true;
                    }
                }

                // 如果已确认有存储，且发现了任意阻碍条件，则可提前终止
                if (hasStored && (isBusy || selfPowerOff || otherPowerIssue)) break;
            }
        }

        if (!hasStored)
        {
            cachedRaiseDisableReason = GizmoDisableReason.NoStored;
            return;
        }

        if (isBusy)
        {
            cachedRaiseDisableReason = GizmoDisableReason.LiftInProcess;
            return;
        }

        // 优先显示自身断电，其次显示组内部分断电
        if (selfPowerOff)
        {
            cachedRaiseDisableReason = GizmoDisableReason.PowerOff;
            return;
        }

        if (otherPowerIssue)
        {
            cachedRaiseDisableReason = GizmoDisableReason.GroupPowerInsufficient;
            return;
        }

        if ((settings?.liftControlMode ?? LiftControlMode.Remote) == LiftControlMode.Console)
        {
            if (!ULS_Utility.TryGetNearestLiftConsoleByDistance(currentMap, Position, out _))
            {
                ThingDef consoleDef = DefDatabase<ThingDef>.GetNamedSilentFail("ULS_LiftConsole");
                bool anyConsoleExists = consoleDef != null && currentMap.listerThings.ThingsOfDef(consoleDef)
                    .Any(t => t.Faction == Faction.OfPlayer);

                cachedRaiseDisableReason = anyConsoleExists
                    ? GizmoDisableReason.ConsolePowerOff
                    : GizmoDisableReason.ConsoleMissing;
            }
        }
    }

    public void InvalidateGizmoCache()
    {
        gizmoCacheTick = -1;
    }

    public static string GetDisableReasonString(GizmoDisableReason reason, int groupMaxSizeArg = 0)
    {
        return reason switch
        {
            GizmoDisableReason.NoStored => "ULS_NoStored".Translate(),
            GizmoDisableReason.GroupTooLarge => "ULS_GroupTooLarge".Translate(groupMaxSizeArg),
            GizmoDisableReason.LiftInProcess => "ULS_LiftInProcess".Translate(),
            GizmoDisableReason.PowerOff => "ULS_PowerOff".Translate(),
            GizmoDisableReason.GroupPowerInsufficient => "ULS_GroupPowerInsufficient".Translate(),
            GizmoDisableReason.ConsoleMissing => "ULS_LiftConsoleMissing".Translate(),
            GizmoDisableReason.ConsolePowerOff => "ULS_LiftConsolePowerOff".Translate(),
            GizmoDisableReason.NotPlayerOwned => "ULS_LowerNotPlayerOwned".Translate(),
            _ => string.Empty,
        };
    }

    public bool CanLowerSingleCellBuilding(out string disableReason)
    {
        disableReason = string.Empty;

        Map currentMap = Map;
        if (currentMap == null)
        {
            return true; // 无法验证，默认允许
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        int groupMaxSize = settings?.groupMaxSize ?? 20;
        if (groupMaxSize < 1) groupMaxSize = 20;

        ULS_ControllerGroupMapComponent groupComp = cachedGroupComp;
        int groupId = controllerGroupId;

        if (groupComp == null || groupId < 1 ||
            !groupComp.TryGetGroupControllerCells(groupId, out List<IntVec3> groupCells) ||
            groupCells == null || groupCells.Count == 0)
        {
            return true; // 无分组，默认允许
        }

        if (groupCells.Count > groupMaxSize)
        {
            disableReason = "ULS_GroupTooLarge".Translate(groupMaxSize);
            return false;
        }

        bool selfPowerOff = false;
        bool otherPowerIssue = false;

        foreach (var cell in groupCells)
        {
            if (ULS_Utility.TryGetControllerAt(currentMap, cell, out Building_WallController controller))
            {
                // 运行状态检测
                if (controller.InLiftProcess)
                {
                    disableReason = "ULS_LiftInProcess".Translate();
                    return false;
                }

                // 电力检测（区分自身和组内其他控制器）
                if (settings is { enableLiftPower: true } && !controller.IsReadyForLiftPower())
                {
                    if (controller == this)
                    {
                        selfPowerOff = true;
                    }
                    else
                    {
                        otherPowerIssue = true;
                    }
                }
            }
        }

        if (selfPowerOff)
        {
            disableReason = "ULS_PowerOff".Translate();
            return false;
        }

        if (otherPowerIssue)
        {
            disableReason = "ULS_GroupPowerInsufficient".Translate();
            return false;
        }

        return true;
    }

    public bool CanLowerMultiCellBuilding(Building building, out string disableReason)
    {
        disableReason = string.Empty;

        if (building == null || !building.Spawned)
        {
            return false;
        }

        Map map = building.Map;
        IntVec3 rootCell = building.Position;
        CellRect rect = building.OccupiedRect();

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        int groupMaxSize = settings?.groupMaxSize ?? 20;
        if (groupMaxSize < 1) groupMaxSize = 20;

        // 根位置必须有控制器
        if (!ULS_Utility.TryGetControllerAt(map, rootCell, out _))
        {
            disableReason = "ULS_MultiCellNeedControllerEveryCell".Translate();
            return false;
        }

        // 检查是否与多格组冲突
        ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();
        if (multiCellComp != null && multiCellComp.HasGroup(rootCell))
        {
            disableReason = "ULS_MultiCellGroupAlreadyExists".Translate();
            return false;
        }

        ULS_ControllerGroupMapComponent ctrlGroupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();

        // 遍历多格建筑占用的每一个格子
        foreach (IntVec3 cell in rect)
        {
            if (!ULS_Utility.TryGetControllerAt(map, cell, out Building_WallController c))
            {
                disableReason = "ULS_MultiCellNeedControllerEveryCell".Translate();
                return false;
            }

            // 组超限检测
            int cellGroupId = c.ControllerGroupId;
            if (ctrlGroupComp != null && cellGroupId > 0 &&
                ctrlGroupComp.TryGetGroupControllerCells(cellGroupId, out List<IntVec3> cellGroupCells) &&
                cellGroupCells != null && cellGroupCells.Count > groupMaxSize)
            {
                disableReason = "ULS_GroupTooLarge".Translate(groupMaxSize);
                return false;
            }

            // 运行状态检测
            if (c.InLiftProcessForUI)
            {
                disableReason = "ULS_LiftInProcess".Translate();
                return false;
            }

            // 电力检测
            if (settings is { enableLiftPower: true } && !c.IsReadyForLiftPower())
            {
                disableReason = "ULS_PowerOff".Translate();
                return false;
            }

            // 存储状态检测
            if (c.HasStored)
            {
                disableReason = "ULS_MultiCellControllerHasStored".Translate();
                return false;
            }

            // 多格组归属检测
            if (c.MultiCellGroupRootCell.IsValid)
            {
                disableReason = "ULS_MultiCellControllerInGroup".Translate();
                return false;
            }
        }

        return true;
    }
}
