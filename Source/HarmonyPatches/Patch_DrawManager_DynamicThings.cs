namespace Universal_Lift_Structure;

[HarmonyPatch(typeof(DynamicDrawManager), nameof(DynamicDrawManager.DrawDynamicThings))]
public static class Patch_DrawManager_DynamicThings
{
    private const float StoredVisible01 = 0.4f;

    private static readonly AccessTools.FieldRef<DynamicDrawManager, Map> MapRef =
        AccessTools.FieldRefAccess<DynamicDrawManager, Map>("map");

    private static readonly AccessTools.FieldRef<Building_WallController, Rot4> StoredRotationRef =
        AccessTools.FieldRefAccess<Building_WallController, Rot4>("storedRotation");

    private static readonly AccessTools.FieldRef<Building_WallController, IntVec3> StoredCellRef =
        AccessTools.FieldRefAccess<Building_WallController, IntVec3>("storedCell");

    public static void Postfix(DynamicDrawManager __instance)
    {
        if (Current.ProgramState is not ProgramState.Playing)
        {
            return;
        }

        Map map = __instance is null ? null : MapRef(__instance);
        if (map is null || map.Disposed)
        {
            return;
        }


        if (Find.CurrentMap != map)
        {
            return;
        }

        List<Building> colonistBuildings = map.listerBuildings?.allBuildingsColonist;
        if (colonistBuildings is null)
        {
            return;
        }

        foreach (var t in colonistBuildings)
        {
            if (t is not Building_WallController controller)
            {
                continue;
            }

            ThingOwner owner = controller.GetDirectlyHeldThings();
            if (owner is null || owner.Count <= 0)
            {
                continue;
            }

            if (owner[0] is not Building storedBuilding)
            {
                continue;
            }

            IntVec3 storedCell = StoredCellRef(controller);
            IntVec3 drawCell = storedCell.IsValid ? storedCell : controller.Position;
            Rot4 drawRot = StoredRotationRef(controller);

            if (controller.TryGetLiftProgress01(out float rawProgress01, out bool isRaising))
            {
                float visible01;
                if (isRaising)
                {
                    visible01 = Mathf.Lerp(StoredVisible01, 1f, rawProgress01);
                }
                else
                {
                    visible01 = Mathf.Lerp(1f, StoredVisible01, rawProgress01);
                }


                LinkDirections? TryGetCachedLinkDirections(IntVec3 c) =>
                    controller.TryGetStoredLinkDirections(c, out LinkDirections dirs)
                        ? dirs
                        : null;

                ULS_LiftClipRenderer.DrawLiftingStoredBuilding(storedBuilding, drawRot, drawCell, visible01, map,
                    TryGetCachedLinkDirections);
                continue;
            }

            UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
            bool showGhost = settings is { enableOverlayDisplay: true, showStoredGhostOverlay: true };
            if (showGhost && controller.HasStored)
            {
                ULS_GhostRenderer.DrawStoredBuildingGhost(drawCell, drawRot, storedBuilding.def, storedBuilding.Stuff);
            }
        }
    }
}