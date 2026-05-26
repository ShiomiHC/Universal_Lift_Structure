namespace Universal_Lift_Structure;

// Manual/Console 模式下跟踪用户期望的升降动作，与 Designation 系统配合
public enum ULS_LiftActionRequest
{
    None,
    Raise,
    Lower
}

public partial class Building_WallController : Building, IThingHolder
{
    private ThingOwner<Thing> innerContainer;

    internal Rot4 storedRotation = Rot4.North;
    internal IntVec3 storedCell = IntVec3.Invalid;

    // 降下 Linked 图形建筑时缓存连接状态，升起时用于恢复
    private List<IntVec3> storedLinkMaskCells;
    private List<byte> storedLinkMaskValues;

    // 用于 ULS_StatPart_StoredMarketValue 计算控制器整体价值
    private float storedThingMarketValueIgnoreHp;

    // PreSwapMap 时记录绝对位置，PostSwapMap 用于计算刚体变换（旋转 + 平移）
    private IntVec3 preSwapPosition = IntVec3.Invalid;

    // Manual/Console 模式下的 Designation 系统状态
    private bool liftActionPending;
    private bool liftActionIsRaise; // true = 升起，false = 降下
    private IntVec3 liftActionStartCell = IntVec3.Invalid; // 仅降下操作使用
    private ULS_LiftActionRequest wantedLiftAction = ULS_LiftActionRequest.None;

    // 在 SpawnSetup 时初始化，避免频繁 GetComponent
    private ULS_LiftRequestMapComponent cachedLiftRequestComp;
    private ULS_ControllerGroupMapComponent cachedGroupComp;

    public bool LiftActionPending => liftActionPending;
    public ULS_LiftActionRequest WantedLiftAction => wantedLiftAction;

    // Manual 模式下小人执行 JobDriver_FlickWallController 完成后回调
    public void Notify_FlickedBy(Pawn pawn)
    {
        if (!liftActionPending)
        {
            return;
        }

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

        wantedLiftAction = ULS_LiftActionRequest.None;
        UpdateLiftDesignation();
    }

    // 绕过 wantedLiftAction 状态机直接设置待处理动作，由外部系统（如 Patch）调用
    public void QueueLiftAction(bool isRaise, IntVec3 lowerStartCell)
    {
        liftActionPending = true;
        liftActionIsRaise = isRaise;
        liftActionStartCell = lowerStartCell;

        wantedLiftAction = isRaise ? ULS_LiftActionRequest.Raise : ULS_LiftActionRequest.Lower;

        if (Map.designationManager.DesignationOn(this, ULS_DesignationDefOf.ULS_FlickLiftStructure) == null)
        {
            Map.designationManager.AddDesignation(new Designation(this, ULS_DesignationDefOf.ULS_FlickLiftStructure));
        }
    }

    // 根据当前控制模式和期望状态同步 Designation 与队列（参考 FlickUtility.UpdateFlickDesignation）
    // Remote：不使用 Designation；Manual：本地设置 liftActionPending；Console：同步到全局队列
    public void UpdateLiftDesignation()
    {
        if (Map == null) return;

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        LiftControlMode controlMode = settings?.liftControlMode ?? LiftControlMode.Remote;
        Designation des = Map.designationManager.DesignationOn(this, ULS_DesignationDefOf.ULS_FlickLiftStructure);

        if (controlMode == LiftControlMode.Remote)
        {
            wantedLiftAction = ULS_LiftActionRequest.None;

            // 清除从其他模式切换时可能残留的 Designation
            if (des != null)
            {
                des.Delete();
            }

            return;
        }

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
        else if (controlMode == LiftControlMode.Console)
        {
            var mapComp = cachedLiftRequestComp;
            if (mapComp != null)
            {
                if (needsDesignation)
                {
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
                    mapComp.RemoveRequestsForController(this);
                }
            }
        }

        // Console 模式下 Designation 的存在完全由全局队列决定
        if (controlMode == LiftControlMode.Console)
        {
            var mapComp = cachedLiftRequestComp;
            needsDesignation = (mapComp != null && mapComp.HasRequestForController(this));
        }

        if (needsDesignation && des == null)
        {
            Map.designationManager.AddDesignation(new Designation(this, ULS_DesignationDefOf.ULS_FlickLiftStructure));
        }
        else if (!needsDesignation && des != null)
        {
            des.Delete();
        }
    }

    public void SetWantedLiftAction(ULS_LiftActionRequest action, IntVec3 lowerStartCell)
    {
        wantedLiftAction = action;

        if (action == ULS_LiftActionRequest.Lower)
        {
            liftActionStartCell = lowerStartCell;
        }

        UpdateLiftDesignation();
    }


    // Console 模式下也用此方法标记请求已处理完成
    public void CancelLiftAction()
    {
        wantedLiftAction = ULS_LiftActionRequest.None;
        UpdateLiftDesignation();
        RefreshGizmoCache();
    }


    private int controllerGroupId;

    // 纯内存属性（不序列化），由 ULS_MultiCellGroupMapComponent 加载期统一单向派发
    internal IntVec3 MultiCellGroupRootCell { get; set; } = IntVec3.Invalid;

    internal int ControllerGroupId
    {
        get => controllerGroupId;
        set
        {
            controllerGroupId = value;
            InvalidateGizmoCache();
        }
    }

    internal Thing StoredThing
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

        // 避免频繁 GetComponent
        if (map != null)
        {
            cachedLiftRequestComp = map.GetComponent<ULS_LiftRequestMapComponent>();
            cachedGroupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        }

        RefreshPowerCacheAndOutput();

        if (map != null)
        {
            ULS_ControllerGroupMapComponent groupComp = cachedGroupComp;
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

                // 如果是自动组控制器，标记自动组需要重新扫描
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
            cachedGroupComp?.RegisterAnimatingController(this);
        }

        // 初始化 Gizmo 缓存
        // 修复：存档加载时延迟刷新，避免在地图未完全初始化时（例如控制台尚未加载）产生错误缓存
        if (respawningAfterLoad)
        {
            InvalidateGizmoCache(); // 仅标记失效，等玩家选中时再刷新
        }
        else
        {
            RefreshGizmoCache(); // 新建时立即刷新
        }
    }

    public override void PreSwapMap()
    {
        base.PreSwapMap();

        if (InLiftProcess && cachedGroupComp != null)
        {
            cachedGroupComp.DeregisterAnimatingController(this);
        }

        // 在建筑被移出旧地图之前记录绝对位置，PostSwapMap 用于计算刚体旋转变换
        preSwapPosition = Position;
    }

    public override void PostSwapMap()
    {
        base.PostSwapMap();

        // 纯内存字段：PostSwapMap 时清空，等待 TryRebuildMultiCellGroupAfterTransfer 重新派发
        MultiCellGroupRootCell = IntVec3.Invalid;

        if (HasStored && preSwapPosition.IsValid)
        {
            // 对于非可旋转建筑（如控制器本身），自身Rotation无法反映飞船的旋转情况。
            // 真实旋转增量即为飞船着陆时的 Rotation（相对于其内部北向原图偏移）。
            int deltaRotInt = Find.CurrentGravship?.Rotation.AsInt ?? 0;
            Rot4 deltaRot = new Rot4(deltaRotInt);
            bool hasRotation = deltaRotInt != 0;

            if (storedCell.IsValid)
            {
                IntVec3 oldOffset = storedCell - preSwapPosition;
                storedCell = Position + (hasRotation ? oldOffset.RotatedBy(deltaRot) : oldOffset);
            }

            if (hasRotation)
            {
                storedRotation = new Rot4((storedRotation.AsInt + deltaRotInt) % 4);
            }

            if (storedLinkMaskCells is { Count: > 0 })
            {
                for (int i = 0; i < storedLinkMaskCells.Count; i++)
                {
                    IntVec3 cellOffset = storedLinkMaskCells[i] - preSwapPosition;
                    storedLinkMaskCells[i] = Position + (hasRotation ? cellOffset.RotatedBy(deltaRot) : cellOffset);

                    if (hasRotation && storedLinkMaskValues != null && i < storedLinkMaskValues.Count)
                    {
                        // 循环左移 deltaRotInt 位（在 4 位掩码内）
                        int mask = storedLinkMaskValues[i];
                        mask = ((mask << deltaRotInt) | (mask >> (4 - deltaRotInt))) & 0xF;
                        storedLinkMaskValues[i] = (byte)mask;
                    }
                }
            }
        }

        if (InLiftProcess && liftBlockerCell.IsValid && preSwapPosition.IsValid)
        {
            int deltaRotInt = Find.CurrentGravship?.Rotation.AsInt ?? 0;
            Rot4 deltaRot = new Rot4(deltaRotInt);
            bool hasRotation = deltaRotInt != 0;

            IntVec3 oldOffset = liftBlockerCell - preSwapPosition;
            liftBlockerCell = Position + (hasRotation ? oldOffset.RotatedBy(deltaRot) : oldOffset);
        }

        if (InLiftProcess && cachedGroupComp != null)
        {
            EnsureLiftBlocker();
            ApplyActivePowerInternal(active: true);
            cachedGroupComp.RegisterAnimatingController(this);
        }

        if (HasStored)
        {
            // 推迟到新地图首次 Tick 执行，确保所有组员格同时已在这个瞬间完成落位 (飞船引擎生成时序避让)
            var multiCellComp = Map?.GetComponent<ULS_MultiCellGroupMapComponent>();
            if (multiCellComp != null)
            {
                multiCellComp.RegisterPendingRebuild(this);
            }
        }
    }

    internal void TryRebuildMultiCellGroupAfterTransfer()
    {
        Map map = Map;
        Thing stored = StoredThing;
        if (map == null || stored == null || stored.def == null || stored.def.size == IntVec2.One)
            return; // 单格建筑无需重建

        ULS_MultiCellGroupMapComponent multiCellComp =
            map.GetComponent<ULS_MultiCellGroupMapComponent>();
        if (multiCellComp == null) return;

        if (multiCellComp.HasGroup(Position)) return;

        // storedRotation 已在 PostSwapMap 中正确叠加了 delta 旋转
        CellRect footprint = GenAdj.OccupiedRect(Position, storedRotation, stored.def.size);

        List<IntVec3> memberCells = new List<IntVec3>();
        List<Building_WallController> memberControllers = new List<Building_WallController>();

        foreach (IntVec3 cell in footprint)
        {
            if (!ULS_Utility.TryGetControllerAt(map, cell, out var controller))
            {
                return; // 某格缺少控制器，放弃重建
            }

            // ULS_MultiCellGroupRecord 设计上，memberControllers 不包含主控(自己)
            if (cell != Position)
            {
                memberCells.Add(cell);
            }
            memberControllers.Add(controller); // 给所有人派发时需要包含主控
        }

        multiCellComp.TryAddGroup(new ULS_MultiCellGroupRecord(Position, Position, memberCells));

        // Bug 修复：Print() 属于静态网格批渲染，不主动触发脏标则飞船传输后成员格会保持旧贴图
        foreach (var c in memberControllers)
        {
            c.MultiCellGroupRootCell = Position;
            map.mapDrawer.MapMeshDirty(c.Position, MapMeshFlagDefOf.Things);
            map.mapDrawer.MapMeshDirty(c.Position, MapMeshFlagDefOf.Buildings);
        }
    }
    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
        Scribe_Values.Look(ref storedRotation, "storedRotation", Rot4.North);
        Scribe_Values.Look(ref storedCell, "storedCell", IntVec3.Invalid);
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

            // 验证 LinkMask 数据一致性：单元格和值必须一一对应
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

    // 必须在建筑从地图移除前调用，缓存 Linked 图形连接状态以便升起时恢复
    // 比特掩码：bit0=北, bit1=东, bit2=南, bit3=西
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
            int mask = 0; // 连接掩码
            int bit = 1; // 当前方向的比特位

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
                    // Odyssey DLC：检查子结构兼容性
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

        // 必须在 base.Destroy 前记录，调用后无法访问 Map
        Map map = Map;

        if (map != null)
        {
            var liftReqComp = cachedLiftRequestComp;
            liftReqComp?.RemoveRequestsForController(this);
        }

        if (map != null)
        {
            cachedGroupComp?.DeregisterController(this);

            // 如果是自动组控制器，通知自动组系统重新扫描
            if (ULS_AutoGroupUtility.IsAutoController(this))
            {
                map.GetComponent<ULS_AutoGroupMapComponent>()?.NotifyAutoGroupsDirty();
            }
        }

        if (map != null && MultiCellGroupRootCell.IsValid)
        {
            ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();
            if (multiCellComp != null)
            {
                multiCellComp.DestroyAndRemoveGroup(MultiCellGroupRootCell);
                base.Destroy(mode);
                return;
            }
        }

        DestroyStored(map);
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