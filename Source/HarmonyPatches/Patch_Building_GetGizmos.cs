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

        // 分支处理：单格建筑 vs 多格建筑（优化：使用专门的验证方法）
        if (__instance.def.Size == IntVec2.One)
        {
            // 单格逻辑：使用专门的降下验证方法
            if (!controller.CanLowerSingleCellBuilding(out string singleCellDisableReason))
            {
                lowerCommand.Disable(singleCellDisableReason);
            }
        }
        else
        {
            // 多格逻辑：委托控制器验证（避免在 Patch 中遍历）
            if (!controller.CanLowerMultiCellBuilding(__instance, out string multiCellDisableReason))
            {
                lowerCommand.Disable(multiCellDisableReason);
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
