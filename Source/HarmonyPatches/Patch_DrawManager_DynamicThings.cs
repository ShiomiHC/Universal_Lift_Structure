namespace Universal_Lift_Structure;

// ============================================================
// 【Harmony 补丁：DynamicDrawManager.DrawDynamicThings】
// ============================================================
// 作用：注入渲染逻辑，用于显示处于升降过程中的被收纳建筑。
// 主要功能：
// - 在控制器进行升降动画时，渲染随之升降的建筑模型。
// - 如果设置开启了“虚影显示”，则在收纳完成状态下渲染建筑虚影。
// ============================================================
[HarmonyPatch(typeof(DynamicDrawManager), nameof(DynamicDrawManager.DrawDynamicThings))]
public static class Patch_DrawManager_DynamicThings
{
    private const float StoredVisible01 = 0.4f; // 收纳状态下的最小可见度

    // 反射获取 DynamicDrawManager 的 map 字段
    private static readonly AccessTools.FieldRef<DynamicDrawManager, Map> MapRef =
        AccessTools.FieldRefAccess<DynamicDrawManager, Map>("map");

    // 反射获取 Building_WallController 的 storedRotation 字段
    private static readonly AccessTools.FieldRef<Building_WallController, Rot4> StoredRotationRef =
        AccessTools.FieldRefAccess<Building_WallController, Rot4>("storedRotation");

    // 反射获取 Building_WallController 的 storedCell 字段
    private static readonly AccessTools.FieldRef<Building_WallController, IntVec3> StoredCellRef =
        AccessTools.FieldRefAccess<Building_WallController, IntVec3>("storedCell");


    // ============================================================
    // 【后置补丁】
    // ============================================================
    // 在所有动态物体绘制完成后执行。
    // ============================================================
    public static void Postfix(DynamicDrawManager __instance)
    {
        // 仅在游戏进行中执行
        if (Current.ProgramState is not ProgramState.Playing)
        {
            return;
        }

        Map map = __instance is null ? null : MapRef(__instance);
        if (map is null || map.Disposed)
        {
            return;
        }

        // 仅绘制当前地图
        if (Find.CurrentMap != map)
        {
            return;
        }

        // 获取控制器组件以利用缓存列表进行优化
        var groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp == null)
        {
            return;
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        bool showGhost = settings is { enableOverlayDisplay: true, showStoredGhostOverlay: true };

        // 优化策略:
        // 1. 如果未开启虚影显示，仅遍历当前正在动画的控制器 (数量极少)
        // 2. 否则遍历所有注册的控制器 (比遍历所有 map.listerBuildings 快)
        IEnumerable<Building_WallController> controllersToIterate = showGhost
            ? groupComp.GetAllControllers()
            : groupComp.GetActiveAnimatingControllers();

        if (controllersToIterate == null)
        {
            return;
        }

        foreach (var controller in controllersToIterate)
        {
            if (controller == null || !controller.Spawned)
            {
                continue;
            }

            // 获取内部收纳的物体
            ThingOwner owner = controller.GetDirectlyHeldThings();
            if (owner is null || owner.Count <= 0)
            {
                continue;
            }

            if (owner[0] is not Building storedBuilding)
            {
                continue;
            }

            // 获取存储时的位置和旋转信息
            IntVec3 storedCell = StoredCellRef(controller);
            IntVec3 drawCell = storedCell.IsValid ? storedCell : controller.Position;
            Rot4 drawRot = StoredRotationRef(controller);

            // 检查是否处于升降动画过程中
            if (controller.TryGetLiftProgress01(out float rawProgress01, out bool isRaising))
            {
                float visible01;
                // 计算可见度：升起过程从 StoredVisible01 -> 1.0，降下过程反之
                if (isRaising)
                {
                    visible01 = Mathf.Lerp(StoredVisible01, 1f, rawProgress01);
                }
                else
                {
                    visible01 = Mathf.Lerp(1f, StoredVisible01, rawProgress01);
                }


                // 获取连接方向（用于正确绘制连接纹理）
                LinkDirections? TryGetCachedLinkDirections(IntVec3 c) =>
                    controller.TryGetStoredLinkDirections(c, out LinkDirections dirs)
                        ? dirs
                        : null;

                // 调用剪裁渲染器绘制建筑
                ULS_LiftClipRenderer.DrawLiftingStoredBuilding(storedBuilding, drawRot, drawCell, visible01, map,
                    TryGetCachedLinkDirections);
                continue;
            }

            // 非动画过程，检查是否需要显示静态虚影
            if (showGhost && controller.HasStored)
            {
                ULS_GhostRenderer.DrawStoredBuildingGhost(drawCell, drawRot, storedBuilding.def, storedBuilding.Stuff);
            }
        }
    }
}