namespace Universal_Lift_Structure;

/// 文件意图：方案 B（原版选择/覆盖层系统）——在 `SelectionDrawer.DrawSelectionOverlays` 末尾追加“控制器所在格子的整格填充高亮”。
/// - 仅覆盖殖民者控制器（`allBuildingsColonist`）。
/// - 由右下角 Overlay Toggle（`ShowControllerCell`）控制显示（当前语义为“填充”，保留原开关名）。
[HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionOverlays))]
public static class Patch_SelectionDrawer_DrawSelectionOverlays
{
    private static readonly List<IntVec3> TmpFillCells = new();

    private static readonly List<Matrix4x4> TmpFillMatrices = new();

    private static readonly List<int> TmpGroupIds = new();
    private static readonly List<IntVec3> TmpFriendlyCells = new();
    private static readonly List<IntVec3> TmpHostileCells = new();
    private static readonly List<IntVec3> TmpNeutralCells = new();

    private sealed class ScanCache
    {
        public int membershipHash;
        public int maxRadius;
        public List<IntVec3> scanCells;
    }

    private static readonly Dictionary<int, ScanCache> ScanCacheByGroupId = new();
    private static readonly HashSet<IntVec3> TmpScanSet = new();

    public static void Postfix()
    {
        if (Current.ProgramState is not ProgramState.Playing)
        {
            return;
        }

        if (Find.ScreenshotModeHandler.Active)
        {
            return;
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        if (settings is null)
        {
            return;
        }

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

        if (showFill)
        {
            TmpFillCells.Clear();
            for (int i = 0; i < colonistBuildings.Count; i++)
            {
                if (colonistBuildings[i] is not Building_WallController controller)
                {
                    continue;
                }

                TmpFillCells.Add(controller.Position);
            }

            if (TmpFillCells.Count > 0)
            {
                // 整格填充：临时调试开关，优先“立刻看见效果”。
                // altOffset：固定值，避免与其他 overlay/地面发生 z-fighting。
                const float altOffset = 0.001f;
                Color fillColor = new Color(0.98f, 0.97f, 0.96f, 0.35f);

                // 纯色材质：用白图 + Transparent shader 以支持 alpha。
                Material fillMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.Transparent, fillColor, renderQueue: 2900);
                fillMat.enableInstancing = true;

                TmpFillMatrices.Clear();
                for (int i = 0; i < TmpFillCells.Count; i++)
                {
                    IntVec3 cell = TmpFillCells[i];
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

        if (!showAutoProjection)
        {
            return;
        }

        // 自动控制器检测区域投影：按组读取 maxRadius + 当前过滤器类型，并生成与检测一致的 scanCells（方形并集）。
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

        for (int i = 0; i < TmpGroupIds.Count; i++)
        {
            int groupId = TmpGroupIds[i];
            if (groupId < 1)
            {
                continue;
            }

            if (!groupComp.TryGetGroupControllerCells(groupId, out List<IntVec3> groupCells) || groupCells is not { Count: > 0 })
            {
                continue;
            }

            // 找到一个代表控制器（用于读取自动组 marker props）。
            Building_WallController representative = null;
            for (int j = 0; j < groupCells.Count; j++)
            {
                if (ULS_Utility.TryGetControllerAt(map, groupCells[j], out Building_WallController c) && c != null)
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

            // scanCells 缓存：仅在成员/半径变化时重建，避免每帧 HashSet 分配与枚举。
            int membershipHash = ComputeMembershipHash(groupCells);
            if (!ScanCacheByGroupId.TryGetValue(groupId, out ScanCache cache) || cache == null)
            {
                cache = new ScanCache();
                ScanCacheByGroupId[groupId] = cache;
            }

            if (cache.scanCells == null || cache.scanCells.Count == 0 || cache.membershipHash != membershipHash || cache.maxRadius != maxRadius)
            {
                cache.membershipHash = membershipHash;
                cache.maxRadius = maxRadius;
                cache.scanCells = BuildScanCells(map, groupCells, maxRadius);
            }

            if (cache.scanCells is not { Count: > 0 })
            {
                continue;
            }

            switch (filterType)
            {
                case ULS_AutoGroupType.Friendly:
                    TmpFriendlyCells.AddRange(cache.scanCells);
                    break;
                case ULS_AutoGroupType.Hostile:
                    TmpHostileCells.AddRange(cache.scanCells);
                    break;
                case ULS_AutoGroupType.Neutral:
                    TmpNeutralCells.AddRange(cache.scanCells);
                    break;
            }
        }

        // 颜色语义：绿=友方，黄=中立，红=敌方。
        // altOffset：为三种颜色提供不同固定高度，避免叠加时 Z-fight。
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

    private static int ComputeMembershipHash(List<IntVec3> cells)
    {
        unchecked
        {
            int h = 17;
            if (cells != null)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    IntVec3 c = cells[i];
                    h = h * 31 + c.x;
                    h = h * 31 + c.z;
                }
            }

            return h;
        }
    }

    private static List<IntVec3> BuildScanCells(Map map, List<IntVec3> groupCells, int maxRadius)
    {
        if (maxRadius < 0) maxRadius = 0;

        TmpScanSet.Clear();
        if (groupCells != null)
        {
            for (int i = 0; i < groupCells.Count; i++)
            {
                IntVec3 center = groupCells[i];
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
