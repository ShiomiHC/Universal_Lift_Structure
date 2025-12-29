namespace Universal_Lift_Structure;

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
                Color fillColor = new Color(0.98f, 0.97f, 0.96f, 0.35f);


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


            int membershipHash = ComputeMembershipHash(groupCells);
            if (!ScanCacheByGroupId.TryGetValue(groupId, out ScanCache cache) || cache == null)
            {
                cache = new ScanCache();
                ScanCacheByGroupId[groupId] = cache;
            }

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

            var targetList = filterType switch
            {
                ULS_AutoGroupType.Friendly => TmpFriendlyCells,
                ULS_AutoGroupType.Hostile => TmpHostileCells,
                ULS_AutoGroupType.Neutral => TmpNeutralCells,
                _ => null
            };
            targetList?.AddRange(cache.scanCells);
        }


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
                foreach (var c in cells)
                {
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
            foreach (var center in groupCells)
            {
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