namespace Universal_Lift_Structure;

// ============================================================
// 【Harmony 补丁：Building.GetGizmos】
// ============================================================
// 作用：向 Building（主要是 WallController）注入自定义的 Gizmo。
// 主要功能：
// - 添加“降下编组” (ULS_LowerGroup) 命令。
// - 添加“取消升降” (ULS_CancelLift) 命令。
// - 根据当前控制器模式 (Remote/Console) 和状态 (电力、组大小、占用情况) 对 Gizmo 进行启用/禁用状态管理。
// ============================================================
[HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
public static class Patch_Building_GetGizmos
{
    // ============================================================
    // 【后置补丁】
    // ============================================================
    // 在原版 Gizmo 生成后追加自定义 Gizmo。
    // ============================================================
    public static void Postfix(Building __instance, ref IEnumerable<Gizmo> __result)
    {
        // 基础有效性检查
        if (__instance is null || !__instance.Spawned)
        {
            return;
        }

        // 检查是否允许注入“降下” Gizmo（过滤非控制器或不符合条件的建筑）
        if (!ULS_Utility.CanInjectLowerGizmo(__instance))
        {
            return;
        }

        // 尝试获取当前建筑位置上的控制器实例
        if (!ULS_Utility.TryGetAnyControllerUnderBuilding(__instance, out Building_WallController controller,
                out IntVec3 controllerCell))
        {
            return;
        }


        // 自动控制器不需要手动降下操作，跳过
        if (ULS_AutoGroupUtility.IsAutoController(controller))
        {
            return;
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        LiftControlMode mode = settings?.liftControlMode ?? LiftControlMode.Remote;

        // 创建“降下编组”命令
        Command_Action lowerCommand = new()
        {
            defaultLabel = "ULS_LowerGroup".Translate(),
            defaultDesc = "ULS_LowerGroupDesc".Translate(),
            icon = ULS_GizmoTextures.LowerGroup,
            action = () =>
            {
                // 远程模式：直接执行
                if (mode is LiftControlMode.Remote)
                {
                    controller.GizmoLowerFromBuilding(__instance, controllerCell);
                    return;
                }

                // 手动/控制台模式：仅设置期望状态，等待工作执行
                controller.SetWantedLiftAction(ULS_LiftActionRequest.Lower, controllerCell);
            }
        };


        // 权限检查：非玩家所属无法操作
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


        // 控制台模式检查：需要范围内有通电的控制台
        if (mode == LiftControlMode.Console)
        {
            if (!ULS_Utility.TryGetNearestLiftConsoleByDistance(__instance.Map, controllerCell, out _))
            {
                ThingDef consoleDef = DefDatabase<ThingDef>.GetNamedSilentFail("ULS_LiftConsole");
                bool anyConsoleExists = consoleDef != null && __instance.Map.listerThings.ThingsOfDef(consoleDef)
                    .Any(t => t.Faction == Faction.OfPlayer);
                if (anyConsoleExists)
                {
                    lowerCommand.Disable("ULS_LiftConsolePowerOff".Translate());
                }
                else
                {
                    lowerCommand.Disable("ULS_LiftConsoleMissing".Translate());
                }
            }
        }

        // 分支处理：单格建筑 vs 多格建筑
        if (__instance.def.Size == IntVec2.One)
        {
            // 单格逻辑：检查编组状态
            ULS_ControllerGroupMapComponent groupComp = __instance.Map.GetComponent<ULS_ControllerGroupMapComponent>();
            int groupId = controller.ControllerGroupId;
            if (groupComp != null && groupId > 0 &&
                groupComp.TryGetGroupControllerCells(groupId, out List<IntVec3> groupCells) && groupCells != null)
            {
                // 检查组大小限制
                if (groupCells.Count > groupMaxSize)
                {
                    lowerCommand.Disable("ULS_GroupTooLarge".Translate(groupMaxSize));
                }
                else
                {
                    // 遍历组内成员检查状态
                    foreach (var t in groupCells)
                    {
                        if (!ULS_Utility.TryGetControllerAt(__instance.Map, t, out Building_WallController c) ||
                            c == null)
                        {
                            continue;
                        }

                        // 检查是否正在运行中
                        if (c.InLiftProcessForUI)
                        {
                            lowerCommand.Disable("ULS_LiftInProcess".Translate());
                            break;
                        }

                        // 检查电力
                        if (settings is { enableLiftPower: true } && !c.IsReadyForLiftPower())
                        {
                            lowerCommand.Disable("ULS_PowerOff".Translate());
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            // 多格逻辑：需要检查占用的所有格子
            Map map = __instance.Map;
            IntVec3 rootCell = __instance.Position;
            CellRect rect = __instance.OccupiedRect();

            // 根位置必须有控制器
            if (!ULS_Utility.TryGetControllerAt(map, rootCell, out _))
            {
                lowerCommand.Disable("ULS_MultiCellNeedControllerEveryCell".Translate());
            }
            else
            {
                // 检查是否与多格组冲突
                ULS_MultiCellGroupMapComponent groupComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();
                if (groupComp != null && groupComp.HasGroup(rootCell))
                {
                    lowerCommand.Disable("ULS_MultiCellGroupAlreadyExists".Translate());
                }
                else
                {
                    bool missingController = false;
                    bool anyStored = false;
                    bool anyInGroup = false;
                    ULS_ControllerGroupMapComponent ctrlGroupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();

                    // 遍历多格建筑占用的每一个格子
                    foreach (IntVec3 cell in rect)
                    {
                        if (!ULS_Utility.TryGetControllerAt(map, cell, out Building_WallController c))
                        {
                            missingController = true;
                            break;
                        }

                        // 组超限检测（针对每个格子所在的控制器组）
                        int cellGroupId = c.ControllerGroupId;
                        if (ctrlGroupComp != null && cellGroupId > 0 &&
                            ctrlGroupComp.TryGetGroupControllerCells(cellGroupId, out List<IntVec3> cellGroupCells) &&
                            cellGroupCells != null && cellGroupCells.Count > groupMaxSize)
                        {
                            lowerCommand.Disable("ULS_GroupTooLarge".Translate(groupMaxSize));
                            break;
                        }

                        // 运行状态检测
                        if (c.InLiftProcessForUI)
                        {
                            lowerCommand.Disable("ULS_LiftInProcess".Translate());

                            break;
                        }

                        // 电力检测
                        if (settings is { enableLiftPower: true } && !c.IsReadyForLiftPower())
                        {
                            lowerCommand.Disable("ULS_PowerOff".Translate());
                            break;
                        }

                        // 存储状态检测
                        if (c.HasStored)
                        {
                            anyStored = true;
                            break;
                        }

                        // 多格组归属检测
                        if (c.MultiCellGroupRootCell.IsValid)
                        {
                            anyInGroup = true;
                            break;
                        }
                    }

                    // 汇总错误信息
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
                }
            }
        }


        __result = Append(__result, lowerCommand);

        // 添加取消升降 Gizmo（仅 Manual/Console 模式且存在期望状态时）
        if (controller.WantedLiftAction != ULS_LiftActionRequest.None &&
            mode is not LiftControlMode.Remote)
        {
            Command_Action cancelCommand = new Command_Action
            {
                defaultLabel = "ULS_CancelLift".Translate(),
                defaultDesc = "ULS_CancelLiftDesc".Translate(),
                icon = TexCommand.ClearPrioritizedWork,
                action = () =>
                {
                    // 重置期望状态并更新 Designation
                    controller.CancelLiftAction();
                }
            };
            __result = Append(__result, cancelCommand);
        }
    }


    // ============================================================
    // 【辅助方法】
    // ============================================================
    // 将单个 Gizmo 追加到 IEnumerable 中。
    // ============================================================
    private static IEnumerable<Gizmo> Append(IEnumerable<Gizmo> source, Gizmo extra)
    {
        foreach (Gizmo gizmo in source)
        {
            yield return gizmo;
        }

        yield return extra;
    }
}
