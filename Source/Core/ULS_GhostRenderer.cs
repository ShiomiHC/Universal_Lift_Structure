namespace Universal_Lift_Structure;

public static class ULS_GhostRenderer
{
    // 虚影颜色：紫色半透明
    private static readonly Color StoredGhostColor = new Color(0.9f, 0.35f, 0.9f, 0.35f);


    // 虚影图形缓存，避免重复创建
    private static readonly Dictionary<int, Graphic> ghostGraphicsCache = new Dictionary<int, Graphic>();


    // ============================================================
    // 【绘制存储物体虚影】
    // ============================================================
    // 绘制存储物体的虚影
    //
    // 【参数说明】
    // - cell: 绘制位置
    // - rot: 旋转方向
    // - def: 物体定义
    // - stuff: 材质属性
    // ============================================================
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


    // ============================================================
    // 【获取虚影图形】
    // ============================================================
    // 获取或创建缓存的虚影图形
    //
    // 【参数说明】
    // - baseGraphic: 基础图形
    // - thingDef: 物体定义
    // - ghostCol: 虚影颜色
    // - stuff: 材质
    //
    // 【返回值】
    // - 缓存的虚影图形
    // ============================================================
    private static Graphic GetGhostGraphicFor(Graphic baseGraphic, ThingDef thingDef, Color ghostCol,
        ThingDef stuff = null)
    {
        int key = Gen.HashCombine(
            Gen.HashCombineStruct(
                Gen.HashCombine(
                    Gen.HashCombine(0, baseGraphic),
                    thingDef),
                ghostCol),
            stuff);


        if (!ghostGraphicsCache.TryGetValue(key, out var cachedGraphic))
        {
            cachedGraphic = CreateGhostGraphic(baseGraphic, thingDef, ghostCol, stuff);
            ghostGraphicsCache.Add(key, cachedGraphic);
        }

        return cachedGraphic;
    }


    // ============================================================
    // 【创建虚影图形】
    // ============================================================
    // 创建新的虚影图形（内部实现）
    //
    // 【逻辑分支】
    // 1. 门：特殊处理
    // 2. 蓝图图形：优先使用
    // 3. 链接图形：递归处理
    // 4. 外观变化图形：提取当前子图形
    // 5. 默认：直接应用 Shader
    //
    // 【参数说明】
    // - baseGraphic: 基础图形
    // - thingDef: 物体定义
    // - ghostCol: 虚影颜色
    // - stuff: 材质
    //
    // 【返回值】
    // - 新创建的虚影图形
    // ============================================================
    private static Graphic CreateGhostGraphic(Graphic baseGraphic, ThingDef thingDef, Color ghostCol, ThingDef stuff)
    {
        Shader ghostShader = ULS_ShaderTypeDefOf.ULS_GhostEdgeDotted.Shader; // 使用自定义的点状边缘虚影Shader


        // 特殊处理：门
        if (thingDef.IsDoor && !thingDef.building.isSupportDoor)
        {
            return GraphicDatabase.Get<Graphic_Single>(
                thingDef.uiIconPath,
                ghostShader,
                thingDef.graphicData.drawSize,
                ghostCol);
        }


        // 优先使用蓝图图形作为虚影，如果没有则使用原图形
        if (thingDef.useBlueprintGraphicAsGhost)
        {
            baseGraphic = thingDef.blueprintDef.graphic;
        }
        else if (baseGraphic == null)
        {
            baseGraphic = thingDef.graphic;
        }


        GraphicData graphicData = null;
        if (baseGraphic.data != null)
        {
            graphicData = new GraphicData();
            graphicData.CopyFrom(baseGraphic.data);
            graphicData.shadowData = null;
        }

        string path = baseGraphic.path;


        // 处理链接图形 (Graphic_Linked)
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


        // 处理外观变化图形 (Graphic_Appearances)
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