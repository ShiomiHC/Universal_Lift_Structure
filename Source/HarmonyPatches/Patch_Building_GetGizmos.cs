namespace Universal_Lift_Structure;

/// 文件意图：Harmony 补丁：对 Building.GetGizmos 做 Postfix，在满足条件时把“降下”按钮挂到被选中的结构（Building/edifice）上。
[HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
public static class Patch_Building_GetGizmos
{
    /// 方法意图：
    /// - 通过 ULS_Utility.CanInjectLowerGizmo 判定该结构是否应注入按钮（与控制器 TryLowerGroup 的预检条件对齐，避免“按钮可点但必失败”）。
    /// - 通过 ULS_Utility.TryGetAnyControllerUnderBuilding 在占格内找到任意一个 Building_WallController，并把 controller.GizmoLowerGroup 作为按钮回调。
    public static void Postfix(Building __instance, ref IEnumerable<Gizmo> __result)
    {
        if (__instance is null || !__instance.Spawned)
        {
            return;
        }

        if (!ULS_Utility.CanInjectLowerGizmo(__instance))
        {
            return;
        }

        if (!ULS_Utility.TryGetAnyControllerUnderBuilding(__instance, out Building_WallController controller, out IntVec3 controllerCell))
        {
            return;
        }

        // 自动控制器：禁止手动“降下/收纳”按钮。
        // 说明：自动组的升降应完全由自动系统驱动，避免玩家手动干预导致状态不一致。
        if (ULS_AutoGroupUtility.IsAutoController(controller))
        {
            return;
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        LiftControlMode mode = settings?.liftControlMode ?? LiftControlMode.Remote;

        Command_Action lowerCommand = new()
        {
            defaultLabel = "ULS_LowerGroup".Translate(),
            icon = TexCommand.ForbidOff,
            action = () =>
            {
                if (mode is LiftControlMode.Remote)
                {
                    controller.GizmoLowerFromBuilding(__instance, controllerCell);
                    return;
                }

                if (mode is LiftControlMode.Manual)
                {
                    ULS_FlickTrigger trigger = controller.GetProxyFlickTrigger();
                    if (trigger is null)
                    {
                        Messages.Message("ULS_LiftTriggerMissing".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    trigger.EnqueueRequest(new(ULS_LiftRequestType.LowerGroup, controller, controllerCell));
                    return;
                }

                if (mode is LiftControlMode.Console)
                {
                    if (!ULS_Utility.TryGetNearestLiftConsoleByDistance(__instance.Map, controllerCell, out ThingWithComps console))
                    {
                        Messages.Message("ULS_LiftConsoleMissing".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    // Console 模式：flick 目标应绑定在控制台。
                    ULS_FlickTrigger trigger = ULS_FlickUtility.GetOrCreateFlickProxyTriggerAt(console.Map, console.Position);
                    if (trigger is null)
                    {
                        Messages.Message("ULS_LiftTriggerMissing".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    trigger.EnqueueRequest(new(ULS_LiftRequestType.LowerGroup, controller, controllerCell));
                }
            }
        };

            // 严格规则：仅允许玩家派系的结构执行“降下/收纳”。
            // 但为了可发现性：对非玩家结构仍注入按钮，并以禁用原因提示。
            if (__instance.Faction != Faction.OfPlayer)
            {
                lowerCommand.Disable("ULS_LowerNotPlayerOwned".Translate());
                __result = Append(__result, lowerCommand);
                return;
            }

            int groupMaxSize = settings?.groupMaxSize ?? 20;
            if (groupMaxSize < 1)
            {
                groupMaxSize = 20;
            }

            // Console 模式：无控制台时禁用（要求地图内存在控制台才允许提交请求）
            if (mode == LiftControlMode.Console)
            {
                if (!ULS_Utility.TryGetNearestLiftConsoleByDistance(__instance.Map, controllerCell, out _))
                {
                    lowerCommand.Disable("ULS_LiftConsoleMissing".Translate());
                }
            }

            if (__instance.def.Size == IntVec2.One)
            {
                ULS_ControllerGroupMapComponent groupComp = __instance.Map.GetComponent<ULS_ControllerGroupMapComponent>();
                int groupId = controller.ControllerGroupId;
                if (groupComp != null && groupId > 0 &&
                    groupComp.TryGetGroupControllerCells(groupId, out List<IntVec3> groupCells) && groupCells != null)
                {
                    if (groupCells.Count > groupMaxSize)
                    {
                        lowerCommand.Disable("ULS_GroupTooLarge".Translate(groupMaxSize));
                    }
                    else
                    {
                        // 升降中间态：组内任一控制器处于中间态时禁用按钮，避免无效操作与误导提示。
                        for (int i = 0; i < groupCells.Count; i++)
                        {
                            if (!ULS_Utility.TryGetControllerAt(__instance.Map, groupCells[i], out Building_WallController c) || c == null)
                            {
                                continue;
                            }

                            if (c.InLiftProcessForUI)
                            {
                                lowerCommand.Disable("ULS_LiftInProcess".Translate());
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                Map map = __instance.Map;
                IntVec3 rootCell = __instance.Position;
                CellRect rect = __instance.OccupiedRect();

                if (!ULS_Utility.TryGetControllerAt(map, rootCell, out _))
                {
                    lowerCommand.Disable("ULS_MultiCellNeedControllerEveryCell".Translate());
                }
                else
                {
                    bool missingController = false;
                    bool anyStored = false;
                    bool anyInGroup = false;
                    foreach (IntVec3 cell in rect)
                    {
                        if (!ULS_Utility.TryGetControllerAt(map, cell, out Building_WallController c))
                        {
                            missingController = true;
                            break;
                        }

                        // 升降中间态：占格内任一控制器处于中间态时禁用按钮。
                        if (c.InLiftProcessForUI)
                        {
                            lowerCommand.Disable("ULS_LiftInProcess".Translate());
                            // 直接结束检查即可：禁用原因优先级最高。
                            break;
                        }

                        if (c.HasStored)
                        {
                            anyStored = true;
                            break;
                        }

                        if (c.MultiCellGroupRootCell.IsValid)
                        {
                            anyInGroup = true;
                            break;
                        }
                    }

                    if (missingController)
                    {
                        lowerCommand.Disable("ULS_MultiCellNeedControllerEveryCell".Translate());
                    }
                    else if (anyStored)
                    {
                        lowerCommand.Disable("ULS_MultiCellControllerHasStored".Translate());
                    }
                    else if (anyInGroup)
                    {
                        lowerCommand.Disable("ULS_MultiCellControllerInGroup".Translate());
                    }
                    else
                    {
                        ULS_MultiCellGroupMapComponent groupComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();
                        if (groupComp != null && groupComp.HasGroup(rootCell))
                        {
                            lowerCommand.Disable("ULS_MultiCellGroupAlreadyExists".Translate());
                        }
                    }
                }
            }

            // 注意：组内存在“已收纳”或“不可降下”的格时，不再禁用按钮；
            // 实际执行会按格跳过不可降下目标，从而避免整组中断。

            __result = Append(__result, lowerCommand);
        }

        
        /// 方法意图：保持原有 Gizmo 顺序不变，在末尾追加一个额外按钮。
        
        private static IEnumerable<Gizmo> Append(IEnumerable<Gizmo> source, Gizmo extra)
        {
            foreach (Gizmo gizmo in source)
            {
                yield return gizmo;
            }

            yield return extra;
        }
    }
