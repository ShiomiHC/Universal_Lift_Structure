namespace Universal_Lift_Structure;

// 核心渲染器：负责绘制正在升降过程中的建筑物
// 
// 技术原理：
// 普通的移动动画只是改变物体的位置 (Transform)，这会导致物体穿模或浮空。
// 这个渲染器通过动态修改网格 (Mesh) 的顶点，实现"被地面遮挡"或"从地下升起"的视觉效果。
// 
// 核心机制：
// 1. 裁剪 (Clipping): 将低于地面的顶点"压平"或裁剪掉，模拟物体缩入地下的效果。
// 2. 缓存 (Caching): 生成的裁剪网格会被缓存，以避免每帧重建 Mesh 带来的性能开销。
// 3. 量化 (Quantization): 升降进度被量化为 64 步 (VisibleSteps)，确保有限的缓存能覆盖所有状态。
public static class ULS_LiftClipRenderer
{
    // 将升降过程分为 64 帧（步），用于网格缓存
    // 步数越高动画越平滑，但内存占用和首次生成的计算量越大
    private const int VisibleSteps = 64;

    // 升降井背景（黑色遮罩/坑底）的高度偏移，防止 Z-fighting
    private const float LiftShaftBackgroundAltitudeOffset = 0.01f;

    // 判断某些连接方向（LinkMask）是否应该使用裁剪模式
    private static bool ShouldUseClippedMeshFor(int linkMask)
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

    // 网格缓存：Key 是由 MeshID 和 进度步数 组合而成的哈希值
    private static readonly Dictionary<long, Mesh> meshCache = new Dictionary<long, Mesh>();

    // [入口方法] 绘制正在升降的存储建筑
    // storedBuilding: 被存储（隐藏）的建筑物实例
    // rot: 建筑的旋转角度
    // rootCell: 建筑的中心/左下角坐标
    // progress01: 当前升降进度 (0.0 = 完全降下/不可见, 1.0 = 完全升起/正常)
    // map: 当前地图
    // tryGetCachedLinkDirections: 可选的连接方向缓存回调，优化性能
    public static void DrawLiftingStoredBuilding(
        Building storedBuilding,
        Rot4 rot,
        IntVec3 rootCell,
        float progress01,
        Map map,
        Func<IntVec3, LinkDirections?> tryGetCachedLinkDirections = null)
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

        // 1. 计算当前动画状态
        float clampedProgress = Mathf.Clamp01(progress01);
        float altitude = AltitudeLayer.Building.AltitudeFor(); // 建筑的标准渲染层级

        // 将连续的进度值量化为离散的步数索引 (0 ~ 64)
        int visibleStep = Mathf.Clamp(Mathf.RoundToInt(clampedProgress * VisibleSteps), 0, VisibleSteps);

        // 2. 绘制升降井背景 (坑底黑色部分)
        // 这给玩家一种"下面有个洞"的视觉暗示
        DrawLiftShaftBackground(rootCell, rot, storedBuilding.def.Size, altitude, map, tryGetCachedLinkDirections);

        // 3. 绘制建筑本体
        // 分为"连接纹理"（墙、沙袋等 Graphic_Linked）和"普通纹理"（工作台、炮塔等）两种处理路径
        if (graphic is Graphic_Linked linkedGraphic)
        {
            Mesh baseMesh = linkedGraphic.SubGraphic.MeshAt(rot); // 获取基础网格

            DrawLinkedGraphic(linkedGraphic, storedBuilding, rot, rootCell, altitude, baseMesh, visibleStep, map,
                tryGetCachedLinkDirections);
            return;
        }

        // 处理普通图形
        Mesh nonLinkedBaseMesh = graphic.MeshAt(rot);

        // 确定用于裁剪计算的 Z 轴尺寸（考虑旋转后在 Z 方向的长度）
        int sizeAlongZ = rot.IsHorizontal ? storedBuilding.def.Size.x : storedBuilding.def.Size.z;

        // 获取处理后的网格（可能从缓存中取，也可能新生成）
        // 普通建筑默认都使用裁剪模式 (true)
        Mesh nonLinkedMesh = GetClippedMesh(nonLinkedBaseMesh, visibleStep, sizeAlongZ);

        // 执行绘制指令
        DrawNonLinkedGraphic(graphic, storedBuilding, rot, rootCell, altitude, nonLinkedMesh);
    }

    // 绘制升降井的黑色背景
    private static void DrawLiftShaftBackground(
        IntVec3 rootCell,
        Rot4 rot,
        IntVec2 footprintSize,
        float buildingAltitude,
        Map map,
        Func<IntVec3, LinkDirections?> tryGetCachedLinkDirections)
    {
        if (ULS_Materials.LiftShaftBase == null)
        {
            return;
        }

        // 稍微降低高度，放在建筑下方
        float altitude = buildingAltitude - LiftShaftBackgroundAltitudeOffset;
        CellRect rect = GenAdj.OccupiedRect(rootCell, rot, footprintSize);

        // 遍历建筑占据的每一格，绘制井道背景
        foreach (IntVec3 cell in rect)
        {
            // 计算连接状态，确保井道背景也能和周围的墙体/地面正确衔接
            LinkDirections linkSet = GetLiftShaftLinkDirections(cell, map, rootCell, tryGetCachedLinkDirections);
            Material material = MaterialAtlasPool.SubMaterialFromAtlas(ULS_Materials.LiftShaftBase, linkSet);

            Vector3 center = cell.ToVector3Shifted();
            center.y = altitude;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(center, Quaternion.identity, Vector3.one), material, 0);
        }
    }

    // 计算井道背景的连接方向
    private static LinkDirections GetLiftShaftLinkDirections(
        IntVec3 cell,
        Map map,
        IntVec3 parentPos,
        Func<IntVec3, LinkDirections?> tryGetCachedLinkDirections)
    {
        LinkDirections? cached = tryGetCachedLinkDirections?.Invoke(cell);
        if (cached.HasValue)
        {
            return cached.Value;
        }

        if (map == null)
        {
            return LinkDirections.None;
        }

        // 定义连接规则
        const LinkFlags linkFlags = LinkFlags.Custom4;

        int linkMask = 0;
        int bit = 1;
        // 检查四周邻居，计算连接掩码
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

    // 绘制具有连接属性的建筑（如墙壁）
    // 需要对每一格分别处理，因为每一格的连接状态可能不同
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
            // 获取材质（考虑连接状态）
            LinkDirections? cached = tryGetCachedLinkDirections?.Invoke(cell);
            Material material = GetLinkedMaterial(linkedGraphic, storedBuilding, cell, map, rootCell, cached,
                out LinkDirections linkSet);

            int linkMask = (int)linkSet;

            // 某些特殊的连接形状判定是否需要裁剪
            bool clip = ShouldUseClippedMeshFor(linkMask);

            // 获取单格 (1x1) 的网格，根据 clip 决定是否裁剪
            Mesh mesh = GetLiftMesh(baseMesh, visibleStep, sizeAlongZ: 1, clip);

            Vector3 center = cell.ToVector3Shifted();
            center.y = baseAltitude;
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(center, Quaternion.identity, Vector3.one), material, 0);
        }
    }

    // 绘制普通（非连接）建筑
    // 通常是一个整体 Mesh，不需要分格绘制
    private static void DrawNonLinkedGraphic(Graphic graphic, Building storedBuilding, Rot4 rot, IntVec3 rootCell,
        float baseAltitude, Mesh mesh)
    {
        Material material = graphic.MatAt(rot, storedBuilding);
        Vector3 center = GenThing.TrueCenter(rootCell, rot, storedBuilding.def.Size, baseAltitude);

        Quaternion rotation = graphic.ShouldDrawRotated ? rot.AsQuat : Quaternion.identity;
        Graphics.DrawMesh(mesh, Matrix4x4.TRS(center, rotation, Vector3.one), material, 0);
    }

    // 获取连接材质的辅助方法
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
            LinkFlags linkFlags = graphicData?.linkFlags ?? LinkFlags.None;
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

    // 判断两格是否应该连接
    private static bool ShouldLinkWith(Map map, IntVec3 cell, LinkFlags linkFlags, IntVec3 parentPos)
    {
        if (!cell.InBounds(map))
        {
            return (linkFlags & LinkFlags.MapEdge) != 0;
        }

        // Odyssey Mod 兼容性：检查地基 (Foundation) 是否一致
        if (ModsConfig.OdysseyActive &&
            ((map.terrainGrid.FoundationAt(cell)?.IsSubstructure ?? false) !=
             (map.terrainGrid.FoundationAt(parentPos)?.IsSubstructure ?? false)))
        {
            return false;
        }

        return (map.linkGrid.LinkFlagsAt(cell) & linkFlags) != 0;
    }

    // 获取（缓存的）修改后网格
    // baseMesh: 原始模型
    // visibleStep: 当前进度步骤
    // sizeAlongZ: Z轴物体长度（用于计算裁剪平面的起始位置）
    // clip: 是进行裁剪（CreateClippedMesh）还是仅仅向下平移（CreateMovedMesh）
    private static Mesh GetLiftMesh(Mesh baseMesh, int visibleStep, int sizeAlongZ, bool clip)
    {
        if (baseMesh == null)
        {
            return null;
        }

        if (sizeAlongZ < 1)
        {
            sizeAlongZ = 1;
        }

        // 构建缓存 Key
        long key = MakeMeshCacheKey(baseMesh, visibleStep, sizeAlongZ, clip);
        if (!meshCache.TryGetValue(key, out var cachedMesh))
        {
            // 缓存未命中，创建新网格
            cachedMesh = clip
                ? CreateClippedMesh(baseMesh, visibleStep, sizeAlongZ) // 裁剪模式
                : CreateMovedMesh(baseMesh, visibleStep); // 平移模式
            meshCache.Add(key, cachedMesh);
        }

        return cachedMesh;
    }

    // 获取裁剪网格的便捷方法
    private static Mesh GetClippedMesh(Mesh baseMesh, int visibleStep, int sizeAlongZ)
    {
        return GetLiftMesh(baseMesh, visibleStep, sizeAlongZ, clip: true);
    }

    // 生成唯一的缓存 Key
    // 组合了 Mesh ID、进度步数、尺寸和模式
    private static long MakeMeshCacheKey(Mesh baseMesh, int visibleStep, int sizeAlongZ, bool clip)
    {
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

    // 创建"平移"网格
    // 简单地降低所有顶点的 Z 轴坐标 (在顶视图中看起来即"向下移动")
    // 用于那些不适合使用 squashing 裁剪的连接形状
    private static Mesh CreateMovedMesh(Mesh baseMesh, int visibleStep)
    {
        Vector3[] baseVertices = baseMesh.vertices;
        Vector3[] vertices = new Vector3[baseVertices.Length];

        float visible01 = Mathf.Clamp01(visibleStep / (float)VisibleSteps);

        // 计算 Z 轴偏移量
        // visible01 = 1 (完全升起) -> offsetZ = 0
        // visible01 = 0 (完全降下) -> offsetZ = -1 (移出视野 1 格距离)
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

    // 创建"裁剪"网格
    // 核心算法：它不仅移动顶点，还会把超出下界（southAnchorZ）的顶点"压"在边界上
    // 并配合 UV 裁剪，防止贴图拉伸，从而产生"慢慢缩入地下"的效果
    private static Mesh CreateClippedMesh(Mesh baseMesh, int visibleStep, int sizeAlongZ)
    {
        Vector3[] baseVertices = baseMesh.vertices;
        Vector3[] vertices = new Vector3[baseVertices.Length];

        float visible01 = Mathf.Clamp01(visibleStep / (float)VisibleSteps);

        // 确定裁剪底线：物体最南端（视觉最下方）
        float southAnchorZ = -sizeAlongZ * 0.5f;

        Bounds baseBounds = baseMesh.bounds;

        // 计算整体下移偏移量
        float offsetZ = -(1f - visible01);

        // 计算移动后包围盒的 Z 范围
        float shiftedMinZ = baseBounds.min.z + offsetZ;
        float shiftedMaxZ = baseBounds.max.z + offsetZ;
        float shiftedRangeZ = shiftedMaxZ - shiftedMinZ;
        if (shiftedRangeZ <= 1e-6f)
        {
            shiftedRangeZ = 1e-6f;
        }

        // 计算裁剪比率 (Clip V)，用于 UV 映射
        // 这决定了纹理的哪一部分应该被显示，哪一部分应该被切掉
        float clipV = Mathf.Clamp01((southAnchorZ - shiftedMinZ) / shiftedRangeZ);

        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 vertex = baseVertices[i];

            // 先进行平移
            float zShifted = vertex.z + offsetZ;

            // 如果顶点低于南端锚点（即"进入地下"的部分）
            // 将其强制拉回到锚点位置
            // 这样做的效果是：物体下半部分被"压扁"在一条线上，视觉上看起来像是被地面遮挡了
            if (zShifted < southAnchorZ)
            {
                zShifted = southAnchorZ;
            }

            vertex.z = zShifted;
            vertices[i] = vertex;
        }

        // UV 修正
        // 对于被"压扁"的顶点，我们需要修正它们的 UV 坐标，否则纹理会堆叠在一起看起来很怪
        // 或者说，我们只想显示"未被上方遮挡"的那部分纹理
        Vector2[] baseUv = baseMesh.uv;
        Vector2[] uv = new Vector2[baseUv.Length];
        for (int j = 0; j < baseUv.Length; j++)
        {
            Vector2 uvCoord = baseUv[j];

            // 简单的 UV 裁剪逻辑：如果 UV 的 V坐标 (y) 低于裁剪线，就将其钳制
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