namespace Universal_Lift_Structure;

public static class ULS_GhostRenderer
{
    private static readonly Color StoredGhostColor = new Color(0.9f, 0.35f, 0.9f, 0.35f);


    private static readonly Dictionary<int, Graphic> ghostGraphicsCache = new Dictionary<int, Graphic>();


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


    private static Graphic CreateGhostGraphic(Graphic baseGraphic, ThingDef thingDef, Color ghostCol, ThingDef stuff)
    {
        Shader ghostShader = ULS_ShaderTypeDefOf.ULS_GhostEdgeDotted.Shader;


        if (thingDef.IsDoor && !thingDef.building.isSupportDoor)
        {
            return GraphicDatabase.Get<Graphic_Single>(
                thingDef.uiIconPath,
                ghostShader,
                thingDef.graphicData.drawSize,
                ghostCol);
        }


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