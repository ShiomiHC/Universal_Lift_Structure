namespace Universal_Lift_Structure;

public partial class Building_WallController : Building, IThingHolder
{
    private ThingOwner<Thing> innerContainer;


    private Rot4 storedRotation = Rot4.North;
    private IntVec3 storedCell = IntVec3.Invalid;


    private List<IntVec3> storedLinkMaskCells;
    private List<byte> storedLinkMaskValues;


    private float storedThingMarketValueIgnoreHp;

    // --- Lift Action State ---
    private bool liftActionPending;
    private bool liftActionIsRaise;
    private IntVec3 liftActionStartCell = IntVec3.Invalid;

    public bool LiftActionPending => liftActionPending;

    public void Notify_FlickedBy(Pawn pawn)
    {
        if (!liftActionPending)
        {
            return;
        }

        // Execute valid action
        if (liftActionIsRaise)
        {
            TryRaiseGroup(showMessage: true);
        }
        else
        {
            TryLowerGroup(liftActionStartCell, showMessage: true);
        }

        liftActionPending = false;
        liftActionStartCell = IntVec3.Invalid;
    }

    public void QueueLiftAction(bool isRaise, IntVec3 lowerStartCell)
    {
        liftActionPending = true;
        liftActionIsRaise = isRaise;
        liftActionStartCell = lowerStartCell;

        if (Map.designationManager.DesignationOn(this, ULS_DesignationDefOf.ULS_FlickLiftStructure) == null)
        {
            Map.designationManager.AddDesignation(new Designation(this, ULS_DesignationDefOf.ULS_FlickLiftStructure));
        }
    }


    private IntVec3 multiCellGroupRootCell = IntVec3.Invalid;


    private int controllerGroupId;


    internal IntVec3 MultiCellGroupRootCell
    {
        get => multiCellGroupRootCell;
        set => multiCellGroupRootCell = value;
    }


    internal int ControllerGroupId
    {
        get => controllerGroupId;
        set => controllerGroupId = value;
    }


    private Thing StoredThing
    {
        get
        {
            if (innerContainer == null || innerContainer.Count == 0)
            {
                return null;
            }

            return innerContainer[0];
        }
    }


    public bool HasStored
    {
        get
        {
            ThingOwner<Thing> container = innerContainer;
            if (container != null)
            {
                return container.Count > 0;
            }

            return false;
        }
    }


    internal float StoredThingMarketValueIgnoreHp => storedThingMarketValueIgnoreHp;


    public override void PostMake()
    {
        base.PostMake();
        innerContainer ??= new ThingOwner<Thing>(this, oneStackOnly: true);
    }


    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);


        RefreshPowerCacheAndOutput();

        if (map != null)
        {
            ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
            if (groupComp != null)
            {
                if (respawningAfterLoad)
                {
                    if (controllerGroupId < 1)
                    {
                        controllerGroupId = groupComp.CreateNewGroupId();
                    }
                }
                else
                {
                    if (controllerGroupId < 1)
                    {
                        bool isAutoController = GetComp<ULS_AutoGroupMarker>() != null;
                        int minNeighborGroupId = int.MaxValue;

                        foreach (var t in GenAdj.CardinalDirections)
                        {
                            IntVec3 neighborCell = Position + t;
                            if (neighborCell.InBounds(map) &&
                                ULS_Utility.TryGetControllerAt(map, neighborCell,
                                    out Building_WallController neighborController))
                            {
                                bool neighborIsAuto = neighborController.GetComp<ULS_AutoGroupMarker>() != null;
                                if (neighborIsAuto != isAutoController)
                                {
                                    continue;
                                }

                                int neighborGroupId = neighborController.ControllerGroupId;
                                if (neighborGroupId > 0 &&
                                    (ULS_AutoGroupUtility.IsGroupCompatibleForAutoMerge(map, neighborGroupId,
                                        isAutoController)) &&
                                    neighborGroupId < minNeighborGroupId)
                                {
                                    minNeighborGroupId = neighborGroupId;
                                }
                            }
                        }

                        controllerGroupId = (minNeighborGroupId != int.MaxValue)
                            ? minNeighborGroupId
                            : groupComp.CreateNewGroupId();
                    }
                }


                groupComp.RegisterOrUpdateController(this);


                if (GetComp<ULS_AutoGroupMarker>() != null)
                {
                    map.GetComponent<ULS_AutoGroupMapComponent>()?.NotifyAutoGroupsDirty();
                }
            }
        }


        if (respawningAfterLoad && InLiftProcess)
        {
            EnsureLiftBlocker();
            ApplyActivePowerInternal(active: true);
        }
    }


    public override void ExposeData()
    {
        base.ExposeData();


        Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
        Scribe_Values.Look(ref storedRotation, "storedRotation", Rot4.North);
        Scribe_Values.Look(ref storedCell, "storedCell", IntVec3.Invalid);
        Scribe_Values.Look(ref multiCellGroupRootCell, "multiCellGroupRootCell", IntVec3.Invalid);
        Scribe_Values.Look(ref controllerGroupId, "controllerGroupId");
        Scribe_Values.Look(ref storedThingMarketValueIgnoreHp, "storedThingMarketValueIgnoreHp");


        Scribe_Collections.Look(ref storedLinkMaskCells, "storedLinkMaskCells", LookMode.Value);
        Scribe_Collections.Look(ref storedLinkMaskValues, "storedLinkMaskValues", LookMode.Value);


        Scribe_Values.Look(ref liftProcessState, "liftProcessState");
        Scribe_Values.Look(ref liftTicksRemaining, "liftTicksRemaining");
        Scribe_Values.Look(ref liftTicksTotal, "liftTicksTotal");
        Scribe_Values.Look(ref liftBlockerCell, "liftBlockerCell", IntVec3.Invalid);
        Scribe_Values.Look(ref liftFinalizeOnComplete, "liftFinalizeOnComplete", defaultValue: false);

        Scribe_Values.Look(ref liftActionPending, "liftActionPending");
        Scribe_Values.Look(ref liftActionIsRaise, "liftActionIsRaise");
        Scribe_Values.Look(ref liftActionStartCell, "liftActionStartCell", IntVec3.Invalid);


        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            innerContainer ??= new ThingOwner<Thing>(this, oneStackOnly: true);


            if (!HasStored)
            {
                storedThingMarketValueIgnoreHp = 0f;
            }
            else if (StoredThing is Building building &&
                     building.Faction == Faction.OfPlayer &&
                     storedThingMarketValueIgnoreHp <= 0f)
            {
                storedThingMarketValueIgnoreHp = building.GetStatValue(StatDefOf.MarketValueIgnoreHp);
            }

            storedLinkMaskCells ??= new List<IntVec3>();
            storedLinkMaskValues ??= new List<byte>();


            if (storedLinkMaskCells.Count != storedLinkMaskValues.Count)
            {
                storedLinkMaskCells.Clear();
                storedLinkMaskValues.Clear();
            }
        }
    }


    internal bool TryGetStoredLinkDirections(IntVec3 cell, out LinkDirections linkDirections)
    {
        linkDirections = LinkDirections.None;
        if (storedLinkMaskCells == null || storedLinkMaskValues == null)
        {
            return false;
        }

        for (int i = 0; i < storedLinkMaskCells.Count; i++)
        {
            if (storedLinkMaskCells[i] == cell)
            {
                linkDirections = (LinkDirections)storedLinkMaskValues[i];
                return true;
            }
        }

        return false;
    }

    private void ClearStoredLinkMaskCache()
    {
        storedLinkMaskCells?.Clear();
        storedLinkMaskValues?.Clear();
    }

    private void CacheStoredLinkMaskForBuilding(Building building, Map map)
    {
        if (building == null || map == null)
        {
            return;
        }

        storedLinkMaskCells ??= new List<IntVec3>();

        storedLinkMaskValues ??= new List<byte>();

        storedLinkMaskCells.Clear();
        storedLinkMaskValues.Clear();

        if (building.def?.graphicData == null)
        {
            return;
        }


        if (building.Graphic is not Graphic_Linked)
        {
            return;
        }

        LinkFlags linkFlags = building.def.graphicData.linkFlags;
        if (linkFlags == LinkFlags.None)
        {
            return;
        }


        IntVec3 parentPos = building.Position;

        foreach (IntVec3 cell in building.OccupiedRect())
        {
            int mask = 0;
            int bit = 1;

            for (int i = 0; i < 4; i++)
            {
                IntVec3 neighbor = cell + GenAdj.CardinalDirections[i];
                if (!neighbor.InBounds(map))
                {
                    if ((linkFlags & LinkFlags.MapEdge) != 0)
                    {
                        mask += bit;
                    }
                }
                else
                {
                    if (ModsConfig.OdysseyActive &&
                        ((map.terrainGrid.FoundationAt(neighbor)?.IsSubstructure ?? false) !=
                         (map.terrainGrid.FoundationAt(parentPos)?.IsSubstructure ?? false)))
                    {
                    }
                    else if ((map.linkGrid.LinkFlagsAt(neighbor) & linkFlags) != 0)
                    {
                        mask += bit;
                    }
                }

                bit *= 2;
            }

            storedLinkMaskCells.Add(cell);
            storedLinkMaskValues.Add((byte)mask);
        }
    }


    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        ClearLiftProcessAndRemoveBlocker();


        Map map = Map;
        IntVec3 position = Position;


        if (map != null)
        {
            map.GetComponent<ULS_ControllerGroupMapComponent>()?.RemoveControllerCell(position);


            if (GetComp<ULS_AutoGroupMarker>() != null)
            {
                map.GetComponent<ULS_AutoGroupMapComponent>()?.NotifyAutoGroupsDirty();
            }
        }


        if (map != null && multiCellGroupRootCell.IsValid)
        {
            ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();
            if (multiCellComp != null)
            {
                multiCellComp.RefundAndRemoveGroup(multiCellGroupRootCell);
                base.Destroy(mode);
                return;
            }
        }


        RefundStored(map);
        base.Destroy(mode);
    }


    public ThingOwner GetDirectlyHeldThings()
    {
        return innerContainer;
    }


    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        if (innerContainer is not null)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, innerContainer);
        }
    }
}