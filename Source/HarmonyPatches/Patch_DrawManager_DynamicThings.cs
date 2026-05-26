namespace Universal_Lift_Structure;

// 注入渲染逻辑：在升降动画过程中渲染被收纳建筑，可选在收纳状态下渲染虚影。
// 使用视口裁剪和 internal 字段直访以减少开销。
[HarmonyPatch(typeof(DynamicDrawManager), nameof(DynamicDrawManager.DrawDynamicThings))]
public static class Patch_DrawManager_DynamicThings
{
    private const float StoredVisible01 = 0.4f; // 收纳状态下的最小可见度

    private static readonly AccessTools.FieldRef<DynamicDrawManager, Map> MapRef =
        AccessTools.FieldRefAccess<DynamicDrawManager, Map>("map");


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

        var groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp == null)
        {
            return;
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        bool showGhost = settings is { enableOverlayDisplay: true, showStoredGhostOverlay: true };

        // 未开启虚影时只遍历动画中的控制器（极少），开启时遍历全部注册控制器（比 listerBuildings 快）
        IEnumerable<Building_WallController> controllersToIterate = showGhost
            ? groupComp.GetAllControllers()
            : groupComp.GetActiveAnimatingControllers();

        if (controllersToIterate == null)
        {
            return;
        }

        // ExpandedBy(2) 防止边缘控制器闪烁
        CellRect viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(2);

        foreach (var controller in controllersToIterate)
        {
            if (controller == null || !controller.Spawned)
            {
                continue;
            }

            if (!viewRect.Contains(controller.Position))
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

            IntVec3 storedCell = controller.storedCell;
            IntVec3 drawCell = storedCell.IsValid ? storedCell : controller.Position;
            Rot4 drawRot = controller.storedRotation;

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

                ULS_LiftClipRenderer.DrawLiftingStoredBuilding(storedBuilding, drawRot, drawCell, visible01, map,
                    c => controller.TryGetStoredLinkDirections(c, out LinkDirections dirs) ? dirs : null);
                continue;
            }

            if (showGhost && controller.HasStored)
            {
                ULS_GhostRenderer.DrawStoredBuildingGhost(drawCell, drawRot, storedBuilding.def, storedBuilding.Stuff);
            }
        }
    }
}
