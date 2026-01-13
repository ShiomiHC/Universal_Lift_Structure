namespace Universal_Lift_Structure;

// ============================================================
// 【Harmony 补丁：SelectionDrawer.DrawSelectionOverlays】
// ============================================================
// 作用：绘制地图层面的全局覆盖层（Overlay）。
// 主要包含：
// - 控制器位置的填充高亮（白色方块），方便玩家快速识别控制器位置。
// - 自动编组范围的投影（彩色条纹），显示友好/中立/敌对的自动感应范围。
// ============================================================
[HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionOverlays))]
public static class Patch_SelectionDrawer_DrawSelectionOverlays
{
    // 复用列表以避免每帧分配内存
    private static readonly List<IntVec3> TmpFillCells = new();
    private static readonly List<Matrix4x4> TmpFillMatrices = new();

    private static readonly List<int> TmpGroupIds = new();
    private static readonly List<IntVec3> TmpFriendlyCells = new();
    private static readonly List<IntVec3> TmpHostileCells = new();
    private static readonly List<IntVec3> TmpNeutralCells = new();

    // ============================================================
    // 【扫描范围缓存类】
    // ============================================================
    // 用于减少每帧重复计算扫描范围的开销
    // ============================================================
    private sealed class ScanCache
    {
        public int membershipHash; // 组成员哈希，用于检测成员是否变动
        public int maxRadius; // 扫描半径
        public List<IntVec3> scanCells; // 计算出的范围单元格
    }

    private static readonly Dictionary<int, ScanCache> ScanCacheByGroupId = new();
    private static readonly HashSet<IntVec3> TmpScanSet = new();

    // ============================================================
    // 【后置补丁】
    // ============================================================
    // 执行自定义覆盖层绘制
    // ============================================================
    public static void Postfix()
    {
        // 仅在游戏进行中绘制
        if (Current.ProgramState is not ProgramState.Playing)
        {
            return;
        }

        // 截图模式下隐藏
        if (Find.ScreenshotModeHandler.Active)
        {
            return;
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        if (settings is null)
        {
            return;
        }

        // 检查全局开关和子开关
        bool overlayMaster = settings.enableOverlayDisplay;
        bool showFill = overlayMaster && settings.ShowControllerCell;
        bool showAutoProjection = overlayMaster && settings.showAutoGroupDetectionProjection;
        if (!showFill && !showAutoProjection)
        {
            return;
        }

        Map map = Find.CurrentMap;
        if (map is null || map.Disposed)
        {
            return;
        }

        List<Building> colonistBuildings = map.listerBuildings?.allBuildingsColonist;
        if (colonistBuildings is null || colonistBuildings.Count <= 0)
        {
            return;
        }

        // --- 1. 绘制控制器填充高亮 ---
        if (showFill)
        {
            TmpFillCells.Clear();
            foreach (var t in colonistBuildings)
            {
                if (t is not Building_WallController controller)
                {
                    continue;
                }

                TmpFillCells.Add(controller.Position);
            }

            if (TmpFillCells.Count > 0)
            {
                const float altOffset = 0.001f;
                // 使用淡白色填充
                Color fillColor = new Color(0.98f, 0.97f, 0.96f, 0.35f);

                // 使用 Instanced 绘制提高性能
                Material fillMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.Transparent, fillColor,
                    renderQueue: 2900);
                fillMat.enableInstancing = true;

                TmpFillMatrices.Clear();
                foreach (var cell in TmpFillCells)
                {
                    if (!cell.InBounds(map))
                    {
                        continue;
                    }

                    TmpFillMatrices.Add(Matrix4x4.TRS(
                        cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays) + new Vector3(0f, altOffset, 0f),
                        Quaternion.identity,
                        Vector3.one));
                }

                if (TmpFillMatrices.Count > 0)
                {
                    Graphics.DrawMeshInstanced(MeshPool.plane10, 0, fillMat, TmpFillMatrices);
                }
            }
        }

        // --- 2. 绘制自动编组投影 ---
        if (!showAutoProjection)
        {
            return;
        }


        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp is null)
        {
            return;
        }

        ULS_AutoGroupMapComponent autoGroupComp = map.GetComponent<ULS_AutoGroupMapComponent>();
        if (autoGroupComp is null)
        {
            return;
        }

        TmpGroupIds.Clear();
        groupComp.GetAllGroupIds(TmpGroupIds);
        if (TmpGroupIds.Count <= 0)
        {
            return;
        }

        TmpFriendlyCells.Clear();
        TmpHostileCells.Clear();
        TmpNeutralCells.Clear();

        foreach (var groupId in TmpGroupIds)
        {
            if (groupId < 1)
            {
                continue;
            }

            if (!groupComp.TryGetGroupControllerCells(groupId, out List<IntVec3> groupCells) ||
                groupCells is not { Count: > 0 })
            {
                continue;
            }


            // 寻找组代表（第一个控制器），用于获取 AutoGroupMarker 组件属性
            Building_WallController representative = null;
            foreach (var t in groupCells)
            {
                if (ULS_Utility.TryGetControllerAt(map, t, out Building_WallController c))
                {
                    representative = c;
                    break;
                }
            }

            if (representative == null)
            {
                continue;
            }

            ULS_AutoGroupMarker marker = representative.GetComp<ULS_AutoGroupMarker>();
            if (marker == null)
            {
                continue;
            }

            CompProperties_ULS_AutoGroupMarker props = marker.Props;
            int maxRadius = props.maxRadius;
            ULS_AutoGroupType filterType = autoGroupComp.GetOrInitGroupFilterType(groupId, props.autoGroupType);


            // 检查缓存有效性（基于成员哈希和半径）
            int membershipHash = ULS_Utility.ComputeMembershipHash(groupCells);
            if (!ScanCacheByGroupId.TryGetValue(groupId, out ScanCache cache) || cache == null)
            {
                cache = new ScanCache();
                ScanCacheByGroupId[groupId] = cache;
            }

            // 如果缓存失效，重新计算扫描范围
            if (cache.scanCells == null || cache.scanCells.Count == 0 || cache.membershipHash != membershipHash ||
                cache.maxRadius != maxRadius)
            {
                cache.membershipHash = membershipHash;
                cache.maxRadius = maxRadius;
                cache.scanCells = BuildScanCells(map, groupCells, maxRadius);
            }

            if (cache.scanCells is not { Count: > 0 })
            {
                continue;
            }

            // 根据类型分类到不同的绘制列表
            var targetList = filterType switch
            {
                ULS_AutoGroupType.Friendly => TmpFriendlyCells,
                ULS_AutoGroupType.Hostile => TmpHostileCells,
                ULS_AutoGroupType.Neutral => TmpNeutralCells,
                _ => null
            };
            targetList?.AddRange(cache.scanCells);
        }


        // 统一绘制各个类型的条纹
        if (TmpFriendlyCells.Count > 0)
        {
            GenDraw.DrawDiagonalStripes(TmpFriendlyCells, new Color(0.25f, 1.00f, 0.25f, 0.22f), altOffset: 0.0020f);
        }

        if (TmpNeutralCells.Count > 0)
        {
            GenDraw.DrawDiagonalStripes(TmpNeutralCells, new Color(1.00f, 0.95f, 0.25f, 0.22f), altOffset: 0.0023f);
        }

        if (TmpHostileCells.Count > 0)
        {
            GenDraw.DrawDiagonalStripes(TmpHostileCells, new Color(1.00f, 0.25f, 0.25f, 0.22f), altOffset: 0.0026f);
        }
    }


    // ============================================================
    // 【构建扫描单元格列表】
    // ============================================================
    private static List<IntVec3> BuildScanCells(Map map, List<IntVec3> groupCells, int maxRadius)
    {
        if (maxRadius < 0) maxRadius = 0;

        TmpScanSet.Clear();
        if (groupCells != null)
        {
            foreach (var center in groupCells)
            {
                // 简单的矩形扫描
                for (int dx = -maxRadius; dx <= maxRadius; dx++)
                {
                    for (int dz = -maxRadius; dz <= maxRadius; dz++)
                    {
                        IntVec3 cell = new IntVec3(center.x + dx, 0, center.z + dz);
                        if (cell.InBounds(map))
                        {
                            TmpScanSet.Add(cell);
                        }
                    }
                }
            }
        }

        return TmpScanSet.Count > 0 ? new List<IntVec3>(TmpScanSet) : null;
    }
}