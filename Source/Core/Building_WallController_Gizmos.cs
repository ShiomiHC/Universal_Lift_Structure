namespace Universal_Lift_Structure;

/// 文件意图：Building_WallController - Gizmos（UI 按钮）部分。
/// 包含：GetGizmos 方法、所有 Gizmo 按钮创建逻辑、按钮命令 action。
public partial class Building_WallController
{
    // ==================== GetGizmos 方法 ====================

    public override IEnumerable<Gizmo> GetGizmos()
    {
        // 返回基类 Gizmos
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        // 获取已选中的控制器列表
        List<Building_WallController> selectedControllers = GetSelectedControllers();
        if (selectedControllers.Count == 0)
        {
            selectedControllers.Add(this);
        }

        // 用于自动组过滤器按钮的变量声明（需要在外部作用域）
        ULS_AutoGroupMapComponent autoGroupComp;
        HashSet<int> groupIds;

        // 只有当前控制器是主选控制器时才显示分组管理按钮
        if (selectedControllers.Count <= 0 || selectedControllers[0] == this)
        {
            // 检查是否有非玩家控制器
            bool anyNotPlayerOwned = false;
            for (int i = 0; i < selectedControllers.Count; i++)
            {
                if (selectedControllers[i]?.Faction != Faction.OfPlayer)
                {
                    anyNotPlayerOwned = true;
                    break;
                }
            }

            // 1. 设置组 ID 按钮
            if (!anyNotPlayerOwned)
            {
                yield return new Command_Action
                {
                    defaultLabel = "ULS_GroupSetIdWithValue".Translate(controllerGroupId),
                    icon = TexCommand.ForbidOff,
                    action = delegate
                    {
                        Map mapLocal = Map;
                        if (mapLocal != null)
                        {
                            ULS_ControllerGroupMapComponent groupComp = mapLocal.GetComponent<ULS_ControllerGroupMapComponent>();
                            if (groupComp != null)
                            {
                                Find.WindowStack.Add(new ULS_Dialog_SetControllerGroupId(controllerGroupId, delegate(int newGroupId)
                                {
                                    List<Building_WallController> expandedList = ExpandSelectedControllersToMultiCellHiddenGroupMembers(mapLocal, selectedControllers);
                                    if (!ULS_AutoGroupUtility.CanAssignControllersToGroup(mapLocal, expandedList, newGroupId, out string rejectKey))
                                    {
                                        MessageReject(rejectKey, this);
                                    }
                                    else
                                    {
                                        List<IntVec3> cellsToAssign = new List<IntVec3>();
                                        for (int j = 0; j < expandedList.Count; j++)
                                        {
                                            Building_WallController controller = expandedList[j];
                                            if (controller != null && controller.Map == mapLocal && controller.Spawned)
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
                    }
                };
            }

            // 2. 合并组按钮（需要至少选中 2 个控制器）
            if (selectedControllers.Count >= 2 && !anyNotPlayerOwned)
            {
                yield return new Command_Action
                {
                    defaultLabel = "ULS_GroupMergeToPrimary".Translate(),
                    icon = TexCommand.ForbidOff,
                    action = delegate
                    {
                        Map map = Map;
                        if (map != null)
                        {
                            ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
                            if (groupComp != null)
                            {
                                // 确保主控制器有有效的组 ID
                                int primaryGroupId = controllerGroupId;
                                if (primaryGroupId < 1)
                                {
                                    primaryGroupId = groupComp.CreateNewGroupId();
                                    controllerGroupId = primaryGroupId;
                                    groupComp.RegisterOrUpdateController(this);
                                }

                                bool primaryIsAuto = ULS_AutoGroupUtility.IsAutoGroup(map, primaryGroupId);
                                List<Building_WallController> expandedList = ExpandSelectedControllersToMultiCellHiddenGroupMembers(map, selectedControllers);
                                HashSet<int> mergedGroupIds = new HashSet<int>();

                                // 合并所有选中控制器的组到主组
                                for (int j = 0; j < expandedList.Count; j++)
                                {
                                    Building_WallController controller = expandedList[j];
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
                        }
                    }
                };
            }

            // 3. 拆分到新组按钮（多格隐组中的控制器不能拆分）
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
                            ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
                            if (groupComp != null)
                            {
                                int newGroupId = groupComp.CreateNewGroupId();
                                List<IntVec3> cellsToAssign = new List<IntVec3>();
                                for (int j = 0; j < selectedControllers.Count; j++)
                                {
                                    Building_WallController controller = selectedControllers[j];
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

            // 4. 自动组过滤器按钮
            if (!anyNotPlayerOwned && Map != null && GetComp<ULS_AutoGroupMarker>() != null)
            {
                autoGroupComp = Map.GetComponent<ULS_AutoGroupMapComponent>();
                if (autoGroupComp != null)
                {
                    List<Building_WallController> expandedList = ExpandSelectedControllersToMultiCellHiddenGroupMembers(Map, selectedControllers);
                    groupIds = new HashSet<int>();
                    bool allAreAutoGroup = true;

                    // 收集所有选中控制器的组 ID
                    for (int i = 0; i < expandedList.Count; i++)
                    {
                        Building_WallController controller = expandedList[i];
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

                    // 只有当所有控制器都是自动组成员时才显示过滤器按钮
                    if (allAreAutoGroup && groupIds.Count > 0)
                    {
                        CompProperties_ULS_AutoGroupMarker props = GetComp<ULS_AutoGroupMarker>().Props;
                        bool hasType = false;
                        bool mixedTypes = false;
                        ULS_AutoGroupType currentType = ULS_AutoGroupType.Friendly;

                        // 检查所有组的过滤器类型是否一致
                        foreach (int gid in groupIds)
                        {
                            ULS_AutoGroupType groupType = autoGroupComp.GetOrInitGroupFilterType(gid, props.autoGroupType);
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

                        // 生成按钮标签
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

                                // 本地函数：为自动组过滤器添加选项
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

        // 自动组控制器不显示升起按钮
        if (GetComp<ULS_AutoGroupMarker>() != null)
        {
            yield break;
        }

        // 5. 升起组按钮
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

        // 检查升起条件
        if (currentMap == null)
        {
            raiseCommand.Disable("ULS_NoStored".Translate());
            disabled = true;
        }
        else
        {
            ULS_ControllerGroupMapComponent groupComp = currentMap.GetComponent<ULS_ControllerGroupMapComponent>();
            int groupId = controllerGroupId;

            if (groupComp == null || groupId < 1 || !groupComp.TryGetGroupControllerCells(groupId, out List<IntVec3> groupCells) || groupCells == null || groupCells.Count == 0)
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
                // 检查是否有存储物
                bool hasStored = false;
                for (int i = 0; i < groupCells.Count; i++)
                {
                    if (ULS_Utility.TryGetControllerAt(currentMap, groupCells[i], out Building_WallController controller) && controller.HasStored)
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
                    // 检查是否有控制器正在升降过程中
                    for (int i = 0; i < groupCells.Count; i++)
                    {
                        if (ULS_Utility.TryGetControllerAt(currentMap, groupCells[i], out Building_WallController controller) && controller.InLiftProcess)
                        {
                            raiseCommand.Disable("ULS_LiftInProcess".Translate());
                            disabled = true;
                            break;
                        }
                    }

                    // 检查电力状态
                    if (!disabled && settings != null && settings.enableLiftPower)
                    {
                        for (int i = 0; i < groupCells.Count; i++)
                        {
                            if (ULS_Utility.TryGetControllerAt(currentMap, groupCells[i], out Building_WallController controller) &&
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

        // 检查控制台模式
        if (!disabled && (settings?.liftControlMode ?? LiftControlMode.Remote) == LiftControlMode.Console &&
            !ULS_Utility.TryGetNearestLiftConsoleByDistance(currentMap, Position, out ThingWithComps _))
        {
            raiseCommand.Disable("ULS_LiftConsoleMissing".Translate());
        }

        yield return raiseCommand;
    }

    // ==================== Gizmo 命令 Action 方法 ====================

        /// Gizmo 按钮：手动升起组（用户点击按钮触发）
        public void GizmoRaiseGroup()
    {
        TryRaiseGroup(showMessage: true);
    }

        /// Gizmo 命令 Action：升起组按钮的实际逻辑
    /// 处理不同控制模式（Remote / Manual / Console）
        private void GizmoRaiseGroupCommandAction()
    {
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        LiftControlMode controlMode = settings?.liftControlMode ?? LiftControlMode.Remote;

        // 检查电力状态
        if (settings != null && settings.enableLiftPower && !IsReadyForLiftPower())
        {
            return;
        }

        // Remote 模式：直接执行
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
                    // Manual 模式：通过 Flick 代理触发
                    ULS_FlickTrigger trigger = GetProxyFlickTrigger();
                    if (trigger == null)
                    {
                        MessageReject("ULS_LiftTriggerMissing", this);
                    }
                    else
                    {
                        trigger.EnqueueRequest(new ULS_LiftRequest(ULS_LiftRequestType.RaiseGroup, this, IntVec3.Invalid));
                    }
                    break;
                }

                case LiftControlMode.Console:
                {
                    // Console 模式：通过最近的控制台触发
                    if (!ULS_Utility.TryGetNearestLiftConsoleByDistance(Map, Position, out ThingWithComps console))
                    {
                        MessageReject("ULS_LiftConsoleMissing", this);
                        break;
                    }

                    ULS_FlickTrigger consoleTrigger = ULS_FlickUtility.GetOrCreateFlickProxyTriggerAt(console.Map, console.Position);
                    if (consoleTrigger == null)
                    {
                        MessageReject("ULS_LiftTriggerMissing", this);
                    }
                    else
                    {
                        consoleTrigger.EnqueueRequest(new ULS_LiftRequest(ULS_LiftRequestType.RaiseGroup, this, IntVec3.Invalid));
                    }
                    break;
                }
            }
        }
    }

        /// Gizmo 按钮：降下组（从空格子开始）
        public void GizmoLowerGroup(IntVec3 startCell)
    {
        TryLowerGroup(startCell, showMessage: true);
    }

        /// Gizmo 按钮：从建筑物降下（通过 Harmony 补丁挂载到建筑物上）
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
