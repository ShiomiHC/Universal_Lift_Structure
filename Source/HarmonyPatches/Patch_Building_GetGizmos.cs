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
                    // 直接在控制器排队 Lower 操作
                    controller.QueueLiftAction(isRaise: false, controllerCell);
                    return;
                }

                if (mode is LiftControlMode.Console)
                {
                    if (!ULS_Utility.TryGetNearestLiftConsoleByDistance(__instance.Map, controllerCell,
                            out ThingWithComps console))
                    {
                        ThingDef consoleDef = DefDatabase<ThingDef>.GetNamedSilentFail("ULS_LiftConsole");
                        bool anyConsoleExists = consoleDef != null && __instance.Map.listerThings
                            .ThingsOfDef(consoleDef).Any(t => t.Faction == Faction.OfPlayer);
                        string messageKey = anyConsoleExists ? "ULS_LiftConsolePowerOff" : "ULS_LiftConsoleMissing";
                        Messages.Message(messageKey.Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    CompLiftConsole consoleComp = console.GetComp<CompLiftConsole>();
                    if (consoleComp == null)
                    {
                        Messages.Message("ULS_LiftConsoleMissing".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    consoleComp.EnqueueRequest(new ULS_LiftRequest(ULS_LiftRequestType.LowerGroup, controller,
                        controllerCell));
                }
            }
        };


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
                    foreach (var t in groupCells)
                    {
                        if (!ULS_Utility.TryGetControllerAt(__instance.Map, t, out Building_WallController c) ||
                            c == null)
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


                    if (c.InLiftProcessForUI)
                    {
                        lowerCommand.Disable("ULS_LiftInProcess".Translate());

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


        __result = Append(__result, lowerCommand);
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
