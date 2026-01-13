namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    public bool AutoRaiseGroup()
    {
        return TryRaiseGroupAllOrNothing();
    }


    private bool TryRaiseGroupAllOrNothing()
    {
        Map map = Map;
        if (map == null)
        {
            return false;
        }


        if (PowerFeatureEnabled && !IsReadyForLiftPower())
        {
            return false;
        }

        int groupMaxSize = GetGroupMaxSize();
        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        int groupId = controllerGroupId;


        if (groupComp == null ||
            groupId < 1 ||
            !groupComp.TryGetGroupControllerCells(groupId, out var cells) ||
            cells == null ||
            cells.Count == 0)
        {
            return false;
        }


        if (cells.Count > groupMaxSize)
        {
            return false;
        }


        using var _1 = new PooledList<IntVec3>(out var uniqueRootCells);
        using var _2 = new PooledHashSet<IntVec3>(out var seenRoots);
        using var _3 = new PooledList<Building_WallController>(out var raisableControllers);
        using var _4 = new PooledHashSet<Building_WallController>(out var processedControllers);

        {
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
                        return false;
                    }

                    IntVec3 spawnCell = controller.storedCell.IsValid ? controller.storedCell : controller.Position;
                    if (controller.IsBlockedForRaise(map, spawnCell, storedThing))
                    {
                        return false;
                    }

                    raisableControllers.Add(controller);
                }
            }


            if (storedCount <= 0 || raisableControllers.Count != storedCount)
            {
                return false;
            }


            foreach (var controller in raisableControllers)
            {
                if (controller == null)
                {
                    continue;
                }

                using var _5 = new PooledHashSet<Building_WallController>(out var memberControllers);
                controller.GetMultiCellMemberControllersOrSelf(map, memberControllers);

                if (!controller.TryStartRaisingProcess(map))
                {
                    foreach (Building_WallController processed in processedControllers)
                    {
                        processed?.ClearLiftProcessAndRemoveBlocker();
                    }

                    return false;
                }


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


    private void TryRaiseGroup(bool showMessage)
    {
        Map map = Map;
        if (map == null)
        {
            return;
        }

        int groupMaxSize = GetGroupMaxSize();
        int groupId = controllerGroupId;

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

            if (!CheckGroupPowerReady(map, uniqueRootCells, showMessage))
            {
                return;
            }


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
                if (controller.IsBlockedForRaise(map, spawnCell, storedThing))
                {
                    failedCount++;
                }
                else
                {
                    raisableControllers.Add(controller);
                }
            }


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


            foreach (var t in raisableControllers)
            {
                if (!t.TryStartRaisingProcess(map))
                {
                    failedCount++;
                }
            }


            if (failedCount > 1 && showMessage)
            {
                MessageNeutral("ULS_GroupRaisedPartial", this, raisableControllers.Count, failedCount);
            }
        }
    }


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


        innerContainer.Remove(storedThing);
        storedThingMarketValueIgnoreHp = 0f;

        if (storedThing.Destroyed)
        {
            storedCell = IntVec3.Invalid;
            return;
        }


        if (!storedCell.IsValid)
        {
            storedCell = Position;
        }

        IntVec3 rootCell = multiCellGroupRootCell;


        GenSpawn.Spawn(storedThing, spawnLoc, map, storedRotation);
        storedCell = IntVec3.Invalid;


        ClearStoredLinkMaskCache();


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


    public void AutoLowerGroup(IntVec3 startCell)
    {
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        if (settings == null || !settings.enableLiftPower || IsReadyForLiftPower())
        {
            TryLowerGroup(startCell, showMessage: false);
        }
    }


    private void TryLowerGroup(IntVec3 startCell, bool showMessage)
    {
        Map map = Map;
        if (map == null)
        {
            return;
        }

        int groupMaxSize = GetGroupMaxSize();


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

            if (!CheckGroupPowerReady(map, uniqueRootCells, showMessage))
            {
                return;
            }


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


            foreach (var t in oneCellLowerTargets)
            {
                if (t is { controller: not null, edifice: not null })
                {
                    t.controller.CacheStoredLinkMaskForBuilding(t.edifice, map);
                }
            }


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


        edifice.DeSpawn();


        if (!innerContainer.TryAdd(edifice))
        {
            GenSpawn.Spawn(edifice, cell, map, storedRotation);
            storedCell = IntVec3.Invalid;
            storedThingMarketValueIgnoreHp = 0f;


            ClearStoredLinkMaskCache();
            return false;
        }

        return true;
    }


    private bool TryLowerMultiCellBuildingInternal(Building building, bool showMessage)
    {
        Map map = building.Map;
        if (map == null)
        {
            return false;
        }


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


        IntVec3 position = building.Position;
        Rot4 rotation = building.Rotation;
        IntVec2 size = building.def.size;
        CellRect occupiedRect = GenAdj.OccupiedRect(position, rotation, size);


        if (!ULS_Utility.TryGetControllerAt(map, position, out var rootController))
        {
            if (showMessage)
            {
                MessageReject("ULS_MultiCellNeedControllerEveryCell", building);
            }

            return false;
        }


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


        foreach (var t in controllers)
        {
            t.MultiCellGroupRootCell = position;
        }


        multiCellComp?.TryAddGroup(new ULS_MultiCellGroupRecord(position, position, controllerCells));

        return true;
    }
}