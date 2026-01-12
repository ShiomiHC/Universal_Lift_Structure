namespace Universal_Lift_Structure;

public static class ULS_LiftClipRenderer
{
    private const int VisibleSteps = 64;


    private const float LiftShaftBackgroundAltitudeOffset = 0.01f;


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


    private static readonly Dictionary<long, Mesh> meshCache = new Dictionary<long, Mesh>();


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

        float clampedProgress = Mathf.Clamp01(progress01);
        float altitude = AltitudeLayer.Building.AltitudeFor();
        int visibleStep = Mathf.Clamp(Mathf.RoundToInt(clampedProgress * VisibleSteps), 0, VisibleSteps);


        DrawLiftShaftBackground(rootCell, rot, storedBuilding.def.Size, altitude, map, tryGetCachedLinkDirections);

        if (graphic is Graphic_Linked linkedGraphic)
        {
            Mesh baseMesh = linkedGraphic.SubGraphic.MeshAt(rot);

            DrawLinkedGraphic(linkedGraphic, storedBuilding, rot, rootCell, altitude, baseMesh, visibleStep, map,
                tryGetCachedLinkDirections);
            return;
        }


        Mesh nonLinkedBaseMesh = graphic.MeshAt(rot);


        int sizeAlongZ = rot.IsHorizontal ? storedBuilding.def.Size.x : storedBuilding.def.Size.z;
        Mesh nonLinkedMesh = GetClippedMesh(nonLinkedBaseMesh, visibleStep, sizeAlongZ);
        DrawNonLinkedGraphic(graphic, storedBuilding, rot, rootCell, altitude, nonLinkedMesh);
    }


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

        float altitude = buildingAltitude - LiftShaftBackgroundAltitudeOffset;
        CellRect rect = GenAdj.OccupiedRect(rootCell, rot, footprintSize);
        foreach (IntVec3 cell in rect)
        {
            LinkDirections linkSet = GetLiftShaftLinkDirections(cell, map, rootCell, tryGetCachedLinkDirections);
            Material material = MaterialAtlasPool.SubMaterialFromAtlas(ULS_Materials.LiftShaftBase, linkSet);

            Vector3 center = cell.ToVector3Shifted();
            center.y = altitude;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(center, Quaternion.identity, Vector3.one), material, 0);
        }
    }


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
            LinkDirections? cached = tryGetCachedLinkDirections?.Invoke(cell);
            Material material = GetLinkedMaterial(linkedGraphic, storedBuilding, cell, map, rootCell, cached,
                out LinkDirections linkSet);

            int linkMask = (int)linkSet;
            bool clip = ShouldSkipClipForLinkMask(linkMask);
            Mesh mesh = GetLiftMesh(baseMesh, visibleStep, sizeAlongZ: 1, clip);

            Vector3 center = cell.ToVector3Shifted();
            center.y = baseAltitude;
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(center, Quaternion.identity, Vector3.one), material, 0);
        }
    }


    private static void DrawNonLinkedGraphic(Graphic graphic, Building storedBuilding, Rot4 rot, IntVec3 rootCell,
        float baseAltitude, Mesh mesh)
    {
        Material material = graphic.MatAt(rot, storedBuilding);
        Vector3 center = GenThing.TrueCenter(rootCell, rot, storedBuilding.def.Size, baseAltitude);

        Quaternion rotation = graphic.ShouldDrawRotated ? rot.AsQuat : Quaternion.identity;
        Graphics.DrawMesh(mesh, Matrix4x4.TRS(center, rotation, Vector3.one), material, 0);
    }


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


    private static bool ShouldLinkWith(Map map, IntVec3 cell, LinkFlags linkFlags, IntVec3 parentPos)
    {
        if (!cell.InBounds(map))
        {
            return (linkFlags & LinkFlags.MapEdge) != 0;
        }


        if (ModsConfig.OdysseyActive &&
            ((map.terrainGrid.FoundationAt(cell)?.IsSubstructure ?? false) !=
             (map.terrainGrid.FoundationAt(parentPos)?.IsSubstructure ?? false)))
        {
            return false;
        }

        return (map.linkGrid.LinkFlagsAt(cell) & linkFlags) != 0;
    }


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


    private static Mesh GetClippedMesh(Mesh baseMesh, int visibleStep, int sizeAlongZ)
    {
        return GetLiftMesh(baseMesh, visibleStep, sizeAlongZ, clip: true);
    }

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


    private static Mesh CreateMovedMesh(Mesh baseMesh, int visibleStep)
    {
        Vector3[] baseVertices = baseMesh.vertices;
        Vector3[] vertices = new Vector3[baseVertices.Length];

        float visible01 = Mathf.Clamp01(visibleStep / (float)VisibleSteps);


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


    private static Mesh CreateClippedMesh(Mesh baseMesh, int visibleStep, int sizeAlongZ)
    {
        Vector3[] baseVertices = baseMesh.vertices;
        Vector3[] vertices = new Vector3[baseVertices.Length];

        float visible01 = Mathf.Clamp01(visibleStep / (float)VisibleSteps);


        float southAnchorZ = -sizeAlongZ * 0.5f;


        Bounds baseBounds = baseMesh.bounds;


        float offsetZ = -(1f - visible01);


        float shiftedMinZ = baseBounds.min.z + offsetZ;
        float shiftedMaxZ = baseBounds.max.z + offsetZ;
        float shiftedRangeZ = shiftedMaxZ - shiftedMinZ;
        if (shiftedRangeZ <= 1e-6f)
        {
            shiftedRangeZ = 1e-6f;
        }


        float clipV = Mathf.Clamp01((southAnchorZ - shiftedMinZ) / shiftedRangeZ);

        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 vertex = baseVertices[i];


            float zShifted = vertex.z + offsetZ;


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