namespace Universal_Lift_Structure;

/// 文件意图：虚影渲染静态工具类。
/// 包含：存储建筑虚影渲染、Graphic 缓存管理。
/// 与 Building_WallController 解耦，可独立使用。
public static class ULS_GhostRenderer
{
    // ==================== 常量与缓存 ====================

    /// 虚影颜色：紫红色半透明
    private static readonly Color StoredGhostColor = new Color(0.9f, 0.35f, 0.9f, 0.35f);

    /// 虚影 Graphic 缓存（key = 组合哈希）
    private static Dictionary<int, Graphic> ghostGraphicsCache = new Dictionary<int, Graphic>();

    // ==================== 公共方法 ====================

    /// 绘制存储建筑的虚影
    /// <param name="cell">绘制位置</param>
    /// <param name="rot">旋转</param>
    /// <param name="def">建筑定义</param>
    /// <param name="stuff">材料（可选）</param>
    public static void DrawStoredBuildingGhost(IntVec3 cell, Rot4 rot, ThingDef def, ThingDef stuff)
    {
        if (def == null)
        {
            return;
        }

        Graphic ghostGraphic = GetGhostGraphicFor(def.graphic, def, StoredGhostColor, stuff);
        ghostGraphic.DrawFromDef(
            GenThing.TrueCenter(cell, rot, def.Size, AltitudeLayer.Blueprint.AltitudeFor()),
            rot,
            def);
    }

    /// 获取虚影 Graphic（带缓存）
    /// <param name="baseGraphic">基础 Graphic</param>
    /// <param name="thingDef">物品定义</param>
    /// <param name="ghostCol">虚影颜色</param>
    /// <param name="stuff">材料（可选）</param>
    public static Graphic GetGhostGraphicFor(Graphic baseGraphic, ThingDef thingDef, Color ghostCol, ThingDef stuff = null)
    {
        // 计算缓存 Key
        int key = Gen.HashCombine(
            Gen.HashCombineStruct(
                Gen.HashCombine(
                    Gen.HashCombine(0, baseGraphic),
                    thingDef),
                ghostCol),
            stuff);

        // 尝试从缓存获取
        if (!ghostGraphicsCache.TryGetValue(key, out var cachedGraphic))
        {
            // 创建新虚影 Graphic
            cachedGraphic = CreateGhostGraphic(baseGraphic, thingDef, ghostCol, stuff);
            ghostGraphicsCache.Add(key, cachedGraphic);
        }

        return cachedGraphic;
    }

    // ==================== 内部方法 ====================

    /// 创建虚影 Graphic
    private static Graphic CreateGhostGraphic(Graphic baseGraphic, ThingDef thingDef, Color ghostCol, ThingDef stuff)
    {
        Shader ghostShader = ULS_ShaderTypeDefOf.ULS_GhostEdgeDotted.Shader;

        // 特殊处理：门（非支撑门）
        if (thingDef.IsDoor && !thingDef.building.isSupportDoor)
        {
            return GraphicDatabase.Get<Graphic_Single>(
                thingDef.uiIconPath,
                ghostShader,
                thingDef.graphicData.drawSize,
                ghostCol);
        }

        // 使用蓝图 Graphic（如果设置）
        if (thingDef.useBlueprintGraphicAsGhost)
        {
            baseGraphic = thingDef.blueprintDef.graphic;
        }
        else if (baseGraphic == null)
        {
            baseGraphic = thingDef.graphic;
        }

        // 复制 GraphicData（移除阴影）
        GraphicData graphicData = null;
        if (baseGraphic.data != null)
        {
            graphicData = new GraphicData();
            graphicData.CopyFrom(baseGraphic.data);
            graphicData.shadowData = null;
        }

        string path = baseGraphic.path;

        // 处理 Linked Graphic
        if (thingDef.graphicData.Linked)
        {
            if (baseGraphic is Graphic_Linked linkedGraphic)
            {
                Graphic subGhostGraphic = GetGhostGraphicFor(linkedGraphic.SubGraphic, thingDef, ghostCol, stuff);
                return GraphicUtility.WrapLinked(subGhostGraphic, linkedGraphic.LinkerType);
            }

            return GraphicDatabase.Get(
                baseGraphic.GetType(),
                path,
                ghostShader,
                baseGraphic.drawSize,
                ghostCol,
                Color.white,
                graphicData,
                null);
        }

        // 处理 Appearances Graphic（材料变体）
        if (baseGraphic is Graphic_Appearances appearancesGraphic && stuff != null)
        {
            return GraphicDatabase.Get<Graphic_Single>(
                appearancesGraphic.SubGraphicFor(stuff).path,
                ghostShader,
                thingDef.graphicData.drawSize,
                ghostCol,
                Color.white,
                graphicData);
        }

        // 默认处理
        return GraphicDatabase.Get(
            baseGraphic.GetType(),
            path,
            ghostShader,
            baseGraphic.drawSize,
            ghostCol,
            Color.white,
            graphicData,
            null);
    }
}
