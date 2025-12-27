namespace Universal_Lift_Structure;

/// 文件意图：升降裁剪渲染器。
/// 包含：逐格绘制、位移、Mesh/UV 裁剪缓存。
public static class ULS_LiftClipRenderer
{
    private const int VisibleSteps = 64;

    // 井壁/轨道背景：使用 4x4 Atlas（Basic Linked 规则），作为升降过程的“背景层”绘制。
    // 约束：
    // - 仅在升降中绘制（由调用方保证）
    // - 覆盖被收纳建筑 footprint（而非控制器 footprint）
    // - linkFlags 固定为 Custom4（与控制器一致）
    // - linkSet 优先使用控制器缓存的 LinkDirections（收纳瞬间快照），否则用 linkGrid + Custom4 重新计算
    private const string LiftShaftAtlasTexPath = "Things/Building/Linked/ULS_LiftShaft_Atlas";
    private const float LiftShaftBackgroundAltitudeOffset = 0.01f;
    private static Material liftShaftBaseMat;

    // 连接贴图：特定 linkMask 不做“从南侧裁切”的效果，仅整体上下移动，用于模拟垂直方向的连接式移动。
    private static bool ShouldSkipClipForLinkMask(int linkMask)
    {
        switch (linkMask & 0xF)
        {
            case 0:
            case 1:
            case 2:
            case 3:
            case 8:
            case 9:
            case 10:
            case 11:
                return true;
            default:
                return false;
        }
    }

    // key 设计：同一个 Graphic 在不同 rot 下可能返回不同 baseMesh；因此缓存必须包含 baseMesh 维度。
    // 使用 Mesh.GetInstanceID() + visibleStep + footprintAlongZ 组合，避免多格建筑因 mesh/UV 与 plane10 不一致而产生尺寸错误。
    private static readonly Dictionary<long, Mesh> meshCache = new Dictionary<long, Mesh>();

    /// 绘制升降中的被收纳建筑
    public static void DrawLiftingStoredBuilding(Building storedBuilding, Rot4 rot, IntVec3 rootCell, float progress01, Map map)
    {
        DrawLiftingStoredBuilding(storedBuilding, rot, rootCell, progress01, map, tryGetCachedLinkDirections: null);
    }

    /// 绘制升降中的被收纳建筑（可选：为 Linked 建筑提供缓存的 LinkDirections）
    public static void DrawLiftingStoredBuilding(
        Building storedBuilding,
        Rot4 rot,
        IntVec3 rootCell,
        float progress01,
        Map map,
        Func<IntVec3, LinkDirections?> tryGetCachedLinkDirections)
    {
        if (storedBuilding == null || storedBuilding.def == null)
        {
            return;
        }

        Graphic graphic = storedBuilding.Graphic;
        if (graphic == null)
        {
            return;
        }

        float clampedProgress = Mathf.Clamp01(progress01);
        float altitude = AltitudeLayer.Building.AltitudeFor();
        int visibleStep = Mathf.Clamp(Mathf.RoundToInt(clampedProgress * VisibleSteps), 0, VisibleSteps);

        // 背景井壁/轨道：始终先画在建筑背后。
        DrawLiftShaftBackground(rootCell, rot, storedBuilding.def.Size, altitude, map, tryGetCachedLinkDirections);

        if (graphic is Graphic_Linked linkedGraphic)
        {
            // Linked：逐格绘制时使用 SubGraphic 的 mesh/uv（通常是 1x1 单格），避免错误套用 plane10 导致尺寸/UV 不一致。
            Mesh baseMesh = linkedGraphic.SubGraphic.MeshAt(rot);
            // Linked 的每格都是 1x1，占格 Z 长度恒为 1。
            DrawLinkedGraphic(linkedGraphic, storedBuilding, rot, rootCell, altitude, baseMesh, visibleStep, map, tryGetCachedLinkDirections);
            return;
        }

        // 非 Linked：使用 graphic 自己的 MeshAt(rot)，让多格建筑的 drawSize/mesh 与原版绘制一致。
        // 注意：此处不再用 def.Size 作为 scale，否则会把“占格尺寸”当成“贴图绘制尺寸”。
        Mesh nonLinkedBaseMesh = graphic.MeshAt(rot);

        // 关键：裁剪“截断线”必须锚定在 footprint 的南侧边界，而不是 mesh bounds 的 minZ。
        // 多格 MeshAt(rot) 可能带有额外 padding（例如某些桌子：boundsZ > footprintZ），
        // 若用 boundsMinZ 作为锚点会导致截断线整体下移，产生“裁剪线不静止”的错觉。
        int sizeAlongZ = rot.IsHorizontal ? storedBuilding.def.Size.x : storedBuilding.def.Size.z;
        Mesh nonLinkedMesh = GetClippedMesh(nonLinkedBaseMesh, visibleStep, sizeAlongZ);
        DrawNonLinkedGraphic(graphic, storedBuilding, rot, rootCell, altitude, nonLinkedMesh);
    }

    /// 绘制升降背景井壁/轨道（逐格 Linked 4x4 Atlas）
    private static void DrawLiftShaftBackground(
        IntVec3 rootCell,
        Rot4 rot,
        IntVec2 footprintSize,
        float buildingAltitude,
        Map map,
        Func<IntVec3, LinkDirections?> tryGetCachedLinkDirections)
    {
        // 开发测试策略：贴图缺失时允许报错暴露问题；但 material 为空会导致后续 NRE，故在这里做一次显式提前返回。
        if (liftShaftBaseMat == null)
        {
            liftShaftBaseMat = MaterialPool.MatFrom(LiftShaftAtlasTexPath, ShaderDatabase.Cutout);
        }

        if (liftShaftBaseMat == null)
        {
            return;
        }

        float altitude = buildingAltitude - LiftShaftBackgroundAltitudeOffset;
        CellRect rect = GenAdj.OccupiedRect(rootCell, rot, footprintSize);
        foreach (IntVec3 cell in rect)
        {
            LinkDirections linkSet = GetLiftShaftLinkDirections(cell, map, rootCell, tryGetCachedLinkDirections);
            Material material = MaterialAtlasPool.SubMaterialFromAtlas(liftShaftBaseMat, linkSet);

            Vector3 center = cell.ToVector3Shifted();
            center.y = altitude;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(center, rot.AsQuat, Vector3.one), material, 0);
        }
    }

    /// 获取井壁/轨道的连接方向（优先缓存；否则按 Custom4 重算）
    private static LinkDirections GetLiftShaftLinkDirections(
        IntVec3 cell,
        Map map,
        IntVec3 parentPos,
        Func<IntVec3, LinkDirections?> tryGetCachedLinkDirections)
    {
        LinkDirections? cached = tryGetCachedLinkDirections != null ? tryGetCachedLinkDirections(cell) : null;
        if (cached.HasValue)
        {
            return cached.Value;
        }

        if (map == null)
        {
            return LinkDirections.None;
        }

        // 固定使用 Custom4，与控制器 linkFlags 一致。
        const LinkFlags linkFlags = LinkFlags.Custom4;

        int linkMask = 0;
        int bit = 1;
        for (int i = 0; i < 4; i++)
        {
            IntVec3 neighbor = cell + GenAdj.CardinalDirections[i];
            if (ShouldLinkWith(map, neighbor, linkFlags, parentPos))
            {
                linkMask += bit;
            }

            bit <<= 1;
        }

        return (LinkDirections)linkMask;
    }

    /// 绘制支持连接的 Graphic（逐格）
    private static void DrawLinkedGraphic(
        Graphic_Linked linkedGraphic,
        Building storedBuilding,
        Rot4 rot,
        IntVec3 rootCell,
        float baseAltitude,
        Mesh baseMesh,
        int visibleStep,
        Map map,
        Func<IntVec3, LinkDirections?> tryGetCachedLinkDirections)
    {
        CellRect rect = GenAdj.OccupiedRect(rootCell, rot, storedBuilding.def.Size);
        foreach (IntVec3 cell in rect)
        {
            LinkDirections? cached = tryGetCachedLinkDirections != null ? tryGetCachedLinkDirections(cell) : null;
            Material material = GetLinkedMaterial(linkedGraphic, storedBuilding, cell, map, rootCell, cached, out LinkDirections linkSet);

            int linkMask = (int)linkSet;
            bool clip = ShouldSkipClipForLinkMask(linkMask);
            Mesh mesh = GetLiftMesh(baseMesh, visibleStep, sizeAlongZ: 1, clip);

            Vector3 center = cell.ToVector3Shifted();
            center.y = baseAltitude;
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(center, rot.AsQuat, Vector3.one), material, 0);
        }
    }

    /// 绘制非连接 Graphic（整体）
    private static void DrawNonLinkedGraphic(Graphic graphic, Building storedBuilding, Rot4 rot, IntVec3 rootCell, float baseAltitude, Mesh mesh)
    {
        Material material = graphic.MatAt(rot, storedBuilding);
        Vector3 center = GenThing.TrueCenter(rootCell, rot, storedBuilding.def.Size, baseAltitude);
        Graphics.DrawMesh(mesh, Matrix4x4.TRS(center, rot.AsQuat, Vector3.one), material, 0);
    }

    /// 获取连接材质（忽略 Spawned 状态）
    private static Material GetLinkedMaterial(
        Graphic_Linked linkedGraphic,
        Building storedBuilding,
        IntVec3 cell,
        Map map,
        IntVec3 parentPos,
        LinkDirections? cachedLinkDirections,
        out LinkDirections linkSet)
    {
        if (cachedLinkDirections.HasValue)
        {
            linkSet = cachedLinkDirections.Value;
        }
        else if (map != null)
        {
            GraphicData graphicData = storedBuilding.def.graphicData;
            LinkFlags linkFlags = graphicData != null ? graphicData.linkFlags : LinkFlags.None;
            int linkMask = 0;
            int bit = 1;
            for (int i = 0; i < 4; i++)
            {
                IntVec3 neighbor = cell + GenAdj.CardinalDirections[i];
                if (ShouldLinkWith(map, neighbor, linkFlags, parentPos))
                {
                    linkMask += bit;
                }

                bit <<= 1;
            }

            linkSet = (LinkDirections)linkMask;
        }
        else
        {
            linkSet = LinkDirections.None;
        }

        Material baseMat = linkedGraphic.SubGraphic.MatSingleFor(storedBuilding);
        return MaterialAtlasPool.SubMaterialFromAtlas(baseMat, linkSet);
    }

    /// 判断是否需要连接（即使未 Spawned）
    private static bool ShouldLinkWith(Map map, IntVec3 cell, LinkFlags linkFlags, IntVec3 parentPos)
    {
        if (!cell.InBounds(map))
        {
            return (linkFlags & LinkFlags.MapEdge) != 0;
        }

        // Odyssey 子结构判定：对齐原版 Graphic_Linked.ShouldLinkWith
        if (ModsConfig.OdysseyActive &&
            ((map.terrainGrid.FoundationAt(cell)?.IsSubstructure ?? false) !=
             (map.terrainGrid.FoundationAt(parentPos)?.IsSubstructure ?? false)))
        {
            return false;
        }

        return (map.linkGrid.LinkFlagsAt(cell) & linkFlags) != 0;
    }

    /// 获取升降 Mesh（按进度缓存）
    private static Mesh GetLiftMesh(Mesh baseMesh, int visibleStep, int sizeAlongZ, bool clip)
    {
        if (baseMesh == null)
        {
            return null;
        }

        // 安全兜底：避免 0 或负数导致 anchor 异常。
        if (sizeAlongZ < 1)
        {
            sizeAlongZ = 1;
        }

        long key = MakeMeshCacheKey(baseMesh, visibleStep, sizeAlongZ, clip);
        if (!meshCache.TryGetValue(key, out var cachedMesh))
        {
            cachedMesh = clip
                ? CreateClippedMesh(baseMesh, visibleStep, sizeAlongZ)
                : CreateMovedMesh(baseMesh, visibleStep);
            meshCache.Add(key, cachedMesh);
        }

        return cachedMesh;
    }

    /// 兼容：保留原有接口，非 Linked 仍使用裁剪版本。
    private static Mesh GetClippedMesh(Mesh baseMesh, int visibleStep, int sizeAlongZ)
    {
        return GetLiftMesh(baseMesh, visibleStep, sizeAlongZ, clip: true);
    }

    private static long MakeMeshCacheKey(Mesh baseMesh, int visibleStep, int sizeAlongZ, bool clip)
    {
        // key = baseMeshInstanceId(32) + visibleStep(16) + sizeAlongZ(15) + clip(1)
        // - visibleStep 范围 0..VisibleSteps
        // - sizeAlongZ 通常 <= 11（建筑最大占格），但这里用 ushort 预留。
        unchecked
        {
            uint meshId = (uint)baseMesh.GetInstanceID();
            ushort packedSize = (ushort)(sizeAlongZ & 0x7FFF);
            if (clip)
            {
                packedSize |= 0x8000;
            }
            uint packed = ((uint)(ushort)visibleStep << 16) | packedSize;
            return ((long)meshId << 32) | packed;
        }
    }

    /// 仅整体位移（不裁剪顶点/不裁剪 UV）
    private static Mesh CreateMovedMesh(Mesh baseMesh, int visibleStep)
    {
        Vector3[] baseVertices = baseMesh.vertices;
        Vector3[] vertices = new Vector3[baseVertices.Length];

        float visible01 = Mathf.Clamp01(visibleStep / (float)VisibleSteps);

        // 与裁剪版本保持一致的位移曲线：visible01 越小，下沉越深。
        float offsetZ = -(1f - visible01);

        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 vertex = baseVertices[i];
            vertex.z += offsetZ;
            vertices[i] = vertex;
        }

        Mesh mesh = new Mesh
        {
            name = $"uls_lift_move_{visibleStep}",
            vertices = vertices,
            uv = baseMesh.uv,
            triangles = baseMesh.triangles,
            normals = baseMesh.normals,
            tangents = baseMesh.tangents,
            colors = baseMesh.colors
        };

        mesh.RecalculateBounds();
        return mesh;
    }

    /// 基于 plane10 裁剪上边界并截断 UV
    private static Mesh CreateClippedMesh(Mesh baseMesh, int visibleStep, int sizeAlongZ)
    {
        Vector3[] baseVertices = baseMesh.vertices;
        Vector3[] vertices = new Vector3[baseVertices.Length];

        float visible01 = Mathf.Clamp01(visibleStep / (float)VisibleSteps);

        // 截断线锚点：固定在“footprint 南侧边界”（local -Z）。
        // 不能用 mesh.bounds/minZ：多格 MeshAt(rot) 可能带 padding，导致截断线整体下移。
        float southAnchorZ = -sizeAlongZ * 0.5f;

        // 下沉距离：以“能完全沉入地面”为目标，用贴图（mesh bounds）计算。
        // 解释：在 visible01=0 时，希望整个 mesh 都在 southAnchorZ 之南（更小 z），从而全部被裁掉。
        // - baseMesh.bounds.max.z 表示 mesh 的最北侧（local +Z）。
        // - 要让最北侧也沉到 southAnchorZ 之下，需要 offsetZ <= southAnchorZ - baseMaxZ。
        // 因此需要的下沉距离 = baseMaxZ - southAnchorZ。
        Bounds baseBounds = baseMesh.bounds;


        // 整体下沉偏移：visible01 越小，下沉越深。
        float offsetZ = -(1f - visible01) ;

        // 用于 UV 归一化：以“下沉后的 mesh bounds”作为 0..1 的参考范围。
        // 注意：这里用 bounds 而不是顶点扫描，避免每个顶点循环再做一次 min/max。
        float shiftedMinZ = baseBounds.min.z + offsetZ;
        float shiftedMaxZ = baseBounds.max.z + offsetZ;
        float shiftedRangeZ = shiftedMaxZ - shiftedMinZ;
        if (shiftedRangeZ <= 1e-6f)
        {
            shiftedRangeZ = 1e-6f;
        }

        // 裁剪平面在“下沉后”的位置是固定 southAnchorZ（不随贴图 padding 漂移）。
        // 先整体下沉，再做硬裁剪：任何低于裁剪线的顶点直接夹到裁剪线。
        float clipV = Mathf.Clamp01((southAnchorZ - shiftedMinZ) / shiftedRangeZ);

        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 vertex = baseVertices[i];

            // 先整体下沉。
            float zShifted = vertex.z + offsetZ;

            // 再做硬裁剪：低于裁剪线的部分被切掉，顶点直接夹到裁剪线。
            if (zShifted < southAnchorZ)
            {
                zShifted = southAnchorZ;
            }

            vertex.z = zShifted;
            vertices[i] = vertex;
        }

        Vector2[] baseUv = baseMesh.uv;
        Vector2[] uv = new Vector2[baseUv.Length];
        for (int j = 0; j < baseUv.Length; j++)
        {
            Vector2 uvCoord = baseUv[j];

            // UV：与“硬裁剪”一致。
            // 目标效果：贴图整体下沉，经过裁剪线后被切掉；不再出现因 UV 窗口缩放导致的“全程扁”。
            // 约束：原版建筑贴图通常满足 uv.y 与 local z 近似线性对应（plane10/Graphic_Multi 典型）。
            // 做法：把 uv.y 当作 0..1 的“纵向进度”，在下沉后裁剪线处做夹紧。
            // - 未被裁剪区域保持原始采样（uv.y 不变）
            // - 被裁剪到裁剪线的顶点，其采样也夹到裁剪线对应的 v，避免纹理被拉伸。
            if (uvCoord.y < clipV)
            {
                uvCoord.y = clipV;
            }
            uv[j] = uvCoord;
        }

        Mesh mesh = new Mesh
        {
            name = $"uls_lift_clip_{visibleStep}",
            vertices = vertices,
            uv = uv,
            triangles = baseMesh.triangles,
            normals = baseMesh.normals,
            tangents = baseMesh.tangents,
            colors = baseMesh.colors
        };

        mesh.RecalculateBounds();
        return mesh;
    }
}