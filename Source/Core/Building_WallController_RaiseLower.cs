namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    // ============================================================
    // 【自动升起当前编组】
    // ============================================================
    // 对外接口：尝试升起当前编组
    //
    // 【返回值】
    // - true: 操作成功
    // ============================================================
    public bool AutoRaiseGroup()
    {
        return TryRaiseGroupAllOrNothing();
    }


    // ============================================================
    // 【尝试原子性升起编组】
    // ============================================================
    // 尝试原子性地升起整个编组 (要么全部成功，要么全部不执行)
    //
    // 【返回值】
    // - true: 升起成功
    // ============================================================
    private bool TryRaiseGroupAllOrNothing()
    {
        Map map = Map;
        if (map == null)
        {
            return false;
        }

        // 检查电力条件
        if (PowerFeatureEnabled && !IsReadyForLiftPower())
        {
            return false;
        }

        int groupMaxSize = GetGroupMaxSize();
        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        int groupId = controllerGroupId;

        // 获取并验证组内所有单元格
        if (groupComp == null ||
            groupId < 1 ||
            !groupComp.TryGetGroupControllerCells(groupId, out var cells) ||
            cells == null ||
            cells.Count == 0)
        {
            return false;
        }

        // 再次检查组大小限制
        if (cells.Count > groupMaxSize)
        {
            return false;
        }


        using var _1 = new PooledList<IntVec3>(out var uniqueRootCells);
        using var _2 = new PooledHashSet<IntVec3>(out var seenRoots);
        using var _3 = new PooledList<Building_WallController>(out var raisableControllers);
        using var _4 = new PooledHashSet<Building_WallController>(out var processedControllers);

        {
            // 筛选唯一的控制器根位置 (处理多格建筑)
            foreach (var cell in cells)
            {
                if (ULS_Utility.TryGetControllerAt(map, cell, out var controller))
                {
                    IntVec3 rootCell = controller.MultiCellGroupRootCell.IsValid
                        ? controller.MultiCellGroupRootCell
                        : controller.Position;

                    if (seenRoots.Add(rootCell))
                    {
                        uniqueRootCells.Add(rootCell);
                    }
                }
                else if (seenRoots.Add(cell))
                {
                    uniqueRootCells.Add(cell);
                }
            }


            int storedCount = 0;

            // 预检查：所有控制器是否都做好了升起准备
            foreach (var t in uniqueRootCells)
            {
                if (ULS_Utility.TryGetControllerAt(map, t, out var controller) &&
                    controller is { HasStored: true })
                {
                    storedCount++;

                    Thing storedThing = controller.StoredThing;
                    if (storedThing == null)
                    {
                        controller.storedCell = IntVec3.Invalid;
                        return false; // 状态异常，无法升起
                    }

                    IntVec3 spawnCell = controller.storedCell.IsValid ? controller.storedCell : controller.Position;
                    // 检查生成位置是否被阻挡
                    if (controller.IsBlockedForRaise(map, spawnCell, storedThing))
                    {
                        return false; // 存在阻挡，整体取消
                    }

                    raisableControllers.Add(controller);
                }
            }

            // 如果没有可升起的物体，或数量不匹配，则中止
            if (storedCount <= 0 || raisableControllers.Count != storedCount)
            {
                return false;
            }


            // 执行升起操作
            foreach (var controller in raisableControllers)
            {
                if (controller == null)
                {
                    continue;
                }

                using var _5 = new PooledHashSet<Building_WallController>(out var memberControllers);
                controller.GetMultiCellMemberControllersOrSelf(map, memberControllers);

                // 尝试启动升起流程
                if (!controller.TryStartRaisingProcess(map))
                {
                    // 只要有一个启动失败，回滚所有已处理的控制器
                    foreach (Building_WallController processed in processedControllers)
                    {
                        processed?.ClearLiftProcessAndRemoveBlocker();
                    }

                    return false;
                }

                // 记录已处理的控制器，以便回滚
                foreach (Building_WallController member in memberControllers)
                {
                    if (member != null)
                    {
                        processedControllers.Add(member);
                    }
                }
            }

            return true;
        }
    }


    // ============================================================
    // 【尝试升起编组】
    // ============================================================
    // 尝试升起整个编组 (允许部分成功)
    //
    // 【参数说明】
    // - showMessage: 是否显示提示消息
    // ============================================================
    private void TryRaiseGroup(bool showMessage)
    {
        Map map = Map;
        if (map == null)
        {
            return;
        }

        int groupMaxSize = GetGroupMaxSize();
        int groupId = controllerGroupId;

        // 获取有效组内单元格
        if (!TryGetValidGroupCells(map, groupId, groupMaxSize, showMessage, "ULS_NoStored", out var cells))
        {
            return;
        }

        using var _1 = new PooledList<IntVec3>(out var uniqueRootCells);
        using var _2 = new PooledHashSet<IntVec3>(out var seenRoots);
        using var _3 = new PooledList<Building_WallController>(out var raisableControllers);

        {
            BuildUniqueRootCells(map, cells, uniqueRootCells, seenRoots);

            int storedCount = 0;
            int failedCount = 0;

            // 检查电力是否就绪
            if (!CheckGroupPowerReady(map, uniqueRootCells, showMessage))
            {
                return;
            }

            // 遍历并筛选可升起的控制器
            foreach (var t in uniqueRootCells)
            {
                if (!ULS_Utility.TryGetControllerAt(map, t, out var controller) ||
                    !controller.HasStored)
                {
                    continue;
                }

                storedCount++;

                Thing storedThing = controller.StoredThing;
                if (storedThing == null)
                {
                    controller.storedCell = IntVec3.Invalid;
                    failedCount++;
                    continue;
                }

                IntVec3 spawnCell = controller.storedCell.IsValid ? controller.storedCell : controller.Position;
                // 检查是否被阻挡
                if (controller.IsBlockedForRaise(map, spawnCell, storedThing))
                {
                    failedCount++;
                }
                else
                {
                    raisableControllers.Add(controller);
                }
            }

            // 结果处理与消息提示
            if (raisableControllers.Count == 0)
            {
                if (storedCount <= 0)
                {
                    if (showMessage)
                    {
                        MessageReject("ULS_NoStored", this);
                    }
                }
                else if (showMessage)
                {
                    MessageReject("ULS_GroupNoRaiseable", this);
                }

                return;
            }

            // 对筛选出的控制器执行升起
            foreach (var t in raisableControllers)
            {
                if (!t.TryStartRaisingProcess(map))
                {
                    failedCount++;
                }
            }

            // 如果有部分失败，显示提示
            if (failedCount > 1 && showMessage)
            {
                MessageNeutral("ULS_GroupRaisedPartial", this, raisableControllers.Count, failedCount);
            }
        }
    }


    // ============================================================
    // 【尝试升起（无消息）】
    // ============================================================
    // 核心逻辑：执行升起的具体操作（生成物体、退费等）
    //
    // 【参数说明】
    // - map: 地图
    // ============================================================
    private void TryRaiseNoMessage(Map map)
    {
        if (!HasStored)
        {
            return;
        }

        Thing storedThing = StoredThing;
        if (storedThing == null)
        {
            storedCell = IntVec3.Invalid;
            storedThingMarketValueIgnoreHp = 0f;
            return;
        }

        IntVec3 spawnLoc = storedCell.IsValid ? storedCell : Position;

        // 从内部容器取出
        innerContainer.Remove(storedThing);
        storedThingMarketValueIgnoreHp = 0f;

        if (storedThing.Destroyed)
        {
            storedCell = IntVec3.Invalid;
            return;
        }

        // 默认生成位置回退
        if (!storedCell.IsValid)
        {
            storedCell = Position;
        }

        IntVec3 rootCell = multiCellGroupRootCell;

        // 生成物体
        GenSpawn.Spawn(storedThing, spawnLoc, map, storedRotation);
        storedCell = IntVec3.Invalid;

        // 清理缓存链接
        ClearStoredLinkMaskCache();

        // 如果是多格组，进行清理或重置
        if (rootCell.IsValid && map != null)
        {
            ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();
            if (multiCellComp != null)
            {
                multiCellComp.RemoveGroup(rootCell);
            }
            else
            {
                multiCellGroupRootCell = IntVec3.Invalid;
            }
        }

        // 成功升起后刷新缓存
        InvalidateGizmoCache();
    }


    // ============================================================
    // 【自动降下编组】
    // ============================================================
    // 对外接口：自动降下组内物体
    //
    // 【参数说明】
    // - startCell: 触发位置
    // ============================================================
    public void AutoLowerGroup(IntVec3 startCell)
    {
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        if (settings == null || !settings.enableLiftPower || IsReadyForLiftPower())
        {
            TryLowerGroup(startCell, showMessage: false);
        }
    }


    // ============================================================
    // 【尝试降下编组】
    // ============================================================
    // 尝试降下整个编组
    //
    // 【参数说明】
    // - startCell: 触发位置
    // - showMessage: 是否显示提示
    // ============================================================
    private void TryLowerGroup(IntVec3 startCell, bool showMessage)
    {
        Map map = Map;
        if (map == null)
        {
            return;
        }

        int groupMaxSize = GetGroupMaxSize();

        // 验证起始位置控制器
        if (!ULS_Utility.TryGetControllerAt(map, startCell, out var startController))
        {
            if (showMessage)
            {
                MessageReject("ULS_GroupNoController", this);
            }

            return;
        }

        int groupId = startController.ControllerGroupId;

        if (!TryGetValidGroupCells(map, groupId, groupMaxSize, showMessage, "ULS_GroupNoController", out var cells))
        {
            return;
        }

        using var _1 = new PooledList<IntVec3>(out var uniqueRootCells);
        using var _2 = new PooledHashSet<IntVec3>(out var seenRoots);
        using var _3 = new PooledHashSet<Building>(out var multiCellBuildingsToLow);
        using var _4 =
            new PooledList<(Building_WallController controller, IntVec3 position, Building edifice)>(
                out var oneCellLowerTargets);

        {
            BuildUniqueRootCells(map, cells, uniqueRootCells, seenRoots);

            // 电力检查
            if (!CheckGroupPowerReady(map, uniqueRootCells, showMessage))
            {
                return;
            }

            // 遍历并分类可降下的建筑 (单格与多格)
            foreach (var t in uniqueRootCells)
            {
                if (!ULS_Utility.TryGetControllerAt(map, t, out var controller))
                {
                    continue;
                }

                if (controller.InLiftProcess || controller.HasStored)
                {
                    continue;
                }

                Building edifice = map.edificeGrid[t];

                if (edifice == null ||
                    edifice.Destroyed ||
                    !edifice.Spawned ||
                    edifice is Frame ||
                    !edifice.def.destroyable)
                {
                    continue;
                }

                if (ULS_Utility.IsEdificeBlacklisted(edifice) || edifice.Faction != Faction.OfPlayer)
                {
                    continue;
                }

                if (edifice.def.Size == IntVec2.One)
                {
                    oneCellLowerTargets.Add((controller, t, edifice));
                }
                else
                {
                    multiCellBuildingsToLow.Add(edifice);
                }
            }


            int loweredCount = 0;
            int failedCount = 0;

            // 处理所有多格建筑的降下
            foreach (Building b in multiCellBuildingsToLow)
            {
                if (TryLowerMultiCellBuildingInternal(b, showMessage: false))
                {
                    loweredCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            // 预缓存所有单格建筑的链接掩码 (防止降下顺序导致的链接断裂)
            foreach (var t in oneCellLowerTargets)
            {
                if (t is { controller: not null, edifice: not null })
                {
                    t.controller.CacheStoredLinkMaskForBuilding(t.edifice, map);
                }
            }

            // 处理所有单格建筑的降下
            foreach (var t in oneCellLowerTargets)
            {
                if (t.controller == null || t.edifice == null)
                {
                    continue;
                }

                int ticks = CalculateLiftTicks(t.edifice);
                if (!t.controller.TryLowerNoMessage(map, t.position, t.edifice, cacheLinkMask: false))
                {
                    failedCount++;
                    continue;
                }

                t.controller.TryStartLoweringProcess(t.position, ticks);
                loweredCount++;
            }

            // 结果统计与消息反馈
            if (loweredCount <= 0)
            {
                if (showMessage)
                {
                    MessageReject("ULS_GroupNoLowerable", this);
                }
            }
            else if (failedCount > 1 && showMessage)
            {
                MessageNeutral("ULS_GroupLoweredPartial", this, loweredCount, failedCount);
            }
        }
    }


    // ============================================================
    // 【尝试降下单一建筑】
    // ============================================================
    // 核心逻辑：尝试降下单一建筑并放入容器
    //
    // 【参数说明】
    // - map: 地图
    // - cell: 位置
    // - edifice: 目标建筑
    // - cacheLinkMask: 是否缓存链接掩码
    //
    // 【返回值】
    // - true: 成功放入容器
    // ============================================================
    private bool TryLowerNoMessage(Map map, IntVec3 cell, Building edifice, bool cacheLinkMask = true)
    {
        storedRotation = edifice.Rotation;
        storedCell = edifice.Position;
        storedThingMarketValueIgnoreHp = (edifice.Faction == Faction.OfPlayer)
            ? edifice.GetStatValue(StatDefOf.MarketValueIgnoreHp)
            : 0f;


        if (cacheLinkMask)
        {
            CacheStoredLinkMaskForBuilding(edifice, map);
        }

        // 移除地图上的物体
        edifice.DeSpawn();

        // 尝试放入内部容器
        if (!innerContainer.TryAdd(edifice))
        {
            // 失败时回滚：重新生成到地图上
            GenSpawn.Spawn(edifice, cell, map, storedRotation);
            storedCell = IntVec3.Invalid;
            storedThingMarketValueIgnoreHp = 0f;


            ClearStoredLinkMaskCache();
            return false;
        }

        // 成功降下后刷新缓存
        InvalidateGizmoCache();
        return true;
    }


    // ============================================================
    // 【尝试降下多格建筑】
    // ============================================================
    // 内部实现：处理多格建筑的校验和降下逻辑
    //
    // 【参数说明】
    // - building: 多格建筑
    // - showMessage: 是否显示提示
    //
    // 【返回值】
    // - true: 成功
    // ============================================================
    private bool TryLowerMultiCellBuildingInternal(Building building, bool showMessage)
    {
        Map map = building.Map;
        if (map == null)
        {
            return false;
        }

        // 基础合法性检查
        if (building.Destroyed || building is Frame || !building.Spawned)
        {
            return false;
        }

        // 建筑属性检查
        if (building.def == null ||
            building.def.building == null ||
            !building.def.building.isEdifice ||
            !building.def.destroyable)
        {
            if (showMessage)
            {
                MessageReject("ULS_NoEdifice", building);
            }

            return false;
        }

        // 黑名单与派系检查
        if (ULS_Utility.IsEdificeBlacklisted(building))
        {
            if (showMessage)
            {
                MessageReject("ULS_GroupBlacklistedAt", building, building.Position);
            }

            return false;
        }

        if (building.Faction != Faction.OfPlayer)
        {
            if (showMessage)
            {
                MessageReject("ULS_LowerNotPlayerOwned", building);
            }

            return false;
        }


        IntVec3 position = building.Position;
        Rot4 rotation = building.Rotation;
        IntVec2 size = building.def.size;
        CellRect occupiedRect = GenAdj.OccupiedRect(position, rotation, size);

        // 确保主位置有控制器
        if (!ULS_Utility.TryGetControllerAt(map, position, out var rootController))
        {
            if (showMessage)
            {
                MessageReject("ULS_MultiCellNeedControllerEveryCell", building);
            }

            return false;
        }

        // 检查是否已经存在多格组冲突
        ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();
        if (multiCellComp != null && multiCellComp.HasGroup(position))
        {
            if (showMessage)
            {
                MessageReject("ULS_MultiCellGroupAlreadyExists", building);
            }

            return false;
        }


        List<Building_WallController> controllers = new List<Building_WallController>();
        List<IntVec3> controllerCells = new List<IntVec3>();

        // 遍历所有占用格，确保每一格都有空闲的控制器
        foreach (IntVec3 cell in occupiedRect)
        {
            if (!cell.InBounds(map) || !ULS_Utility.TryGetControllerAt(map, cell, out var controller))
            {
                if (showMessage)
                {
                    MessageReject("ULS_MultiCellNeedControllerEveryCell", building);
                }

                return false;
            }

            if (controller.HasStored)
            {
                if (showMessage)
                {
                    MessageReject("ULS_MultiCellControllerHasStored", building);
                }

                return false;
            }

            if (controller.MultiCellGroupRootCell.IsValid)
            {
                if (showMessage)
                {
                    MessageReject("ULS_MultiCellControllerInGroup", building);
                }

                return false;
            }

            controllers.Add(controller);
            controllerCells.Add(cell);
        }

        // 设置临时分组ID (若未分组则新建)
        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp != null)
        {
            int groupId = rootController.ControllerGroupId;
            if (groupId < 1)
            {
                groupId = (rootController.ControllerGroupId = groupComp.CreateNewGroupId());
                groupComp.RegisterOrUpdateController(rootController);
            }

            foreach (var t in controllers)
            {
                t.ControllerGroupId = groupId;
                groupComp.RegisterOrUpdateController(t);
            }
        }

        // 执行降下操作
        int ticks = CalculateLiftTicks(building);
        if (!rootController.TryLowerNoMessage(map, position, building))
        {
            if (showMessage)
            {
                MessageReject("ULS_GroupLowerFailed", building);
            }

            return false;
        }


        foreach (var controller in controllers)
        {
            controller?.TryStartLoweringProcess(controller.Position, ticks);
        }

        // 设置多格组根位置记录
        foreach (var t in controllers)
        {
            t.MultiCellGroupRootCell = position;
        }

        // 注册多格组记录
        multiCellComp?.TryAddGroup(new ULS_MultiCellGroupRecord(position, position, controllerCells));

        return true;
    }
}