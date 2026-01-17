namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    // ============================================================
    // 【获取交互按钮 (Gizmos)】
    // ============================================================
    // 获取该控制器的交互按钮 (Gizmos)
    //
    // 【返回值】
    // - Gizmo 列表
    // ============================================================
    public override IEnumerable<Gizmo> GetGizmos()
    {
        // 确保电力状态正确（边缘情况：Mod设置运行时变更）
        EnsureIdlePowerIfFeatureDisabled();

        // 获取基类 Gizmos
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }


        List<Building_WallController> selectedControllers = GetSelectedControllers();
        if (selectedControllers.Count == 0)
        {
            selectedControllers.Add(this);
        }


        if (selectedControllers.Count <= 0 || selectedControllers[0] == this)
        {
            // 检查是否有归属非玩家的控制器
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
                // [设置分组ID] 按钮
                yield return new Command_Action
                {
                    defaultLabel = "ULS_GroupSetIdWithValue".Translate(controllerGroupId),
                    defaultDesc = "ULS_GroupSetIdDesc".Translate(),
                    icon = ULS_GizmoTextures.SetGroupId,
                    action = delegate { OnGizmoAction_SetGroupId(selectedControllers); }
                };

                // [选择组内所有控制器] 按钮
                yield return new Command_Action
                {
                    defaultLabel = "ULS_SelectGroupControllers".Translate(),
                    defaultDesc = "ULS_SelectGroupControllersDesc".Translate(),
                    icon = ULS_GizmoTextures.SelectLinked,
                    action = delegate { OnGizmoAction_SelectGroupControllers(); }
                };
            }


            // [合并编组] 按钮 (选中2个以上时显示)
            if (selectedControllers.Count >= 2 && !anyNotPlayerOwned)
            {
                yield return new Command_Action
                {
                    defaultLabel = "ULS_GroupMergeToPrimary".Translate(),
                    defaultDesc = "ULS_GroupMergeToPrimaryDesc".Translate(),
                    icon = ULS_GizmoTextures.MergeGroups,
                    action = delegate { OnGizmoAction_MergeGroups(selectedControllers); }
                };
            }


            // [拆分编组] 按钮 (不包含多格建筑时显示)
            if (!anyNotPlayerOwned && !AnySelectedControllerInMultiCellHiddenGroup(selectedControllers))
            {
                yield return new Command_Action
                {
                    defaultLabel = "ULS_GroupSplitToNew".Translate(),
                    defaultDesc = "ULS_GroupSplitToNewDesc".Translate(),
                    icon = ULS_GizmoTextures.SplitGroup,
                    action = delegate { OnGizmoAction_SplitToNewGroup(selectedControllers); }
                };
            }


            // [自动编组过滤] 按钮
            if (!anyNotPlayerOwned && Map != null && GetComp<ULS_AutoGroupMarker>() != null)
            {
                ULS_AutoGroupMapComponent autoGroupComp = Map.GetComponent<ULS_AutoGroupMapComponent>();
                if (autoGroupComp != null)
                {
                    // 扩展选中的控制器，包含多格组
                    List<Building_WallController> expandedList = new List<Building_WallController>();
                    ExpandSelectedControllersToMultiCellHiddenGroupMembers(Map, selectedControllers, expandedList);
                    HashSet<int> groupIds = new HashSet<int>();
                    bool allAreAutoGroup = true;


                    // 检查所有选中的控制器是否都支持自动编组
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


                        // 检查选中组的过滤类型是否一致
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

                        yield return new Command_Action
                        {
                            defaultLabel = "ULS_AutoGroup_FilterWithValue".Translate(labelText),
                            defaultDesc = "ULS_AutoGroup_FilterDesc".Translate(),
                            icon = ULS_GizmoTextures.SetAutoGroupFilter,
                            action = delegate { OnGizmoAction_SetAutoGroupFilter(groupIds); }
                        };

                        // [反转模式切换] 按钮
                        // 检查选中组的反转模式状态是否一致
                        bool hasInvertedState = false;
                        bool mixedInverted = false;
                        bool currentInverted = false;

                        foreach (int gid in groupIds)
                        {
                            bool inverted = autoGroupComp.GetGroupInvertedMode(gid);
                            if (!hasInvertedState)
                            {
                                hasInvertedState = true;
                                currentInverted = inverted;
                            }
                            else if (currentInverted != inverted)
                            {
                                mixedInverted = true;
                                break;
                            }
                        }

                        string invertedLabelText = mixedInverted
                            ? "ULS_AutoGroup_Inverted_Mixed".Translate()
                            : (currentInverted
                                ? "ULS_AutoGroup_Inverted_On".Translate()
                                : "ULS_AutoGroup_Inverted_Off".Translate());

                        yield return new Command_Action
                        {
                            defaultLabel = "ULS_AutoGroup_InvertedWithValue".Translate(invertedLabelText),
                            defaultDesc = "ULS_AutoGroup_InvertedDesc".Translate(),
                            icon = ULS_GizmoTextures.ToggleInvertedMode,
                            action = delegate { OnGizmoAction_ToggleInvertedMode(groupIds); }
                        };
                    }
                }
            }
        }

        if (GetComp<ULS_AutoGroupMarker>() != null)
        {
            yield break;
        }


        // [升起编组] 按钮 (核心功能)
        Command_Action raiseCommand = new Command_Action
        {
            defaultLabel = "ULS_RaiseGroup".Translate(),
            defaultDesc = "ULS_RaiseGroupDesc".Translate(),
            icon = ULS_GizmoTextures.RaiseGroup,
            action = GizmoRaiseGroupCommandAction
        };

        // 使用缓存进行状态检查（优化：避免每帧重复计算）
        if (!IsGizmoCacheValid)
        {
            RefreshGizmoCache();
        }

        if (cachedRaiseDisableReason != GizmoDisableReason.None)
        {
            raiseCommand.Disable(GetDisableReasonString(cachedRaiseDisableReason, cachedGroupMaxSizeArg));
        }

        yield return raiseCommand;

        // 添加取消升降 Gizmo（仅 Manual/Console 模式且存在期望状态时）
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        if (wantedLiftAction != ULS_LiftActionRequest.None &&
            settings is { liftControlMode: not LiftControlMode.Remote })
        {
            Command_Action cancelCommand = new Command_Action
            {
                defaultLabel = "ULS_CancelLift".Translate(),
                defaultDesc = "ULS_CancelLiftDesc".Translate(),
                icon = TexCommand.ClearPrioritizedWork,
                action = delegate
                {
                    // 重置期望状态并更新 Designation
                    wantedLiftAction = ULS_LiftActionRequest.None;
                    UpdateLiftDesignation();
                }
            };
            yield return cancelCommand;
        }
    }


    public void GizmoRaiseGroup()
    {
        TryRaiseGroup(showMessage: true);
    }


    private void GizmoRaiseGroupCommandAction()
    {
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        LiftControlMode controlMode = settings?.liftControlMode ?? LiftControlMode.Remote;

        // 电力检查
        if (settings is { enableLiftPower: true } && !IsReadyForLiftPower())
        {
            return;
        }

        // Remote 模式：直接执行
        if (controlMode == LiftControlMode.Remote)
        {
            TryRaiseGroup(showMessage: true);
            RefreshGizmoCache(); // 立即刷新缓存，让 UI 响应更快
        }
        // Manual/Console 模式：设置期望状态
        else
        {
            wantedLiftAction = ULS_LiftActionRequest.Raise;
            UpdateLiftDesignation();
            RefreshGizmoCache(); // 立即刷新缓存，让 UI 响应更快
        }
    }


    public void GizmoLowerGroup(IntVec3 startCell)
    {
        TryLowerGroup(startCell, showMessage: true);
        RefreshGizmoCache(); // 立即刷新缓存，让 UI 响应更快
    }


    public void GizmoLowerFromBuilding(Building building, IntVec3 controllerCell)
    {
        TryLowerGroup(controllerCell, showMessage: true);
        RefreshGizmoCache(); // 立即刷新缓存，让 UI 响应更快
    }

    private void OnGizmoAction_SetGroupId(List<Building_WallController> selectedControllers)
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
                    List<Building_WallController> expandedList = new List<Building_WallController>();
                    ExpandSelectedControllersToMultiCellHiddenGroupMembers(mapLocal,
                        selectedControllers, expandedList);
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

                        // 立即刷新所有相关控制器的缓存，让 UI 响应更快
                        foreach (var ctrl in expandedList)
                        {
                            ctrl?.RefreshGizmoCache();
                        }
                    }
                }));
        }
    }

    private void OnGizmoAction_MergeGroups(List<Building_WallController> selectedControllers)
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
        List<Building_WallController> expandedList = new List<Building_WallController>();
        ExpandSelectedControllersToMultiCellHiddenGroupMembers(map, selectedControllers, expandedList);
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

        // 立即刷新所有相关控制器的缓存，让 UI 响应更快
        foreach (var ctrl in expandedList)
        {
            ctrl?.RefreshGizmoCache();
        }
    }

    private void OnGizmoAction_SplitToNewGroup(List<Building_WallController> selectedControllers)
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

                // 立即刷新所有被拆分控制器的缓存，让 UI 响应更快
                foreach (var ctrl in selectedControllers)
                {
                    ctrl?.RefreshGizmoCache();
                }
            }
        }
    }

    private void OnGizmoAction_SetAutoGroupFilter(HashSet<int> groupIds)
    {
        ULS_AutoGroupMapComponent autoGroupComp = Map.GetComponent<ULS_AutoGroupMapComponent>();
        if (autoGroupComp == null) return;

        List<FloatMenuOption> options = new List<FloatMenuOption>();
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

    private void OnGizmoAction_SelectGroupControllers()
    {
        Map map = Map;
        if (map == null) return;

        ULS_ControllerGroupMapComponent groupComp =
            map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp == null || controllerGroupId < 1) return;

        if (!groupComp.TryGetGroupControllerCells(controllerGroupId, out var cells) ||
            cells == null || cells.Count == 0) return;

        // 如果按住Shift键，则追加选择；否则替换选择
        if (!Event.current.shift)
        {
            Find.Selector.ClearSelection();
        }

        foreach (var cell in cells)
        {
            if (ULS_Utility.TryGetControllerAt(map, cell, out var controller))
            {
                Find.Selector.Select(controller, playSound: false, forceDesignatorDeselect: false);
            }
        }
    }

    private void OnGizmoAction_ToggleInvertedMode(HashSet<int> groupIds)
    {
        ULS_AutoGroupMapComponent autoGroupComp = Map?.GetComponent<ULS_AutoGroupMapComponent>();
        if (autoGroupComp == null) return;

        // 确定目标状态：如果任意分组处于非反转状态，则全部切换到反转状态
        bool targetInverted = false;
        foreach (int gid in groupIds)
        {
            if (!autoGroupComp.GetGroupInvertedMode(gid))
            {
                targetInverted = true;
                break;
            }
        }

        // 统一设置所有分组的反转模式状态
        foreach (int gid in groupIds)
        {
            autoGroupComp.SetGroupInvertedMode(gid, targetInverted);
        }

        // 立即触发所有受影响分组的升降判定，确保模式切换立即生效
        foreach (int gid in groupIds)
        {
            autoGroupComp.ForceProcessAutoGroup(gid);
        }
    }
}