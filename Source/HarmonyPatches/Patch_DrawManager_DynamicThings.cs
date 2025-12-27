namespace Universal_Lift_Structure;

/// 文件意图：全局动态物渲染补丁。在地图绘制循环中遍历控制器，负责渲染“升降过程中的裁剪动画（剪裁渲染）”以及“收纳状态下的静态虚影（若配置开启）”。
[HarmonyPatch(typeof(DynamicDrawManager), nameof(DynamicDrawManager.DrawDynamicThings))]
public static class Patch_DrawManager_DynamicThings
{
    // 收纳建筑在“降下完成/升起起始”时的可见比例。
    // 需求：不再完全降下；升起也不从完全降下开始。
    // 〇 后续若要改为区间（40%~60%）或可配置，请从这里调整。
    private const float StoredVisible01 = 0.4f;

    private static readonly AccessTools.FieldRef<DynamicDrawManager, Map> MapRef =
        AccessTools.FieldRefAccess<DynamicDrawManager, Map>("map");

    private static readonly AccessTools.FieldRef<Building_WallController, Rot4> StoredRotationRef =
        AccessTools.FieldRefAccess<Building_WallController, Rot4>("storedRotation");

    private static readonly AccessTools.FieldRef<Building_WallController, IntVec3> StoredCellRef =
        AccessTools.FieldRefAccess<Building_WallController, IntVec3>("storedCell");

    public static void Postfix(DynamicDrawManager __instance)
    {
        // 仅在游戏进行中绘制，避免主菜单/世界视图等非 Map 视图的调用路径产生副作用。
        if (Current.ProgramState is not ProgramState.Playing)
        {
            return;
        }

        Map map = __instance is null ? null : MapRef(__instance);
        if (map is null || map.Disposed)
        {
            return;
        }

        // 只绘制当前正在查看的地图。
        if (Find.CurrentMap != map)
        {
            return;
        }

        List<Building> colonistBuildings = map.listerBuildings?.allBuildingsColonist;
        if (colonistBuildings is null)
        {
            return;
        }

        for (int i = 0; i < colonistBuildings.Count; i++)
        {
            if (colonistBuildings[i] is not Building_WallController controller)
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
                    // 升起：从“收纳可见度”到完全可见
                    visible01 = Mathf.Lerp(StoredVisible01, 1f, rawProgress01);
                }
                else
                {
                    // 降下：从完全可见到“收纳可见度”
                    visible01 = Mathf.Lerp(1f, StoredVisible01, rawProgress01);
                }

                // Link 建筑在收纳/升降期间会 DeSpawn，按原版逻辑会丢失连接。
                // 这里优先使用控制器在“收纳瞬间”缓存的 LinkDirections，仅影响虚影自身（语义①）。
                Func<IntVec3, LinkDirections?> tryGetCachedLinkDirections = c =>
                    controller.TryGetStoredLinkDirections(c, out LinkDirections dirs)
                        ? dirs
                        : null;

                ULS_LiftClipRenderer.DrawLiftingStoredBuilding(storedBuilding, drawRot, drawCell, visible01, map, tryGetCachedLinkDirections);
                continue;
            }

            UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
            bool showGhost = settings is null || settings.showStoredGhostOverlay;
            if (showGhost && controller.HasStored)
            {
                ULS_GhostRenderer.DrawStoredBuildingGhost(drawCell, drawRot, storedBuilding.def, storedBuilding.Stuff);
            }
        }
    }
}
