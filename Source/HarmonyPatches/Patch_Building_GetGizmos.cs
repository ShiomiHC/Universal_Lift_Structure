namespace Universal_Lift_Structure;

[HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
public static class Patch_Building_GetGizmos
{
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

        if (!ULS_Utility.TryGetAnyControllerUnderBuilding(__instance, out Building_WallController controller,
                out IntVec3 controllerCell))
        {
            return;
        }


        // 自动控制器不响应手动降下操作
        if (ULS_AutoGroupUtility.IsAutoController(controller))
        {
            return;
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        LiftControlMode mode = settings?.liftControlMode ?? LiftControlMode.Remote;

        Command_Action lowerCommand = new()
        {
            defaultLabel = "ULS_LowerGroup".Translate(),
            defaultDesc = "ULS_LowerGroupDesc".Translate(),
            icon = ULS_GizmoTextures.LowerGroup,
            action = () =>
            {
                if (mode is LiftControlMode.Remote)
                {
                    controller.GizmoLowerFromBuilding(__instance, controllerCell);
                    return;
                }

                controller.SetWantedLiftAction(ULS_LiftActionRequest.Lower, controllerCell);
            }
        };


        if (__instance.Faction != Faction.OfPlayer)
        {
            lowerCommand.Disable("ULS_LowerNotPlayerOwned".Translate());
            __result = Append(__result, lowerCommand);
            return;
        }


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

        if (__instance.def.Size == IntVec2.One)
        {
            if (!controller.CanLowerSingleCellBuilding(out string singleCellDisableReason))
            {
                lowerCommand.Disable(singleCellDisableReason);
            }
        }
        else
        {
            if (!controller.CanLowerMultiCellBuilding(__instance, out string multiCellDisableReason))
            {
                lowerCommand.Disable(multiCellDisableReason);
            }
        }


        __result = Append(__result, lowerCommand);

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
                    controller.CancelLiftAction();
                }
            };
            __result = Append(__result, cancelCommand);
        }
    }


    private static IEnumerable<Gizmo> Append(IEnumerable<Gizmo> source, Gizmo extra)
    {
        foreach (Gizmo gizmo in source)
        {
            yield return gizmo;
        }

        yield return extra;
    }
}
