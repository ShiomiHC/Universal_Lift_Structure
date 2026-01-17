namespace Universal_Lift_Structure;

// ============================================================
// 【升降动作请求枚举】
// ============================================================
// 定义用户期望执行的升降动作类型
//
// 【用途】
// - 在非 Remote 模式下，用于跟踪用户通过 Gizmo 设定的期望状态
// - 与 Designation 系统配合，指示小人应执行的动作类型
//
// 【使用场景】
// - Manual 模式：记录用户期望，小人执行升降任务时读取此状态
// - Console 模式：将此状态转换为全局队列中的请求
// ============================================================
public enum ULS_LiftActionRequest
{
    // 无操作
    // 表示没有期望的升降动作，不需要 Designation
    None,

    // 期望升起
    // 表示用户希望将建筑升起到地表
    Raise,

    // 期望降下
    // 表示用户希望将建筑降下到地下
    Lower
}

// ============================================================
// 【墙体控制器核心类】
// ============================================================
// 控制建筑物在地表与地下之间升降的核心建筑
//
// 【继承关系】
// - 继承自 Building：作为地图上的可操作建筑存在
// - 实现 IThingHolder：可存储和管理地下建筑物
//
// 【核心职责】
// 1. 存储管理：将建筑物存入内部容器（降下时）或取出（升起时）
// 2. 升降流程：管理整个升降动画和状态转换过程
// 3. 电力管理：检查并消耗电力以执行升降操作
// 4. 分组系统：支持多控制器编组和多格结构联动
// 5. 用户交互：提供 Gizmo 按钮和各种控制模式
//
// 【partial 类结构】
// 本类是 partial 类，功能分散在多个文件中：
// - Building_WallController.cs：主类定义、生命周期、序列化
// - Building_WallController_Power.cs：电力系统
// - Building_WallController_Group.cs：分组逻辑
// - Building_WallController_GroupHelpers.cs：分组辅助方法
// - Building_WallController_LiftProcess.cs：升降流程管理
// - Building_WallController_RaiseLower.cs：升降动作执行
// - Building_WallController_Gizmos.cs：用户界面
//
// 【关键设计】
// - 使用 ThingOwner 存储建筑，支持序列化和嵌套容器
// - 缓存 LinkMask 数据，用于恢复 Linked 图形的连接状态
// - 支持三种控制模式（Remote/Manual/Console）
// ============================================================
public partial class Building_WallController : Building, IThingHolder
{
    // ============================================================
    // 【存储相关字段】
    // ============================================================

    // 内部容器，存储降下的建筑物
    // 特性：oneStackOnly = true，只能存储一个建筑
    private ThingOwner<Thing> innerContainer;

    // 存储建筑的原始朝向
    // 用途：升起时恢复建筑的正确朝向
    internal Rot4 storedRotation = Rot4.North;

    // 存储建筑的原始位置
    // 用途：验证位置有效性，确保升起时位置可用
    internal IntVec3 storedCell = IntVec3.Invalid;

    // 缓存的 LinkMask 数据：单元格列表
    // 用途：记录 Linked 图形建筑每个单元格的连接状态
    private List<IntVec3> storedLinkMaskCells;

    // 缓存的 LinkMask 数据：连接方向值
    // 用途：与 storedLinkMaskCells 一一对应，存储每个单元格的 LinkDirections
    private List<byte> storedLinkMaskValues;

    // 缓存的建筑市场价值（忽略耐久度）
    // 用途：用于计算控制器的整体市场价值（见 ULS_StatPart_StoredMarketValue）
    private float storedThingMarketValueIgnoreHp;

    // ============================================================
    // 【升降动作状态字段】
    // ============================================================
    // 这些字段用于 Manual/Console 模式下的 Designation 系统

    // 是否有待处理的升降动作
    // 用途：Manual 模式下，小人执行任务时检查此标志
    private bool liftActionPending;

    // 待处理动作是否为"升起"
    // true = 升起，false = 降下
    private bool liftActionIsRaise;

    // 动作开始时的位置（仅用于降下操作）
    // 用途：验证控制器是否在动作执行前移动过
    private IntVec3 liftActionStartCell = IntVec3.Invalid;

    // 用户期望的升降动作
    // 用途：Gizmo 和 Designation 系统的核心状态机
    private ULS_LiftActionRequest wantedLiftAction = ULS_LiftActionRequest.None;

    // ============================================================
    // 【缓存的 MapComponent 引用】
    // ============================================================
    // 在 SpawnSetup 时初始化，避免重复调用 GetComponent

    // 升降请求队列管理器
    private ULS_LiftRequestMapComponent cachedLiftRequestComp;

    // 控制器分组管理器
    private ULS_ControllerGroupMapComponent cachedGroupComp;

    // ============================================================
    // 【公共只读属性】
    // ============================================================

    // 是否有待处理的升降动作
    public bool LiftActionPending => liftActionPending;

    // 当前期望的升降动作类型
    public ULS_LiftActionRequest WantedLiftAction => wantedLiftAction;

    // ============================================================
    // 【小人触发升降动作回调】
    // ============================================================
    // 当小人执行 JobDriver_FlickWallController 任务时调用
    //
    // 【调用时机】
    // - Manual 模式：小人完成前往控制器的任务后
    // - 仅在 liftActionPending = true 时执行
    //
    // 【执行流程】
    // 1. 检查是否有待处理的动作
    // 2. 根据 liftActionIsRaise 执行升起或降下操作
    // 3. 清除待处理状态
    // 4. 重置期望状态并更新 Designation
    //
    // 【参数说明】
    // - pawn: 执行操作的小人（当前未使用）
    //
    // 【注意事项】
    // - 此方法仅在 Manual 模式下被调用
    // - Remote 和 Console 模式不使用此回调机制
    // ============================================================
    public void Notify_FlickedBy(Pawn pawn)
    {
        // 检查是否有待处理的动作
        if (!liftActionPending)
        {
            return;
        }

        // 根据动作类型执行对应操作
        if (liftActionIsRaise)
        {
            TryRaiseGroup(showMessage: true);
        }
        else
        {
            TryLowerGroup(liftActionStartCell, showMessage: true);
        }

        // 清除待处理状态
        liftActionPending = false;
        liftActionStartCell = IntVec3.Invalid;

        // 重置期望状态并更新 Designation
        wantedLiftAction = ULS_LiftActionRequest.None;
        UpdateLiftDesignation();
    }

    // ============================================================
    // 【将升降动作加入待处理队列】
    // ============================================================
    // 直接设置待处理状态并添加 Designation
    //
    // 【调用场景】
    // - 由外部系统（如 Patch 或特殊逻辑）调用
    // - 绕过 wantedLiftAction 状态机，直接设置待处理动作
    //
    // 【参数说明】
    // - isRaise: true = 升起，false = 降下
    // - lowerStartCell: 降下动作的起始位置（升起时传入 IntVec3.Invalid）
    //
    // 【执行流程】
    // 1. 设置 liftActionPending 标志为 true
    // 2. 记录动作类型和起始位置
    // 3. 同步 wantedLiftAction 状态
    // 4. 添加 Designation（如果尚未存在）
    //
    // 【注意事项】
    // - 此方法不经过 UpdateLiftDesignation 的完整流程
    // - 主要用于兼容性或特殊场景
    // ============================================================
    public void QueueLiftAction(bool isRaise, IntVec3 lowerStartCell)
    {
        // 设置待处理标志
        liftActionPending = true;
        liftActionIsRaise = isRaise;
        liftActionStartCell = lowerStartCell;

        // 同步期望状态
        wantedLiftAction = isRaise ? ULS_LiftActionRequest.Raise : ULS_LiftActionRequest.Lower;

        // 添加 Designation（如果不存在）
        if (Map.designationManager.DesignationOn(this, ULS_DesignationDefOf.ULS_FlickLiftStructure) == null)
        {
            Map.designationManager.AddDesignation(new Designation(this, ULS_DesignationDefOf.ULS_FlickLiftStructure));
        }
    }

    // ============================================================
    // 【更新升降 Designation】
    // ============================================================
    // 根据当前控制模式和期望状态，同步 Designation 和队列
    //
    // 【设计参考】
    // - 参考 RimWorld FlickUtility.UpdateFlickDesignation 的设计模式
    //
    // 【三种控制模式的处理逻辑】
    //
    // 1. Remote 模式：
    //    - 不使用期望状态机制
    //    - 清除 wantedLiftAction
    //    - 删除所有 Designation
    //
    // 2. Manual 模式：
    //    - 直接在本地设置 liftActionPending
    //    - 根据 wantedLiftAction 设置动作类型
    //    - Designation 基于 wantedLiftAction
    //
    // 3. Console 模式：
    //    - 将期望状态转换为全局队列中的请求
    //    - 期望升起/降下时：添加请求到 LiftRequestMapComponent
    //    - 期望取消时：从队列移除针对本控制器的请求
    //    - Designation 基于队列中是否有针对此控制器的请求
    //
    // 【重要机制】
    // - Console 模式下，Designation 的存在完全由全局队列决定
    // - Manual 模式下，Designation 的存在完全由本地 wantedLiftAction 决定
    //
    // 【调用时机】
    // - 用户通过 Gizmo 设置期望状态时
    // - 控制模式切换时
    // - 动作执行完成后
    // ============================================================
    public void UpdateLiftDesignation()
    {
        // 检查地图是否有效
        if (Map == null) return;

        // 获取当前控制模式
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        LiftControlMode controlMode = settings?.liftControlMode ?? LiftControlMode.Remote;
        Designation des = Map.designationManager.DesignationOn(this, ULS_DesignationDefOf.ULS_FlickLiftStructure);

        // ========================================
        // Remote 模式：不使用 Designation
        // ========================================
        if (controlMode == LiftControlMode.Remote)
        {
            wantedLiftAction = ULS_LiftActionRequest.None;

            // 清除可能残留的 Designation（从其他模式切换到 Remote 时）
            if (des != null)
            {
                des.Delete();
            }

            return;
        }

        // ============================================================
        // 判断是否需要 Designation（基于期望状态）
        // ============================================================
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

        // ========================================
        // Manual 模式：直接在本地设置
        // ========================================
        if (controlMode == LiftControlMode.Manual)
        {
            liftActionPending = needsDesignation;
            if (needsDesignation)
            {
                // 设置动作类型
                liftActionIsRaise = (wantedLiftAction == ULS_LiftActionRequest.Raise);

                // 设置起始位置（仅降下动作需要）
                liftActionStartCell = (wantedLiftAction == ULS_LiftActionRequest.Lower)
                    ? Position
                    : IntVec3.Invalid;
            }
            else
            {
                liftActionStartCell = IntVec3.Invalid;
            }
        }
        // ========================================
        // Console 模式：同步到全局队列
        // ========================================
        else if (controlMode == LiftControlMode.Console)
        {
            var mapComp = cachedLiftRequestComp;
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

        // ============================================================
        // 同步 Designation
        // ============================================================
        // Console 模式下，Designation 基于全局队列
        if (controlMode == LiftControlMode.Console)
        {
            var mapComp = cachedLiftRequestComp;
            needsDesignation = (mapComp != null && mapComp.HasRequestForController(this));
        }

        // 添加或删除 Designation
        if (needsDesignation && des == null)
        {
            Map.designationManager.AddDesignation(new Designation(this, ULS_DesignationDefOf.ULS_FlickLiftStructure));
        }
        else if (!needsDesignation && des != null)
        {
            des.Delete();
        }
    }

    // ============================================================
    // 【设置期望升降动作】
    // ============================================================
    // 由 Gizmo 调用，设置用户期望的升降动作类型
    //
    // 【参数说明】
    // - action: 期望的动作类型（None/Raise/Lower）
    // - lowerStartCell: 降下动作的起始位置（仅在 action = Lower 时有效）
    //
    // 【执行流程】
    // 1. 设置 wantedLiftAction 字段
    // 2. 如果是降下动作，记录起始位置
    // 3. 调用 UpdateLiftDesignation() 同步状态
    //
    // 【调用场景】
    // - 用户点击 Gizmo 按钮时
    // - 用户取消期望动作时
    //
    // 【注意事项】
    // - 此方法会触发完整的 Designation 更新流程
    // - Console 模式下会自动同步到全局队列
    // ============================================================
    public void SetWantedLiftAction(ULS_LiftActionRequest action, IntVec3 lowerStartCell)
    {
        wantedLiftAction = action;

        // 对于降下动作，需要记录起始位置
        if (action == ULS_LiftActionRequest.Lower)
        {
            liftActionStartCell = lowerStartCell;
        }

        // 更新 Designation 和队列状态
        UpdateLiftDesignation();
    }


    // ============================================================
    // 【取消当前升降请求】
    // ============================================================
    // 清除期望状态并同步 Designation 和队列
    //
    // 【调用场景】
    // - 用户点击 Gizmo 取消按钮时
    // - Console 模式：控制台处理完请求后调用，标记请求已完成
    //
    // 【执行流程】
    // 1. 将 wantedLiftAction 设置为 None
    // 2. 调用 UpdateLiftDesignation() 同步状态
    //    - Manual 模式：清除 liftActionPending
    //    - Console 模式：从全局队列移除请求
    //    - 所有模式：删除 Designation
    //
    // 【注意事项】
    // - Console 模式下，此方法也用于标记请求已处理完成
    // - 清除后，Gizmo 界面会相应更新
    // ============================================================
    public void CancelLiftAction()
    {
        // 重置期望状态为无操作
        wantedLiftAction = ULS_LiftActionRequest.None;

        // 同步 Designation 和队列
        UpdateLiftDesignation();

        // 立即刷新缓存，让 UI 响应更快
        RefreshGizmoCache();
    }


    // ============================================================
    // 【分组相关字段】
    // ============================================================

    // 多格结构的根单元格位置
    // 用途：如果此控制器是多格建筑组的一部分，此字段指向组的根单元格
    // Invalid 表示此控制器不是多格组的一部分
    private IntVec3 multiCellGroupRootCell = IntVec3.Invalid;

    // 控制器分组 ID
    // 用途：用于多个控制器的编组功能，同组控制器可以联动升降
    // \u003c 1 表示无效分组 ID
    private int controllerGroupId;

    // ============================================================
    // 【分组相关属性】
    // ============================================================

    // 多格结构根单元格（内部访问）
    // 用途：由 ULS_MultiCellGroupMapComponent 管理，标识此控制器所属的多格组
    internal IntVec3 MultiCellGroupRootCell
    {
        get => multiCellGroupRootCell;
        set => multiCellGroupRootCell = value;
    }

    // 控制器分组 ID（内部访问）
    // 用途：由 ULS_ControllerGroupMapComponent 管理，用于分组联动功能
    internal int ControllerGroupId
    {
        get => controllerGroupId;
        set
        {
            controllerGroupId = value;
            InvalidateGizmoCache(); // 分组变化时立即刷新缓存
        }
    }

    // ============================================================
    // 【存储相关属性】
    // ============================================================

    // 存储的建筑物（私有访问）
    // 用途：快速访问内部容器中的第一个物品（唯一物品）
    // 返回：容器中的建筑物，如果为空则返回 null
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

    // 是否存储了建筑物（公共访问）
    // 用途：检查控制器是否处于"已降下"状态（即内部有存储建筑）
    // 返回：true = 已存储建筑，false = 未存储建筑
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

    // 存储建筑的市场价值（忽略耐久度）（内部访问）
    // 用途：用于 ULS_StatPart_StoredMarketValue 计算控制器的整体价值
    // 返回：存储建筑的市场价值，如果未存储则返回 0
    internal float StoredThingMarketValueIgnoreHp => storedThingMarketValueIgnoreHp;


    // ============================================================
    // 【建筑创建后初始化】
    // ============================================================
    // 当建筑首次在世界中创建时调用（不包括加载存档）
    //
    // 【执行流程】
    // 1. 调用基类的 PostMake 方法
    // 2. 初始化内部容器（如果尚未初始化）
    //
    // 【注意事项】
    // - 使用空合并赋值确保容器只初始化一次
    // - oneStackOnly = true 确保容器只能存储一个建筑
    // ============================================================
    public override void PostMake()
    {
        base.PostMake();
        innerContainer ??= new ThingOwner<Thing>(this, oneStackOnly: true);
    }


    // ============================================================
    // 【建筑生成到地图时初始化】
    // ============================================================
    // 当建筑生成到地图时调用（包括首次放置和存档加载）
    //
    // 【执行流程】
    // 1. 调用基类的 SpawnSetup 方法
    // 2. 缓存地图组件引用（请求队列、分组管理器）
    // 3. 刷新电力缓存和输出
    // 4. 处理分组逻辑：
    //    - 加载时：如果无分组ID，创建新ID
    //    - 首次放置：尝试与相邻控制器合并分组
    // 5. 注册到分组管理器
    // 6. 如果是自动组控制器，通知自动组系统
    // 7. 如果是加载时且正在升降过程中，恢复升降阻挡器和电力状态
    //
    // 【参数说明】
    // - map: 目标地图
    // - respawningAfterLoad: true = 从存档加载，false = 首次放置
    //
    // 【注意事项】
    // - 首次放置时会自动尝试与相邻同类型控制器合并分组
    // - 自动组和手动组控制器不会相互合并
    // - 加载时需要恢复升降进行中的状态
    // ============================================================
    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);

        // 缓存地图组件引用，避免重复调用 GetComponent
        if (map != null)
        {
            cachedLiftRequestComp = map.GetComponent<ULS_LiftRequestMapComponent>();
            cachedGroupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        }

        // 刷新电力系统缓存
        RefreshPowerCacheAndOutput();

        // 处理分组逻辑
        if (map != null)
        {
            ULS_ControllerGroupMapComponent groupComp = cachedGroupComp;
            if (groupComp != null)
            {
                // 加载时：确保有分组ID
                if (respawningAfterLoad)
                {
                    if (controllerGroupId < 1)
                    {
                        controllerGroupId = groupComp.CreateNewGroupId();
                    }
                }
                // 首次放置：尝试与相邻控制器合并分组
                else
                {
                    if (controllerGroupId < 1)
                    {
                        bool isAutoController = ULS_AutoGroupUtility.IsAutoController(this);
                        int minNeighborGroupId = int.MaxValue;

                        // 遍历四个基本方向的相邻单元格
                        foreach (var t in GenAdj.CardinalDirections)
                        {
                            IntVec3 neighborCell = Position + t;
                            if (neighborCell.InBounds(map) &&
                                ULS_Utility.TryGetControllerAt(map, neighborCell,
                                    out Building_WallController neighborController))
                            {
                                // 检查相邻控制器是否为同类型（自动组或手动组）
                                bool neighborIsAuto = ULS_AutoGroupUtility.IsAutoController(neighborController);
                                if (neighborIsAuto != isAutoController)
                                {
                                    continue;
                                }

                                // 检查相邻组是否兼容
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

                        // 如果找到兼容的相邻组，加入该组；否则创建新组
                        controllerGroupId = (minNeighborGroupId != int.MaxValue)
                            ? minNeighborGroupId
                            : groupComp.CreateNewGroupId();
                    }
                }

                // 注册到分组管理器
                groupComp.RegisterOrUpdateController(this);

                // 如果是自动组控制器，标记自动组需要重新扫描
                if (ULS_AutoGroupUtility.IsAutoController(this))
                {
                    map.GetComponent<ULS_AutoGroupMapComponent>()?.NotifyAutoGroupsDirty();
                }
            }
        }

        // 加载时恢复升降进行中的状态
        if (respawningAfterLoad && InLiftProcess)
        {
            EnsureLiftBlocker();
            ApplyActivePowerInternal(active: true);
            cachedGroupComp?.RegisterAnimatingController(this);
        }

        // 初始化 Gizmo 缓存
        RefreshGizmoCache();
    }


    // ============================================================
    // 【序列化和反序列化】
    // ============================================================
    // 处理建筑数据的保存和加载
    //
    // 【序列化字段组】
    // 1. 存储相关：内部容器、建筑朝向、位置、市场价值
    // 2. LinkMask缓存：连接图形的单元格和方向数据
    // 3. 升降流程：状态、剩余时间、总时间、阻挡器位置
    // 4. 升降动作：待处理标志、动作类型、起始位置、期望动作
    // 5. 分组相关：多格组根单元格、控制器分组ID
    //
    // 【加载后处理】
    // - 确保内部容器已初始化
    // - 验证并修复市场价值缓存
    // - 初始化 LinkMask 列表
    // - 验证 LinkMask 数据一致性
    //
    // 【注意事项】
    // - 使用默认值确保兼容旧版本存档
    // - 加载后会自动修复缺失的市场价值数据
    // ============================================================
    public override void ExposeData()
    {
        base.ExposeData();

        // 序列化存储相关字段
        Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
        Scribe_Values.Look(ref storedRotation, "storedRotation", Rot4.North);
        Scribe_Values.Look(ref storedCell, "storedCell", IntVec3.Invalid);
        Scribe_Values.Look(ref multiCellGroupRootCell, "multiCellGroupRootCell", IntVec3.Invalid);
        Scribe_Values.Look(ref controllerGroupId, "controllerGroupId");
        Scribe_Values.Look(ref storedThingMarketValueIgnoreHp, "storedThingMarketValueIgnoreHp");

        // 序列化 LinkMask 缓存
        Scribe_Collections.Look(ref storedLinkMaskCells, "storedLinkMaskCells", LookMode.Value);
        Scribe_Collections.Look(ref storedLinkMaskValues, "storedLinkMaskValues", LookMode.Value);

        // 序列化升降流程字段
        Scribe_Values.Look(ref liftProcessState, "liftProcessState");
        Scribe_Values.Look(ref liftTicksRemaining, "liftTicksRemaining");
        Scribe_Values.Look(ref liftTicksTotal, "liftTicksTotal");
        Scribe_Values.Look(ref liftBlockerCell, "liftBlockerCell", IntVec3.Invalid);
        Scribe_Values.Look(ref liftFinalizeOnComplete, "liftFinalizeOnComplete", defaultValue: false);

        // 序列化升降动作字段
        Scribe_Values.Look(ref liftActionPending, "liftActionPending");
        Scribe_Values.Look(ref liftActionIsRaise, "liftActionIsRaise");
        Scribe_Values.Look(ref liftActionStartCell, "liftActionStartCell", IntVec3.Invalid);
        Scribe_Values.Look(ref wantedLiftAction, "wantedLiftAction");

        // 加载后处理
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // 确保内部容器已初始化
            innerContainer ??= new ThingOwner<Thing>(this, oneStackOnly: true);

            // 验证并修复市场价值缓存
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

            // 初始化 LinkMask 列表
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


    // ============================================================
    // 【查询存储建筑的连接方向】
    // ============================================================
    // 根据单元格位置查询该位置的 LinkDirections
    //
    // 【用途】
    // - 在升起建筑时恢复 Linked 图形的连接状态
    // - 由 ULS_GhostRenderer 调用，确保虚影显示正确的连接
    //
    // 【参数说明】
    // - cell: 要查询的单元格位置
    // - linkDirections: 输出参数，查询到的连接方向
    //
    // 【返回值】
    // - true: 找到该单元格的连接数据
    // - false: 未找到数据或缓存为空
    //
    // 【注意事项】
    // - 仅在存储了 Linked 图形建筑时有数据
    // - 数据在降下建筑时缓存，升起建筑后清除
    // ============================================================
    internal bool TryGetStoredLinkDirections(IntVec3 cell, out LinkDirections linkDirections)
    {
        linkDirections = LinkDirections.None;

        // 检查缓存是否有效
        if (storedLinkMaskCells == null || storedLinkMaskValues == null)
        {
            return false;
        }

        // 遍历缓存查找匹配的单元格
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

    // ============================================================
    // 【清除 LinkMask 缓存】
    // ============================================================
    // 清空存储的连接图形缓存数据
    //
    // 【调用时机】
    // - 升起建筑后，数据不再需要
    // - 重新缓存前，清除旧数据
    //
    // 【注意事项】
    // - 使用空合并操作符确保空引用安全
    // ============================================================
    private void ClearStoredLinkMaskCache()
    {
        storedLinkMaskCells?.Clear();
        storedLinkMaskValues?.Clear();
    }

    // ============================================================
    // 【缓存建筑的 LinkMask 数据】
    // ============================================================
    // 在降下 Linked 图形建筑时，保存每个单元格的连接状态
    //
    // 【用途】
    // - 确保升起建筑时能正确恢复连接图形
    // - 支持虚影渲染显示正确的连接状态
    //
    // 【参数说明】
    // - building: 要缓存的建筑
    // - map: 建筑所在的地图
    //
    // 【执行流程】
    // 1. 验证参数和图形类型
    // 2. 初始化并清空缓存列表
    // 3. 检查建筑是否使用 Graphic_Linked
    // 4. 遍历建筑占据的每个单元格：
    //    - 检查四个基本方向的相邻单元格
    //    - 根据连接标志和地图边界计算连接掩码
    //    - 支持 Odyssey DLC 的子结构系统
    // 5. 将单元格和掩码值存入缓存
    //
    // 【比特掩码说明】
    // - bit 0 (1): 北方连接
    // - bit 1 (2): 东方连接
    // - bit 2 (4): 南方连接
    // - bit 3 (8): 西方连接
    //
    // 【注意事项】
    // - 仅对 Graphic_Linked 类型的建筑有效
    // - 必须在建筑从地图移除前调用
    // - 支持地图边界连接（MapEdge 标志）
    // ============================================================
    private void CacheStoredLinkMaskForBuilding(Building building, Map map)
    {
        // 验证参数
        if (building == null || map == null)
        {
            return;
        }

        // 初始化缓存列表
        storedLinkMaskCells ??= new List<IntVec3>();
        storedLinkMaskValues ??= new List<byte>();

        // 清空旧数据
        storedLinkMaskCells.Clear();
        storedLinkMaskValues.Clear();

        // 检查图形数据是否有效
        if (building.def?.graphicData == null)
        {
            return;
        }

        // 仅处理 Linked 图形
        if (building.Graphic is not Graphic_Linked)
        {
            return;
        }

        // 获取连接标志
        LinkFlags linkFlags = building.def.graphicData.linkFlags;
        if (linkFlags == LinkFlags.None)
        {
            return;
        }

        // 记录建筑根位置（用于子结构检测）
        IntVec3 parentPos = building.Position;

        // 遍历建筑占据的每个单元格
        foreach (IntVec3 cell in building.OccupiedRect())
        {
            int mask = 0; // 连接掩码
            int bit = 1; // 当前方向的比特位

            // 检查四个基本方向（北、东、南、西）
            for (int i = 0; i < 4; i++)
            {
                IntVec3 neighbor = cell + GenAdj.CardinalDirections[i];

                // 邻居单元格在地图外
                if (!neighbor.InBounds(map))
                {
                    // 如果支持地图边界连接，设置连接位
                    if ((linkFlags & LinkFlags.MapEdge) != 0)
                    {
                        mask += bit;
                    }
                }
                // 邻居单元格在地图内
                else
                {
                    // Odyssey DLC：检查子结构兼容性
                    if (ModsConfig.OdysseyActive &&
                        ((map.terrainGrid.FoundationAt(neighbor)?.IsSubstructure ?? false) !=
                         (map.terrainGrid.FoundationAt(parentPos)?.IsSubstructure ?? false)))
                    {
                        // 子结构类型不同，不连接
                    }
                    // 检查邻居单元格是否有匹配的连接标志
                    else if ((map.linkGrid.LinkFlagsAt(neighbor) & linkFlags) != 0)
                    {
                        mask += bit;
                    }
                }

                // 移动到下一个方向的比特位
                bit *= 2;
            }

            // 存储单元格和对应的连接掩码
            storedLinkMaskCells.Add(cell);
            storedLinkMaskValues.Add((byte)mask);
        }
    }


    // ============================================================
    // 【建筑销毁处理】
    // ============================================================
    // 当建筑被销毁时调用，执行清理和退费逻辑
    //
    // 【执行流程】
    // 1. 清除升降流程并移除阻挡器
    // 2. 记录销毁前的地图和位置（base.Destroy 后无法访问）
    // 3. 从全局升降队列移除针对本控制器的请求
    // 4. 从控制器分组系统移除
    // 5. 如果是自动组控制器，通知自动组系统重新扫描
    // 6. 处理两种销毁情况：
    //    - 多格组根控制器：退费整个多格组
    //    - 普通控制器：退费存储的建筑
    // 7. 调用基类销毁方法
    //
    // 【参数说明】
    // - mode: 销毁模式（Vanish/Kill/Deconstruct等）
    //
    // 【注意事项】
    // - 必须在调用 base.Destroy 前记录 Map 和 Position
    // - 多格组根控制器会触发整个组的退费
    // - 确保所有地图组件引用都被正确清理
    // ============================================================
    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        // 清除升降流程和阻挡器
        ClearLiftProcessAndRemoveBlocker();

        // 记录销毁前的状态（base.Destroy 后无法访问）
        Map map = Map;

        // 从全局升降队列移除针对本控制器的请求
        if (map != null)
        {
            var liftReqComp = cachedLiftRequestComp;
            liftReqComp?.RemoveRequestsForController(this);
        }

        // 从控制器分组系统移除
        if (map != null)
        {
            cachedGroupComp?.DeregisterController(this);

            // 如果是自动组控制器，通知自动组系统重新扫描
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


    // ============================================================
    // 【IThingHolder 接口实现：获取直接持有的物品】
    // ============================================================
    // 返回此建筑直接持有的物品容器
    //
    // 【用途】
    // - 用于序列化系统遍历嵌套容器
    // - 确保存储的建筑被正确保存和加载
    //
    // 【返回值】
    // - 内部容器，包含降下的建筑物（如果有）
    // ============================================================
    public ThingOwner GetDirectlyHeldThings()
    {
        return innerContainer;
    }


    // ============================================================
    // 【IThingHolder 接口实现：获取子容器】
    // ============================================================
    // 递归获取此建筑内部的所有子容器
    //
    // 【用途】
    // - 用于序列化系统遍历嵌套的容器结构
    // - 确保多层嵌套的建筑（如容器中的容器）被正确处理
    //
    // 【参数说明】
    // - outChildren: 输出列表，用于收集所有子容器
    //
    // 【注意事项】
    // - 如果内部容器为空，不添加任何内容
    // - 使用 ThingOwnerUtility 确保正确提取嵌套容器
    // ============================================================
    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        if (innerContainer is not null)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, innerContainer);
        }
    }
}