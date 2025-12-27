namespace Universal_Lift_Structure;

/// 文件意图：核心建筑逻辑：Building_WallController（结构控制器）- 核心部分。
/// 包含：类定义、字段声明、核心属性、生命周期方法、IThingHolder 接口实现。
///
/// 设计要点：
/// - 显式分组：每个控制器持久化一个玩家可见的 GroupId（唯一权威）；"组升起/组降下"按 GroupId 枚举成员，不再做实时 4 向连通域扫描。
/// - 自动分配：新放置控制器会扫描四邻已存在控制器，按"最小 GroupId"自动加入；四邻无控制器则创建新组。
/// - 多格建筑：按建筑 footprint 建立隐组，强制要求每格都有控制器；由根格控制器作为主控制器实际持有收纳物，成员控制器记录根格引用实现联动。
/// - 生命周期：在控制器被销毁前尝试对收纳物执行 RefundStored，并联动清理多格隐组。
public partial class Building_WallController : Building, IThingHolder
{
    // ==================== 字段声明 ====================

    // 容器：存储被收纳的建筑物
    private ThingOwner<Thing> innerContainer;

    // 存储状态：记录被收纳物的旋转、位置
    private Rot4 storedRotation = Rot4.North;
    private IntVec3 storedCell = IntVec3.Invalid;

    // Link 贴图缓存：用于让“被收纳建筑的升降虚影”在 DeSpawn 期间仍保持收纳瞬间的连接形态。
    // 设计：按 occupied cell 缓存 4-bit linkMask（0..15），等价于原版 Graphic_Linked.LinkedDrawMatFrom 的计算结果。
    // 注意：这仅用于虚影自身（语义①），不会影响邻居建筑的连接刷新。
    private List<IntVec3> storedLinkMaskCells;
    private List<byte> storedLinkMaskValues;

    // 财富统计：缓存被收纳建筑的 MarketValueIgnoreHp
    // 目的：配合 StatPart 将价值叠加到控制器上，确保被收纳物仍计入 WealthBuildings
    private float storedThingMarketValueIgnoreHp;

    // 多格隐组：若该控制器属于某个"多格收纳隐组"，此处记录其根格（Building.Position）
    // 目的：在任意成员控制器被销毁时，能够追溯到隐组并触发整体 Refund/清理
    private IntVec3 multiCellGroupRootCell = IntVec3.Invalid;

    // 显式分组：玩家可见的组ID
    // 该字段会被存档持久化
    // 组规模不受 groupMaxSize 限制；groupMaxSize 仅限制"组升起/组降下"是否允许执行
    private int controllerGroupId;

    // ==================== 核心属性 ====================

    /// 多格隐组根格访问器
    internal IntVec3 MultiCellGroupRootCell
    {
        get => multiCellGroupRootCell;
        set => multiCellGroupRootCell = value;
    }

    /// 显式分组 ID 访问器
    internal int ControllerGroupId
    {
        get => controllerGroupId;
        set => controllerGroupId = value;
    }

    /// 获取存储的物品（只读）
    public Thing StoredThing
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

    /// 是否有存储物品
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

    /// 财富统计：只读访问存储物品的市场价值
    internal float StoredThingMarketValueIgnoreHp => storedThingMarketValueIgnoreHp;

    // ==================== 生命周期方法 ====================

    /// 初始化：创建容器
    public override void PostMake()
    {
        base.PostMake();
        if (innerContainer == null)
        {
            innerContainer = new ThingOwner<Thing>(this, oneStackOnly: true);
        }
    }

    /// 生成设置：注册到分组系统、初始化电力和 Flick 代理、恢复升降状态
    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);

        // 初始化电力系统
        RefreshPowerCacheAndOutput();
        // 重要：读档重生阶段不要主动创建 `ULS_FlickProxy`。
        // 原因：控制器的 `SpawnSetup` 可能早于存档中的隐藏物（代理）完成生成/登记，
        // 若此处强制创建会导致“每次读档都额外新增一个代理”并堆积。
        // 代理按需惰性获取（`GetProxyFlickTrigger()` -> `EnsureFlickProxy()`）即可。
        if (!respawningAfterLoad)
        {
            EnsureFlickProxy();
        }

        if (map != null)
        {
            ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
            if (groupComp != null)
            {
                if (respawningAfterLoad)
                {
                    // 读档：确保有有效的组 ID
                    if (controllerGroupId < 1)
                    {
                        controllerGroupId = groupComp.CreateNewGroupId();
                    }
                }
                else
                {
                    // 新放置：自动分配组 ID（扫描四邻，取最小 GroupId 或创建新组）
                    if (controllerGroupId < 1)
                    {
                        bool isAutoController = GetComp<ULS_AutoGroupMarker>() != null;
                        int minNeighborGroupId = int.MaxValue;

                        for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                        {
                            IntVec3 neighborCell = Position + GenAdj.CardinalDirections[i];
                            if (neighborCell.InBounds(map) &&
                                ULS_Utility.TryGetControllerAt(map, neighborCell,
                                    out Building_WallController neighborController))
                            {
                                // 自动/手动控制器不能混组
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

                // 注册到分组系统
                groupComp.RegisterOrUpdateController(this);

                // 自动组：通知自动组系统刷新
                if (GetComp<ULS_AutoGroupMarker>() != null)
                {
                    map.GetComponent<ULS_AutoGroupMapComponent>()?.NotifyAutoGroupsDirty();
                }
            }
        }

        // 读档恢复：如果正在升降过程中，恢复阻挡器和电力状态
        if (respawningAfterLoad && InLiftProcess)
        {
            EnsureLiftBlocker();
            ApplyActivePowerInternal(active: true);
        }
    }

    /// 存档序列化：保存所有状态字段
    public override void ExposeData()
    {
        base.ExposeData();

        // 容器和存储状态
        Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
        Scribe_Values.Look(ref storedRotation, "storedRotation", Rot4.North);
        Scribe_Values.Look(ref storedCell, "storedCell", IntVec3.Invalid);
        Scribe_Values.Look(ref multiCellGroupRootCell, "multiCellGroupRootCell", IntVec3.Invalid);
        Scribe_Values.Look(ref controllerGroupId, "controllerGroupId");
        Scribe_Values.Look(ref storedThingMarketValueIgnoreHp, "storedThingMarketValueIgnoreHp");

        // Link 贴图缓存（升降虚影用）
        Scribe_Collections.Look(ref storedLinkMaskCells, "storedLinkMaskCells", LookMode.Value);
        Scribe_Collections.Look(ref storedLinkMaskValues, "storedLinkMaskValues", LookMode.Value);

        // 升降过程状态
        Scribe_Values.Look(ref liftProcessState, "liftProcessState");
        Scribe_Values.Look(ref liftTicksRemaining, "liftTicksRemaining");
        Scribe_Values.Look(ref liftTicksTotal, "liftTicksTotal");
        Scribe_Values.Look(ref liftBlockerCell, "liftBlockerCell", IntVec3.Invalid);
        Scribe_Values.Look(ref liftFinalizeOnComplete, "liftFinalizeOnComplete", defaultValue: false);

        // Flick 代理
        Scribe_References.Look(ref flickProxy, "flickProxy");
        Scribe_Values.Look(ref flickProxyCell, "flickProxyCell", IntVec3.Invalid);

        // 读档后初始化
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // 确保容器存在
            if (innerContainer == null)
            {
                innerContainer = new ThingOwner<Thing>(this, oneStackOnly: true);
            }

            // 刷新财富缓存
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

            // 清理无效的 Flick 代理引用
            if (flickProxy != null && (flickProxy.Destroyed || !flickProxy.Spawned))
            {
                flickProxy = null;
            }

            // 确保 Link 缓存容器存在（允许为空但不为 null，便于后续逻辑直接使用）
            storedLinkMaskCells ??= new List<IntVec3>();
            storedLinkMaskValues ??= new List<byte>();

            // 防御：若读档造成数量不一致，直接清空（开发期不静默兜底：让问题暴露在可见行为上）
            if (storedLinkMaskCells.Count != storedLinkMaskValues.Count)
            {
                storedLinkMaskCells.Clear();
                storedLinkMaskValues.Clear();
            }
        }
    }

    // ==================== Link 贴图缓存（升降虚影） ====================

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

    internal void ClearStoredLinkMaskCache()
    {
        storedLinkMaskCells?.Clear();
        storedLinkMaskValues?.Clear();
    }

    internal void CacheStoredLinkMaskForBuilding(Building building, Map map)
    {
        if (building == null || map == null)
        {
            return;
        }

        if (storedLinkMaskCells == null)
        {
            storedLinkMaskCells = new List<IntVec3>();
        }

        if (storedLinkMaskValues == null)
        {
            storedLinkMaskValues = new List<byte>();
        }

        storedLinkMaskCells.Clear();
        storedLinkMaskValues.Clear();

        if (building.def?.graphicData == null)
        {
            return;
        }

        // 只缓存基础链接：墙/围墙这类（用户确认范围 A）。
        if (building.Graphic is not Graphic_Linked)
        {
            return;
        }

        LinkFlags linkFlags = building.def.graphicData.linkFlags;
        if (linkFlags == LinkFlags.None)
        {
            return;
        }

        // 注意：Odyssey 子结构判定按原版对齐：比较邻格与 parent.Position（建筑根格）的 IsSubstructure。
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
                        // 不连接
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

    /// 销毁：清理升降状态、Flick 代理、从分组系统注销、退款存储物
    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        // 清理升降过程和阻挡器
        ClearLiftProcessAndRemoveBlocker();

        // 清理 Flick 代理
        DestroyFlickProxyIfAny();

        Map map = Map;
        IntVec3 position = Position;

        // 从分组系统注销
        if (map != null)
        {
            map.GetComponent<ULS_ControllerGroupMapComponent>()?.RemoveControllerCell(position);

            // 自动组：通知自动组系统刷新
            if (GetComp<ULS_AutoGroupMarker>() != null)
            {
                map.GetComponent<ULS_AutoGroupMapComponent>()?.NotifyAutoGroupsDirty();
            }
        }

        // 多格隐组：触发整体退款和清理
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

        // 普通控制器：退款存储物
        RefundStored(map);
        base.Destroy(mode);
    }

    // ==================== IThingHolder 接口实现 ====================

    /// 获取直接持有的物品容器（用于存档系统）
    public ThingOwner GetDirectlyHeldThings()
    {
        return innerContainer;
    }

    /// 获取子容器（用于递归遍历）
    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        if (innerContainer is not null)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, innerContainer);
        }
    }
}