namespace Universal_Lift_Structure;

// ============================================================
// 【Gizmo 禁用原因枚举】
// ============================================================
// 缓存升起/降下按钮的禁用原因，避免每帧重复计算
// ============================================================
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

// ============================================================
// 【墙体控制器：Gizmo 缓存扩展】
// ============================================================
// 实现 Gizmo 状态缓存，避免每帧重复计算
//
// 【设计思路】
// - 将"是否通电"、"组内状态"等检查从 UI 渲染循环移到 Tick 中
// - Gizmo 补丁仅读取缓存的标志位
// - 在关键状态变化时立即刷新缓存
// ============================================================
public partial class Building_WallController
{
    // ============================================================
    // 【缓存字段】
    // ============================================================

    // 升起按钮的禁用原因
    private GizmoDisableReason cachedRaiseDisableReason = GizmoDisableReason.None;

    // GroupTooLarge 时的组大小限制参数（用于翻译字符串）
    private int cachedGroupMaxSizeArg;

    // 缓存更新时的游戏 tick
    private int gizmoCacheTick = -1;

    // ============================================================
    // 【缓存属性】
    // ============================================================

    // 升起按钮禁用原因（只读）
    public GizmoDisableReason CachedRaiseDisableReason => cachedRaiseDisableReason;

    // 组大小限制参数（只读）
    public int CachedGroupMaxSizeArg => cachedGroupMaxSizeArg;

    // 缓存是否有效（事件驱动模式：由 InvalidateGizmoCache 标记失效）
    public bool IsGizmoCacheValid => gizmoCacheTick >= 0;

    // ============================================================
    // 【刷新 Gizmo 缓存】
    // ============================================================
    // 重新计算升起按钮的禁用状态
    //
    // 【调用时机】
    // - Tick() 中每 250 ticks 调用
    // - 关键状态变化时强制调用（分组变化/电力变化）
    // - Gizmo 渲染时如果缓存失效则调用
    // ============================================================
    public void RefreshGizmoCache()
    {
        gizmoCacheTick = Find.TickManager?.TicksGame ?? 0;

        // 重置缓存
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

        // 检查组是否存在或有效
        if (groupComp == null || groupId < 1 ||
            !groupComp.TryGetGroupControllerCells(groupId, out List<IntVec3> groupCells) ||
            groupCells == null || groupCells.Count == 0)
        {
            cachedRaiseDisableReason = GizmoDisableReason.NoStored;
            return;
        }

        // 检查组大小限制
        if (groupCells.Count > groupMaxSize)
        {
            cachedRaiseDisableReason = GizmoDisableReason.GroupTooLarge;
            cachedGroupMaxSizeArg = groupMaxSize;
            return;
        }

        // 检查组内成员状态
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

        // 控制台模式检查
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

    // ============================================================
    // 【标记缓存失效】
    // ============================================================
    // 强制下次 Gizmo 渲染时刷新缓存
    //
    // 【调用场景】
    // - 分组变化时
    // - 电力状态变化时
    // - 存储状态变化时
    // ============================================================
    public void InvalidateGizmoCache()
    {
        gizmoCacheTick = -1;
    }

    // ============================================================
    // 【获取禁用原因的翻译字符串】
    // ============================================================
    // 将枚举转换为对应的翻译 key
    //
    // - reason: 禁用原因枚举
    // - groupMaxSizeArg: GroupTooLarge 时的参数
    //
    // 【返回值】
    // - 翻译后的字符串
    // ============================================================
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

    // ============================================================
    // 【验证单格建筑降下可行性】
    // ============================================================
    // 供 Patch_Building_GetGizmos 调用，验证单格建筑是否可以降下
    // 注意：与 RefreshGizmoCache 不同，此方法检查的是「降下」而非「升起」的条件
    //
    // - disableReason: 输出参数，禁用原因字符串
    //
    // 【返回值】
    // - true: 可以降下
    // - false: 不能降下，disableReason 包含原因
    // ============================================================
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

        // 检查组是否存在或有效
        if (groupComp == null || groupId < 1 ||
            !groupComp.TryGetGroupControllerCells(groupId, out List<IntVec3> groupCells) ||
            groupCells == null || groupCells.Count == 0)
        {
            return true; // 无分组，默认允许
        }

        // 检查组大小限制
        if (groupCells.Count > groupMaxSize)
        {
            disableReason = "ULS_GroupTooLarge".Translate(groupMaxSize);
            return false;
        }

        // 检查组内成员状态（针对降下操作）
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

        // 优先显示自身断电，其次显示组内部分断电
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

    // ============================================================
    // 【验证多格建筑降下可行性】
    // ============================================================
    // 供 Patch_Building_GetGizmos 调用，验证多格建筑是否可以降下
    //
    // - building: 要降下的多格建筑
    // - disableReason: 输出参数，禁用原因字符串
    //
    // 【返回值】
    // - true: 可以降下
    // - false: 不能降下，disableReason 包含原因
    // ============================================================
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
