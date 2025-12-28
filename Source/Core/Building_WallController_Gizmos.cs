namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }


        List<Building_WallController> selectedControllers = GetSelectedControllers();
        if (selectedControllers.Count == 0)
        {
            selectedControllers.Add(this);
        }


        ULS_AutoGroupMapComponent autoGroupComp;
        HashSet<int> groupIds;


        if (selectedControllers.Count <= 0 || selectedControllers[0] == this)
        {
            bool anyNotPlayerOwned = false;
            foreach (var t in selectedControllers)
            {
                if (t?.Faction != Faction.OfPlayer)
                {
                    anyNotPlayerOwned = true;
                    break;
                }
            }


            if (!anyNotPlayerOwned)
            {
                yield return new Command_Action
                {
                    defaultLabel = "ULS_GroupSetIdWithValue".Translate(controllerGroupId),
                    icon = TexCommand.ForbidOff,
                    action = delegate
                    {
                        Map mapLocal = Map;
                        if (mapLocal == null) return;
                        ULS_ControllerGroupMapComponent groupComp =
                            mapLocal.GetComponent<ULS_ControllerGroupMapComponent>();
                        if (groupComp != null)
                        {
                            Find.WindowStack.Add(new ULS_Dialog_SetControllerGroupId(controllerGroupId,
                                delegate(int newGroupId)
                                {
                                    List<Building_WallController> expandedList =
                                        ExpandSelectedControllersToMultiCellHiddenGroupMembers(mapLocal,
                                            selectedControllers);
                                    if (!ULS_AutoGroupUtility.CanAssignControllersToGroup(mapLocal, expandedList,
                                            newGroupId, out string rejectKey))
                                    {
                                        MessageReject(rejectKey, this);
                                    }
                                    else
                                    {
                                        List<IntVec3> cellsToAssign = new List<IntVec3>();
                                        foreach (var controller in expandedList)
                                        {
                                            if (controller != null && controller.Map == mapLocal &&
                                                controller.Spawned)
                                            {
                                                cellsToAssign.Add(controller.Position);
                                            }
                                        }

                                        groupComp.AssignControllerCellsToGroup(cellsToAssign, newGroupId);
                                        mapLocal.GetComponent<ULS_AutoGroupMapComponent>()?.NotifyAutoGroupsDirty();
                                    }
                                }));
                        }
                    }
                };
            }


            if (selectedControllers.Count >= 2 && !anyNotPlayerOwned)
            {
                yield return new Command_Action
                {
                    defaultLabel = "ULS_GroupMergeToPrimary".Translate(),
                    icon = TexCommand.ForbidOff,
                    action = delegate
                    {
                        Map map = Map;
                        if (map == null) return;
                        ULS_ControllerGroupMapComponent groupComp =
                            map.GetComponent<ULS_ControllerGroupMapComponent>();
                        if (groupComp == null) return;
                        int primaryGroupId = controllerGroupId;
                        if (primaryGroupId < 1)
                        {
                            primaryGroupId = groupComp.CreateNewGroupId();
                            controllerGroupId = primaryGroupId;
                            groupComp.RegisterOrUpdateController(this);
                        }

                        bool primaryIsAuto = ULS_AutoGroupUtility.IsAutoGroup(map, primaryGroupId);
                        List<Building_WallController> expandedList =
                            ExpandSelectedControllersToMultiCellHiddenGroupMembers(map, selectedControllers);
                        HashSet<int> mergedGroupIds = new HashSet<int>();


                        foreach (var controller in expandedList)
                        {
                            if (controller != null && controller.Map == map && controller.Spawned)
                            {
                                int groupId = controller.ControllerGroupId;
                                if (groupId >= 1 && groupId != primaryGroupId && mergedGroupIds.Add(groupId))
                                {
                                    bool isAuto = ULS_AutoGroupUtility.IsAutoGroup(map, groupId);
                                    if (primaryIsAuto != isAuto)
                                    {
                                        MessageReject("ULS_AutoGroup_MixAutoAndManual", this);
                                        return;
                                    }

                                    groupComp.MergeGroups(primaryGroupId, groupId);
                                }
                            }
                        }

                        map.GetComponent<ULS_AutoGroupMapComponent>()?.NotifyAutoGroupsDirty();
                    }
                };
            }


            if (!anyNotPlayerOwned && !AnySelectedControllerInMultiCellHiddenGroup(selectedControllers))
            {
                yield return new Command_Action
                {
                    defaultLabel = "ULS_GroupSplitToNew".Translate(),
                    icon = TexCommand.ForbidOff,
                    action = delegate
                    {
                        Map map = Map;
                        if (map != null)
                        {
                            ULS_ControllerGroupMapComponent groupComp =
                                map.GetComponent<ULS_ControllerGroupMapComponent>();
                            if (groupComp != null)
                            {
                                int newGroupId = groupComp.CreateNewGroupId();
                                List<IntVec3> cellsToAssign = new List<IntVec3>();
                                foreach (var controller in selectedControllers)
                                {
                                    if (controller != null && controller.Map == map && controller.Spawned)
                                    {
                                        cellsToAssign.Add(controller.Position);
                                    }
                                }

                                groupComp.AssignControllerCellsToGroup(cellsToAssign, newGroupId);
                                map.GetComponent<ULS_AutoGroupMapComponent>()?.NotifyAutoGroupsDirty();
                            }
                        }
                    }
                };
            }


            if (!anyNotPlayerOwned && Map != null && GetComp<ULS_AutoGroupMarker>() != null)
            {
                autoGroupComp = Map.GetComponent<ULS_AutoGroupMapComponent>();
                if (autoGroupComp != null)
                {
                    List<Building_WallController> expandedList =
                        ExpandSelectedControllersToMultiCellHiddenGroupMembers(Map, selectedControllers);
                    groupIds = new HashSet<int>();
                    bool allAreAutoGroup = true;


                    foreach (var controller in expandedList)
                    {
                        if (controller != null && controller.Map == Map && controller.Spawned)
                        {
                            if (controller.GetComp<ULS_AutoGroupMarker>() == null)
                            {
                                allAreAutoGroup = false;
                                break;
                            }

                            if (controller.ControllerGroupId > 0)
                            {
                                groupIds.Add(controller.ControllerGroupId);
                            }
                        }
                    }


                    if (allAreAutoGroup && groupIds.Count > 0)
                    {
                        CompProperties_ULS_AutoGroupMarker props = GetComp<ULS_AutoGroupMarker>().Props;
                        bool hasType = false;
                        bool mixedTypes = false;
                        ULS_AutoGroupType currentType = ULS_AutoGroupType.Friendly;


                        foreach (int gid in groupIds)
                        {
                            ULS_AutoGroupType groupType =
                                autoGroupComp.GetOrInitGroupFilterType(gid, props.autoGroupType);
                            if (!hasType)
                            {
                                hasType = true;
                                currentType = groupType;
                            }
                            else if (currentType != groupType)
                            {
                                mixedTypes = true;
                                break;
                            }
                        }


                        string labelText = (!mixedTypes)
                            ? (currentType switch
                            {
                                ULS_AutoGroupType.Hostile => "ULS_AutoGroup_Filter_Hostile",
                                ULS_AutoGroupType.Friendly => "ULS_AutoGroup_Filter_Friendly",
                                _ => "ULS_AutoGroup_Filter_Neutral",
                            }).Translate()
                            : "ULS_AutoGroup_Filter_Mixed".Translate();

                        List<FloatMenuOption> options;
                        yield return new Command_Action
                        {
                            defaultLabel = "ULS_AutoGroup_FilterWithValue".Translate(labelText),
                            icon = TexCommand.ForbidOff,
                            action = delegate
                            {
                                options = new List<FloatMenuOption>();
                                AddOption(ULS_AutoGroupType.Friendly, "ULS_AutoGroup_SetFilter_Friendly");
                                AddOption(ULS_AutoGroupType.Hostile, "ULS_AutoGroup_SetFilter_Hostile");
                                AddOption(ULS_AutoGroupType.Neutral, "ULS_AutoGroup_SetFilter_Neutral");
                                Find.WindowStack.Add(new FloatMenu(options));


                                void AddOption(ULS_AutoGroupType type, string labelKey)
                                {
                                    options.Add(new FloatMenuOption(labelKey.Translate(), delegate
                                    {
                                        foreach (int gid in groupIds)
                                        {
                                            autoGroupComp.SetGroupFilterType(gid, type);
                                        }
                                    }));
                                }
                            }
                        };
                    }
                }
            }
        }


        if (GetComp<ULS_AutoGroupMarker>() != null)
        {
            yield break;
        }


        Command_Action raiseCommand = new Command_Action
        {
            defaultLabel = "ULS_RaiseGroup".Translate(),
            icon = TexCommand.ForbidOn,
            action = GizmoRaiseGroupCommandAction
        };

        bool disabled = false;
        Map currentMap = Map;
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        int groupMaxSize = GetGroupMaxSize();


        if (currentMap == null)
        {
            raiseCommand.Disable("ULS_NoStored".Translate());
            disabled = true;
        }
        else
        {
            ULS_ControllerGroupMapComponent groupComp = currentMap.GetComponent<ULS_ControllerGroupMapComponent>();
            int groupId = controllerGroupId;

            if (groupComp == null || groupId < 1 ||
                !groupComp.TryGetGroupControllerCells(groupId, out List<IntVec3> groupCells) || groupCells == null ||
                groupCells.Count == 0)
            {
                raiseCommand.Disable("ULS_NoStored".Translate());
                disabled = true;
            }
            else if (groupCells.Count > groupMaxSize)
            {
                raiseCommand.Disable("ULS_GroupTooLarge".Translate(groupMaxSize));
                disabled = true;
            }
            else
            {
                bool hasStored = false;
                foreach (var t in groupCells)
                {
                    if (ULS_Utility.TryGetControllerAt(currentMap, t, out Building_WallController controller) &&
                        controller.HasStored)
                    {
                        hasStored = true;
                        break;
                    }
                }

                if (!hasStored)
                {
                    raiseCommand.Disable("ULS_NoStored".Translate());
                    disabled = true;
                }
                else
                {
                    foreach (var t in groupCells)
                    {
                        if (ULS_Utility.TryGetControllerAt(currentMap, t, out Building_WallController controller) &&
                            controller.InLiftProcess)
                        {
                            raiseCommand.Disable("ULS_LiftInProcess".Translate());
                            disabled = true;
                            break;
                        }
                    }


                    if (!disabled && settings is { enableLiftPower: true })
                    {
                        foreach (var t in groupCells)
                        {
                            if (ULS_Utility.TryGetControllerAt(currentMap, t, out Building_WallController controller) &&
                                controller != null && !controller.IsReadyForLiftPower())
                            {
                                raiseCommand.Disable("ULS_PowerOff".Translate());
                                disabled = true;
                                break;
                            }
                        }
                    }
                }
            }
        }


        if (!disabled && (settings?.liftControlMode ?? LiftControlMode.Remote) == LiftControlMode.Console)
        {
            if (!ULS_Utility.TryGetNearestLiftConsoleByDistance(currentMap, Position, out ThingWithComps _))
            {
                ThingDef consoleDef = DefDatabase<ThingDef>.GetNamedSilentFail("ULS_LiftConsole");
                bool anyConsoleExists = consoleDef != null && currentMap.listerThings.ThingsOfDef(consoleDef)
                    .Any(t => t.Faction == Faction.OfPlayer);

                if (anyConsoleExists)
                {
                    raiseCommand.Disable("ULS_LiftConsolePowerOff".Translate());
                }
                else
                {
                    raiseCommand.Disable("ULS_LiftConsoleMissing".Translate());
                }

                disabled = true;
            }
        }

        yield return raiseCommand;
    }


    public void GizmoRaiseGroup()
    {
        TryRaiseGroup(showMessage: true);
    }


    private void GizmoRaiseGroupCommandAction()
    {
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        LiftControlMode controlMode = settings?.liftControlMode ?? LiftControlMode.Remote;


        if (settings is { enableLiftPower: true } && !IsReadyForLiftPower())
        {
            return;
        }


        if (controlMode == LiftControlMode.Remote)
        {
            TryRaiseGroup(showMessage: true);
        }
        else
        {
            if (Map == null)
            {
                return;
            }

            switch (controlMode)
            {
                case LiftControlMode.Manual:
                {
                    // 在控制器上直接设置挂起状态并添加 Flick 指定
                    QueueLiftAction(isRaise: true, IntVec3.Invalid);
                    break;
                }

                case LiftControlMode.Console:
                {
                    if (!ULS_Utility.TryGetNearestLiftConsoleByDistance(Map, Position, out ThingWithComps consoleThing))
                    {
                        // 检查是否因为没电
                        ThingDef consoleDef = DefDatabase<ThingDef>.GetNamedSilentFail("ULS_LiftConsole");
                        bool anyConsoleExists = consoleDef != null && Map.listerThings.ThingsOfDef(consoleDef)
                            .Any(t => t.Faction == Faction.OfPlayer);
                        if (anyConsoleExists)
                        {
                            MessageReject("ULS_LiftConsolePowerOff", this);
                        }
                        else
                        {
                            MessageReject("ULS_LiftConsoleMissing", this);
                        }

                        break;
                    }

                    CompLiftConsole consoleComp = consoleThing.GetComp<CompLiftConsole>();
                    if (consoleComp == null)
                    {
                        // 兼容性保护：如果是旧存档，可能还没更到
                        MessageReject("ULS_LiftConsoleMissing", this);
                    }
                    else
                    {
                        consoleComp.EnqueueRequest(new ULS_LiftRequest(ULS_LiftRequestType.RaiseGroup, this,
                            IntVec3.Invalid));
                    }

                    break;
                }
            }
        }
    }


    public void GizmoLowerGroup(IntVec3 startCell)
    {
        TryLowerGroup(startCell, showMessage: true);
    }


    public void GizmoLowerFromBuilding(Building building, IntVec3 controllerCell)
    {
        if (building.OccupiedRect().Area <= 1)
        {
            TryLowerGroup(controllerCell, showMessage: true);
        }
        else
        {
            TryLowerMultiCellBuilding(building);
        }
    }
}