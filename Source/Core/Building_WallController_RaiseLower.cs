namespace Universal_Lift_Structure;

/// 文件意图：Building_WallController - 升降业务逻辑。
/// 包含：组升起、组降下、多格建筑处理、回滚逻辑、无消息升降。
public partial class Building_WallController
{
    // ==================== 组升起业务 ====================

    /// 自动升起组（不显示消息，用于自动模式）
    public bool AutoRaiseGroup()
    {
        return TryRaiseGroupAllOrNothing();
    }

    /// 尝试升起组（全部或不升）
    private bool TryRaiseGroupAllOrNothing()
    {
        Map map = Map;
        if (map == null)
        {
            return false;
        }

        // 电力检查
        if (PowerFeatureEnabled && !IsReadyForLiftPower())
        {
            return false;
        }

        int groupMaxSize = GetGroupMaxSize();
        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        int groupId = controllerGroupId;

        // 获取组成员
        if (groupComp == null ||
            groupId < 1 ||
            !groupComp.TryGetGroupControllerCells(groupId, out var cells) ||
            cells == null ||
            cells.Count == 0)
        {
            return false;
        }

        // 组规模检查
        if (cells.Count > groupMaxSize)
        {
            return false;
        }

        // 去重：多格建筑只取根格
        List<IntVec3> uniqueRootCells = new List<IntVec3>();
        HashSet<IntVec3> seenRoots = new HashSet<IntVec3>();

        for (int i = 0; i < cells.Count; i++)
        {
            IntVec3 cell = cells[i];
            if (ULS_Utility.TryGetControllerAt(map, cell, out var controller) && controller != null)
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

        // 收集可升起的控制器
        int storedCount = 0;
        List<Building_WallController> raisableControllers = new List<Building_WallController>();

        for (int j = 0; j < uniqueRootCells.Count; j++)
        {
            if (ULS_Utility.TryGetControllerAt(map, uniqueRootCells[j], out var controller) &&
                controller != null &&
                controller.HasStored)
            {
                storedCount++;

                Thing storedThing = controller.StoredThing;
                if (storedThing == null)
                {
                    controller.storedCell = IntVec3.Invalid;
                    return false;
                }

                IntVec3 spawnCell = controller.storedCell.IsValid ? controller.storedCell : controller.Position;
                if (controller.IsBlockedForRaise(map, spawnCell, storedThing))
                {
                    return false; // 有任何阻挡就失败
                }

                raisableControllers.Add(controller);
            }
        }

        // 没有可升起的
        if (storedCount <= 0 || raisableControllers.Count != storedCount)
        {
            return false;
        }

        // 执行升起（带回滚）
        HashSet<Building_WallController> processedControllers = new HashSet<Building_WallController>();

        for (int k = 0; k < raisableControllers.Count; k++)
        {
            Building_WallController controller = raisableControllers[k];
            if (controller == null)
            {
                continue;
            }

            HashSet<Building_WallController> memberControllers = controller.GetMultiCellMemberControllersOrSelf(map);

            if (!controller.TryStartRaisingProcess(map))
            {
                // 启动失败：回滚已启动的
                foreach (Building_WallController processed in processedControllers)
                {
                    processed?.ClearLiftProcessAndRemoveBlocker();
                }
                return false;
            }

            // 记录已启动的成员
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

    /// 尝试升起组（部分成功模式）
    private void TryRaiseGroup(bool showMessage)
    {
        Map map = Map;
        if (map == null)
        {
            return;
        }

        int groupMaxSize = GetGroupMaxSize();
        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        int groupId = controllerGroupId;

        // 获取组成员
        if (groupComp == null ||
            groupId < 1 ||
            !groupComp.TryGetGroupControllerCells(groupId, out var cells) ||
            cells == null ||
            cells.Count == 0)
        {
            if (showMessage)
            {
                MessageReject("ULS_NoStored", this);
            }
            return;
        }

        // 组规模检查
        if (cells.Count > groupMaxSize)
        {
            if (showMessage)
            {
                MessageReject("ULS_GroupTooLarge", this, groupMaxSize);
            }
            return;
        }

        // 去重：多格建筑只取根格
        List<IntVec3> uniqueRootCells = new List<IntVec3>();
        HashSet<IntVec3> seenRoots = new HashSet<IntVec3>();

        for (int i = 0; i < cells.Count; i++)
        {
            IntVec3 cell = cells[i];
            if (ULS_Utility.TryGetControllerAt(map, cell, out var controller) && controller != null)
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

        // 电力检查
        int storedCount = 0;
        int failedCount = 0;
        List<Building_WallController> raisableControllers = new List<Building_WallController>();

        if (PowerFeatureEnabled)
        {
            for (int j = 0; j < uniqueRootCells.Count; j++)
            {
                if (ULS_Utility.TryGetControllerAt(map, uniqueRootCells[j], out var controller) &&
                    controller != null &&
                    !controller.IsReadyForLiftPower())
                {
                    if (showMessage)
                    {
                        MessageReject("ULS_GroupPowerInsufficient", controller);
                    }
                    return;
                }
            }
        }

        // 收集可升起的控制器
        for (int k = 0; k < uniqueRootCells.Count; k++)
        {
            if (!ULS_Utility.TryGetControllerAt(map, uniqueRootCells[k], out var controller) ||
                controller == null ||
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
            if (controller.IsBlockedForRaise(map, spawnCell, storedThing))
            {
                failedCount++;
            }
            else
            {
                raisableControllers.Add(controller);
            }
        }

        // 没有可升起的
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

        // 执行升起
        for (int l = 0; l < raisableControllers.Count; l++)
        {
            if (!raisableControllers[l].TryStartRaisingProcess(map))
            {
                failedCount++;
            }
        }

        // 显示消息
        if (failedCount <= 0)
        {
            if (showMessage)
            {
                MessageNeutral("ULS_GroupRaised", this, raisableControllers.Count);
            }
        }
        else if (showMessage)
        {
            MessageNeutral("ULS_GroupRaisedPartial", this, raisableControllers.Count, failedCount);
        }
    }

    /// 无消息升起（内部使用）
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

        // 从容器移除
        innerContainer.Remove(storedThing);
        storedThingMarketValueIgnoreHp = 0f;

        if (storedThing.Destroyed)
        {
            storedCell = IntVec3.Invalid;
            return;
        }

        // 确保有效位置
        if (!storedCell.IsValid)
        {
            storedCell = Position;
        }

        IntVec3 rootCell = multiCellGroupRootCell;

        // 生成建筑
        GenSpawn.Spawn(storedThing, spawnLoc, map, storedRotation, WipeMode.VanishOrMoveAside);
        storedCell = IntVec3.Invalid;

        // 升起放出：清理 link 贴图缓存（仅用于升降虚影）
        ClearStoredLinkMaskCache();

        // 清理多格隐组
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
    }

    // ==================== 组降下业务 ====================

    /// 自动降下组（不显示消息，用于自动模式）
    public void AutoLowerGroup(IntVec3 startCell)
    {
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        if (settings == null || !settings.enableLiftPower || IsReadyForLiftPower())
        {
            TryLowerGroup(startCell, showMessage: false);
        }
    }

    /// 尝试降下组
    private void TryLowerGroup(IntVec3 startCell, bool showMessage)
    {
        Map map = Map;
        if (map == null)
        {
            return;
        }

        int groupMaxSize = GetGroupMaxSize();

        // 获取起始控制器
        if (!ULS_Utility.TryGetControllerAt(map, startCell, out var startController))
        {
            if (showMessage)
            {
                MessageReject("ULS_GroupNoController", this);
            }
            return;
        }

        // 获取组成员
        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        int groupId = startController.ControllerGroupId;

        if (groupComp == null ||
            groupId < 1 ||
            !groupComp.TryGetGroupControllerCells(groupId, out var cells) ||
            cells == null ||
            cells.Count == 0)
        {
            if (showMessage)
            {
                MessageReject("ULS_GroupNoController", this);
            }
            return;
        }

        // 组规模检查
        if (cells.Count > groupMaxSize)
        {
            if (showMessage)
            {
                MessageReject("ULS_GroupTooLarge", this, groupMaxSize);
            }
            return;
        }

        // 去重：多格建筑只取根格
        List<IntVec3> uniqueRootCells = new List<IntVec3>();
        HashSet<IntVec3> seenRoots = new HashSet<IntVec3>();

        for (int i = 0; i < cells.Count; i++)
        {
            IntVec3 cell = cells[i];
            if (ULS_Utility.TryGetControllerAt(map, cell, out var controller) && controller != null)
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

        // 电力检查
        if (PowerFeatureEnabled)
        {
            for (int j = 0; j < uniqueRootCells.Count; j++)
            {
                if (ULS_Utility.TryGetControllerAt(map, uniqueRootCells[j], out var controller) &&
                    controller != null &&
                    !controller.IsReadyForLiftPower())
                {
                    if (showMessage)
                    {
                        MessageReject("ULS_GroupPowerInsufficient", controller);
                    }
                    return;
                }
            }
        }

        // 尝试降下
        int loweredCount = 0;
        int skippedCount = 0;
        HashSet<Building> processedMultiCellBuildings = new HashSet<Building>();

        // 两阶段：先为本次“将要收纳”的 1x1 link 建筑预缓存连接形态，再逐个 DeSpawn/收纳。
        // 原因：逐个 DeSpawn 会触发 linkGrid 刷新，导致后处理的墙在缓存阶段看到的邻居已断开，从而 cachedNonZero 变为 0。
        List<(Building_WallController controller, IntVec3 position, Building edifice)> oneCellLowerTargets =
            new List<(Building_WallController controller, IntVec3 position, Building edifice)>();

        for (int k = 0; k < uniqueRootCells.Count; k++)
        {
            if (!ULS_Utility.TryGetControllerAt(map, uniqueRootCells[k], out var controller) || controller == null)
            {
                skippedCount++;
                continue;
            }

            if (controller.InLiftProcess)
            {
                skippedCount++;
                continue;
            }

            if (controller.HasStored)
            {
                skippedCount++;
                continue;
            }

            IntVec3 position = controller.Position;
            Building edifice = map.edificeGrid[position];

            // 检查建筑有效性
            if (edifice == null ||
                edifice.Destroyed ||
                !edifice.Spawned ||
                edifice is Frame ||
                !edifice.def.destroyable)
            {
                skippedCount++;
            }
            else if (ULS_Utility.IsEdificeBlacklisted(edifice))
            {
                skippedCount++;
            }
            else if (edifice.Faction != Faction.OfPlayer)
            {
                skippedCount++;
            }
            else if (edifice.def.Size == IntVec2.One)
            {
                // 1x1 建筑：先收集，后统一预缓存，再执行收纳
                oneCellLowerTargets.Add((controller, position, edifice));
            }
            else if (!processedMultiCellBuildings.Contains(edifice))
            {
                // 多格建筑：整体降下
                if (!TryLowerMultiCellBuildingInternal(edifice, showMessage: false))
                {
                    skippedCount++;
                    processedMultiCellBuildings.Add(edifice);
                }
                else
                {
                    loweredCount++;
                    processedMultiCellBuildings.Add(edifice);
                }
            }
        }

        // 预缓存：在任何 DeSpawn 发生前，按“组操作开始时”的 link 状态统一缓存
        for (int i = 0; i < oneCellLowerTargets.Count; i++)
        {
            var t = oneCellLowerTargets[i];
            if (t.controller != null && t.edifice != null)
            {
                t.controller.CacheStoredLinkMaskForBuilding(t.edifice, map);
            }
        }

        // 执行 1x1 收纳与升降过程
        for (int i = 0; i < oneCellLowerTargets.Count; i++)
        {
            var t = oneCellLowerTargets[i];
            if (t.controller == null || t.edifice == null)
            {
                skippedCount++;
                continue;
            }

            int ticks = CalculateLiftTicks(t.edifice);
            if (!t.controller.TryLowerNoMessage(map, t.position, t.edifice, cacheLinkMask: false))
            {
                skippedCount++;
                continue;
            }

            t.controller.TryStartLoweringProcess(t.position, ticks);
            loweredCount++;
        }

        // 显示消息
        if (loweredCount <= 0)
        {
            if (showMessage)
            {
                MessageReject("ULS_GroupNoLowerable", this);
            }
        }
        else if (skippedCount <= 0)
        {
            if (showMessage)
            {
                MessageNeutral("ULS_GroupLowered", this, loweredCount);
            }
        }
        else if (showMessage)
        {
            MessageNeutral("ULS_GroupLoweredPartial", this, loweredCount, skippedCount);
        }
    }

    /// 无消息降下（内部使用）
    private bool TryLowerNoMessage(Map map, IntVec3 cell, Building edifice, bool cacheLinkMask = true)
    {
        storedRotation = edifice.Rotation;
        storedCell = edifice.Position;
        storedThingMarketValueIgnoreHp = (edifice.Faction == Faction.OfPlayer)
            ? edifice.GetStatValue(StatDefOf.MarketValueIgnoreHp)
            : 0f;

        // 在 DeSpawn 前缓存 link 连接形态，供升降虚影渲染使用。
        // 注意：这里的缓存仅面向墙/围墙等基础链接（用户确认范围 A）。
        // 组降下会先统一预缓存，随后在此处跳过，以避免 DeSpawn 顺序影响 linkGrid。
        if (cacheLinkMask)
        {
            CacheStoredLinkMaskForBuilding(edifice, map);
        }

        // 从地图移除
        edifice.DeSpawn();

        // 尝试加入容器
        if (!innerContainer.TryAdd(edifice))
        {
            // 失败：恢复建筑
            GenSpawn.Spawn(edifice, cell, map, storedRotation);
            storedCell = IntVec3.Invalid;
            storedThingMarketValueIgnoreHp = 0f;

            // 回滚：清理 link 缓存，避免读档/后续渲染误用
            ClearStoredLinkMaskCache();
            return false;
        }

        return true;
    }

    // ==================== 多格建筑降下 ====================

    /// 尝试降下多格建筑（公开方法）
    private void TryLowerMultiCellBuilding(Building building)
    {
        TryLowerMultiCellBuildingInternal(building, showMessage: true);
    }

    /// 尝试降下多格建筑（内部实现）
    private bool TryLowerMultiCellBuildingInternal(Building building, bool showMessage)
    {
        Map map = building.Map;
        if (map == null)
        {
            return false;
        }

        // 建筑有效性检查
        if (building.Destroyed || building is Frame || !building.Spawned)
        {
            return false;
        }

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

        // 获取建筑占用区域
        IntVec3 position = building.Position;
        Rot4 rotation = building.Rotation;
        IntVec2 size = building.def.size;
        CellRect occupiedRect = GenAdj.OccupiedRect(position, rotation, size);

        // 检查根格控制器
        if (!ULS_Utility.TryGetControllerAt(map, position, out var rootController))
        {
            if (showMessage)
            {
                MessageReject("ULS_MultiCellNeedControllerEveryCell", building);
            }
            return false;
        }

        // 检查是否已存在多格隐组
        ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();
        if (multiCellComp != null && multiCellComp.HasGroup(position))
        {
            if (showMessage)
            {
                MessageReject("ULS_MultiCellGroupAlreadyExists", building);
            }
            return false;
        }

        // 检查所有格子都有控制器
        List<Building_WallController> controllers = new List<Building_WallController>();
        List<IntVec3> controllerCells = new List<IntVec3>();

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

        // 统一组 ID
        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp != null)
        {
            int groupId = rootController.ControllerGroupId;
            if (groupId < 1)
            {
                groupId = (rootController.ControllerGroupId = groupComp.CreateNewGroupId());
                groupComp.RegisterOrUpdateController(rootController);
            }

            for (int i = 0; i < controllers.Count; i++)
            {
                controllers[i].ControllerGroupId = groupId;
                groupComp.RegisterOrUpdateController(controllers[i]);
            }
        }

        // 执行降下
        int ticks = CalculateLiftTicks(building);
        if (!rootController.TryLowerNoMessage(map, position, building))
        {
            if (showMessage)
            {
                MessageReject("ULS_GroupLowerFailed", building);
            }
            return false;
        }

        // 启动所有控制器的降下过程
        for (int j = 0; j < controllers.Count; j++)
        {
            Building_WallController controller = controllers[j];
            controller?.TryStartLoweringProcess(controller.Position, ticks);
        }

        // 标记多格隐组
        for (int k = 0; k < controllers.Count; k++)
        {
            controllers[k].MultiCellGroupRootCell = position;
        }

        // 注册多格隐组
        multiCellComp?.TryAddGroup(new ULS_MultiCellGroupRecord(position, position, controllerCells));

        return true;
    }

}
