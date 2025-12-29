namespace Universal_Lift_Structure;

// 期望的升降动作类型
public enum ULS_LiftActionRequest
{
    None, // 无操作
    Raise, // 期望升起
    Lower // 期望降下
}

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

    // 期望的升降动作（用于 Gizmo 取消功能）
    private ULS_LiftActionRequest wantedLiftAction = ULS_LiftActionRequest.None;

    public bool LiftActionPending => liftActionPending;
    public ULS_LiftActionRequest WantedLiftAction => wantedLiftAction;

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

        // 重置期望状态并更新 Designation
        wantedLiftAction = ULS_LiftActionRequest.None;
        UpdateLiftDesignation();
    }

    public void QueueLiftAction(bool isRaise, IntVec3 lowerStartCell)
    {
        liftActionPending = true;
        liftActionIsRaise = isRaise;
        liftActionStartCell = lowerStartCell;

        // 同步期望状态
        wantedLiftAction = isRaise ? ULS_LiftActionRequest.Raise : ULS_LiftActionRequest.Lower;

        if (Map.designationManager.DesignationOn(this, ULS_DesignationDefOf.ULS_FlickLiftStructure) == null)
        {
            Map.designationManager.AddDesignation(new Designation(this, ULS_DesignationDefOf.ULS_FlickLiftStructure));
        }
    }

    // 更新升降 Designation（参考 RW FlickUtility.UpdateFlickDesignation）
    public void UpdateLiftDesignation()
    {
        if (Map == null) return;

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        LiftControlMode controlMode = settings?.liftControlMode ?? LiftControlMode.Remote;

        // Remote 模式不使用期望状态机制
        if (controlMode == LiftControlMode.Remote)
        {
            wantedLiftAction = ULS_LiftActionRequest.None;
            return;
        }

        // 判断是否需要 Designation
        bool needsDesignation = false;
        switch (wantedLiftAction)
        {
            case ULS_LiftActionRequest.Raise:
            case ULS_LiftActionRequest.Lower:
                needsDesignation = true;
                break;
            case ULS_LiftActionRequest.None:
                break;
        }

        // Manual 模式：直接在本地设置
        if (controlMode == LiftControlMode.Manual)
        {
            liftActionPending = needsDesignation;
            if (needsDesignation)
            {
                liftActionIsRaise = (wantedLiftAction == ULS_LiftActionRequest.Raise);
                liftActionStartCell = (wantedLiftAction == ULS_LiftActionRequest.Lower)
                    ? Position
                    : IntVec3.Invalid;
            }
            else
            {
                liftActionStartCell = IntVec3.Invalid;
            }
        }
        // Console 模式：同步到全局队列
        else if (controlMode == LiftControlMode.Console)
        {
            var mapComp = Map.GetComponent<ULS_LiftRequestMapComponent>();
            if (mapComp != null)
            {
                if (needsDesignation)
                {
                    // 添加请求到全局队列
                    ULS_LiftRequestType requestType = (wantedLiftAction == ULS_LiftActionRequest.Raise)
                        ? ULS_LiftRequestType.RaiseGroup
                        : ULS_LiftRequestType.LowerGroup;
                    IntVec3 startCell = (wantedLiftAction == ULS_LiftActionRequest.Lower)
                        ? Position
                        : IntVec3.Invalid;
                    mapComp.EnqueueRequest(new ULS_LiftRequest(requestType, this, startCell));
                }
                else
                {
                    // 取消：从全局队列移除针对本控制器的请求
                    mapComp.RemoveRequestsForController(this);
                }
            }
        }

        // 同步 Designation（参考 FlickUtility.UpdateFlickDesignation 的模式）
        // 在 Console 模式下，Designation 基于全局队列中是否有针对此控制器的请求
        if (controlMode == LiftControlMode.Console)
        {
            var mapComp = Map.GetComponent<ULS_LiftRequestMapComponent>();
            needsDesignation = (mapComp != null && mapComp.HasRequestForController(this));
        }

        Designation des = Map.designationManager.DesignationOn(this, ULS_DesignationDefOf.ULS_FlickLiftStructure);
        if (needsDesignation && des == null)
        {
            Map.designationManager.AddDesignation(new Designation(this, ULS_DesignationDefOf.ULS_FlickLiftStructure));
        }
        else if (!needsDesignation && des != null)
        {
            des.Delete();
        }
    }

    // 设置期望升降动作（用于 Gizmo 和 Harmony Patch 调用）
    public void SetWantedLiftAction(ULS_LiftActionRequest action, IntVec3 lowerStartCell)
    {
        wantedLiftAction = action;

        // 对于降下动作，需要记录起始位置
        if (action == ULS_LiftActionRequest.Lower)
        {
            liftActionStartCell = lowerStartCell;
        }

        UpdateLiftDesignation();
    }

    /// <summary>
    /// 取消当前的升降请求。
    /// 在 Console 模式下，当控制台处理完请求后，也调用此方法来"完成"请求并清除状态。
    /// </summary>
    public void CancelLiftAction()
    {
        wantedLiftAction = ULS_LiftActionRequest.None;
        UpdateLiftDesignation();
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
                        bool isAutoController = ULS_AutoGroupUtility.IsAutoController(this);
                        int minNeighborGroupId = int.MaxValue;

                        foreach (var t in GenAdj.CardinalDirections)
                        {
                            IntVec3 neighborCell = Position + t;
                            if (neighborCell.InBounds(map) &&
                                ULS_Utility.TryGetControllerAt(map, neighborCell,
                                    out Building_WallController neighborController))
                            {
                                bool neighborIsAuto = ULS_AutoGroupUtility.IsAutoController(neighborController);
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


                if (ULS_AutoGroupUtility.IsAutoController(this))
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
        Scribe_Values.Look(ref wantedLiftAction, "wantedLiftAction");


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

        // 记录销毁前的状态（用于后续操作）
        Map map = Map;
        IntVec3 position = Position;

        // 从全局升降队列移除针对本控制器的请求
        if (map != null)
        {
            var liftReqComp = map.GetComponent<ULS_LiftRequestMapComponent>();
            liftReqComp?.RemoveRequestsForController(this);
        }

        // 从控制器组移除
        if (map != null)
        {
            map.GetComponent<ULS_ControllerGroupMapComponent>()?.RemoveControllerCell(position);

            // 如果是自动组控制器，通知自动组系统
            if (ULS_AutoGroupUtility.IsAutoController(this))
            {
                map.GetComponent<ULS_AutoGroupMapComponent>()?.NotifyAutoGroupsDirty();
            }
        }

        // 如果是多格组根控制器，退费并移除整个组
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

        // 普通销毁：退费存储的建筑
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