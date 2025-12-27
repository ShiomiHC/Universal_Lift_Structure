namespace Universal_Lift_Structure;

/// 文件意图：Building_WallController - 升降过程管理。
/// 包含：升降状态机、升降阻挡器管理、升降特效、升降流程控制、Tick 升降逻辑。
public partial class Building_WallController
{
    // ==================== 升降过程字段 ====================

    /// 升降过程状态枚举
    private enum LiftProcessState
    {
        None,       // 无升降过程
        Raising,    // 正在升起
        Lowering    // 正在降下
    }

    // 升降过程状态
    private LiftProcessState liftProcessState;

    // 升降剩余时间（Ticks）
    private int liftTicksRemaining;

    // 升降总时间（Ticks）
    private int liftTicksTotal;

    // 升降阻挡器位置
    private IntVec3 liftBlockerCell = IntVec3.Invalid;

    // 升降完成时是否执行最终化（用于多格建筑联动）
    private bool liftFinalizeOnComplete;

    // 升降特效常量
    private const int LiftFleckIntervalTicks = 20;      // 粉尘特效间隔
    private const float LiftFleckRadius = 0.7f;         // 普通粉尘半径
    private const float LiftFleckScale = 1f;            // 普通粉尘缩放
    private const int LiftBurstCount = 6;               // 爆发烟雾数量
    private const float LiftBurstRadius = 1.3f;           // 爆发烟雾半径
    private const float LiftBurstScale = 1.3f;            // 爆发烟雾缩放

    // ==================== 升降过程属性 ====================

    /// 是否正在升降过程中
    private bool InLiftProcess
    {
        get
        {
            if (liftProcessState != LiftProcessState.None)
            {
                return liftTicksRemaining > 0;
            }
            return false;
        }
    }

    /// UI 专用：是否正在升降过程中
    internal bool InLiftProcessForUI => InLiftProcess;

    /// 渲染层读取升降进度（0..1）
    internal bool TryGetLiftProgress01(out float progress01, out bool isRaising)
    {
        if (!InLiftProcess || liftTicksTotal <= 0)
        {
            progress01 = 0f;
            isRaising = false;
            return false;
        }

        isRaising = liftProcessState == LiftProcessState.Raising;
        float totalTicks = liftTicksTotal;
        float remainingTicks = liftTicksRemaining;

        if (isRaising)
        {
            // 统一语义：progress01 始终表示“已完成比例”（0 -> 1）。
            progress01 = 1f - remainingTicks / totalTicks;
        }
        else if (liftProcessState == LiftProcessState.Lowering)
        {
            // 降下同样使用“已完成比例”（0 -> 1），方向由 isRaising 区分。
            progress01 = 1f - remainingTicks / totalTicks;
        }
        else
        {
            progress01 = 0f;
            return false;
        }

        progress01 = Mathf.Clamp01(progress01);
        return true;
    }

    /// 尝试获取当前活跃的升降阻挡器位置
    internal bool TryGetActiveLiftBlockerCell(out IntVec3 cell)
    {
        if (InLiftProcess && liftBlockerCell.IsValid)
        {
            cell = liftBlockerCell;
            return true;
        }
        cell = IntVec3.Invalid;
        return false;
    }

    // ==================== 升降时间计算 ====================

    /// 根据物品属性计算升降所需时间（Ticks）
    private static int CalculateLiftTicks(Thing thing)
    {
        if (thing == null)
        {
            return 60;
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        float hpMultiplier = settings?.liftDurationHpSet ?? 1f;
        float massMultiplier = settings?.liftDurationMassSet ?? 1f;

        float hpTicks = thing.MaxHitPoints * 0.2f * hpMultiplier;
        float massTicks = thing.GetStatValue(StatDefOf.Mass) * 50f * massMultiplier;
        int calculatedTicks = Mathf.RoundToInt(hpTicks + massTicks);

        return Mathf.Max(60, calculatedTicks);
    }

    // ==================== 升降特效 ====================

    /// 播放升降粉尘/烟雾特效
    /// <param name="burst">true=爆发烟雾，false=普通粉尘</param>
    private void ThrowLiftDustFleck(bool burst)
    {
        Map map = Map;
        if (map == null || !Spawned)
        {
            return;
        }

        Vector3 basePos = Position.ToVector3Shifted();

        if (!burst)
        {
            // 普通粉尘
            float radius = LiftFleckRadius;
            float scale = LiftFleckScale;
            FleckMaker.ThrowDustPuff(
                (basePos + Gen.RandomHorizontalVector(radius)).WithY(AltitudeLayer.MoteLow.AltitudeFor()),
                map,
                scale);
        }
        else
        {
            // 爆发粉尘
            int count = LiftBurstCount;
            float radius = LiftBurstRadius;
            float size = LiftBurstScale;
            for (int i = 0; i < count; i++)
            {
                FleckMaker.ThrowDustPuff(
                    (basePos + Gen.RandomHorizontalVector(radius)).WithY(AltitudeLayer.MoteLow.AltitudeFor()),
                    map,
                    size);
            }
        }
    }

    // ==================== 升降流程控制 ====================

    /// 开始升降过程
    private void BeginLiftProcess(LiftProcessState state, IntVec3 blockerCell, int ticksTotal, bool finalizeOnComplete)
    {
        if (ticksTotal < 1)
        {
            ticksTotal = 1;
        }

        liftProcessState = state;
        liftTicksTotal = ticksTotal;
        liftTicksRemaining = ticksTotal;
        liftBlockerCell = blockerCell;
        liftFinalizeOnComplete = finalizeOnComplete;

        ApplyActivePowerInternal(active: true);
        EnsureLiftBlocker();
        ThrowLiftDustFleck(burst: true);
    }

    /// 清理升降过程并移除阻挡器
    private void ClearLiftProcessAndRemoveBlocker()
    {
        DestroyLiftBlockerIfAny();

        liftProcessState = LiftProcessState.None;
        liftTicksRemaining = 0;
        liftTicksTotal = 0;
        liftBlockerCell = IntVec3.Invalid;
        liftFinalizeOnComplete = false;

        ApplyActivePowerInternal(active: false);
    }

    /// 尝试开始降下过程
    private void TryStartLoweringProcess(IntVec3 blockerCell, int ticks)
    {
        if (InLiftProcess)
        {
            return;
        }

        BeginLiftProcess(LiftProcessState.Lowering, blockerCell, ticks, finalizeOnComplete: false);
    }

    /// 尝试开始升起过程（多格建筑联动）
    private bool TryStartRaisingProcess(Map map)
    {
        if (map == null || InLiftProcess || !HasStored)
        {
            return false;
        }

        Thing storedThing = StoredThing;
        if (storedThing == null)
        {
            storedCell = IntVec3.Invalid;
            storedThingMarketValueIgnoreHp = 0f;
            return false;
        }

        IntVec3 spawnCell = storedCell.IsValid ? storedCell : Position;
        if (IsBlockedForRaise(map, spawnCell, storedThing))
        {
            return false;
        }

        int ticksTotal = CalculateLiftTicks(storedThing);

        // 获取多格成员控制器
        HashSet<Building_WallController> memberControllers = GetMultiCellMemberControllersOrSelf(map);

        // 检查所有成员是否都空闲
        foreach (Building_WallController member in memberControllers)
        {
            if (member == null || member.InLiftProcess)
            {
                return false;
            }
        }

        // 启动所有成员的升起过程
        foreach (Building_WallController member in memberControllers)
        {
            bool finalizeOnComplete = (member == this); // 只有根控制器负责最终化
            member.BeginLiftProcess(LiftProcessState.Raising, member.Position, ticksTotal, finalizeOnComplete);
        }

        return true;
    }

    // ==================== 升降阻挡器管理 ====================

    /// 确保升降阻挡器存在
    private void EnsureLiftBlocker()
    {
        Map map = Map;
        if (map != null && liftBlockerCell.IsValid && liftBlockerCell.InBounds(map))
        {
            Building existing = map.edificeGrid[liftBlockerCell];
            if (existing != null)
            {
                // 已存在建筑，不处理
                _ = existing.def;
                _ = ULS_ThingDefOf.ULS_LiftBlocker;
            }
            else
            {
                // 生成阻挡器
                GenSpawn.Spawn(ThingMaker.MakeThing(ULS_ThingDefOf.ULS_LiftBlocker), liftBlockerCell, map);
            }
        }
    }

    /// 销毁升降阻挡器（如果存在）
    private void DestroyLiftBlockerIfAny()
    {
        Map map = Map;
        if (map != null && liftBlockerCell.IsValid && liftBlockerCell.InBounds(map))
        {
            Building blocker = map.edificeGrid[liftBlockerCell];
            if (blocker != null && blocker.def == ULS_ThingDefOf.ULS_LiftBlocker)
            {
                blocker.Destroy();
            }
        }
    }

    // ==================== Tick 升降逻辑 ====================

    /// Tick：处理升降过程
    protected override void Tick()
    {
        base.Tick();

        // 确保电力特性禁用时恢复空闲功耗
        EnsureIdlePowerIfFeatureDisabled();

        if (!InLiftProcess)
        {
            return;
        }

        // 电力检查
        if (PowerFeatureEnabled)
        {
            if (!PowerOn)
            {
                HandlePowerLossDuringLift();
                return;
            }
            ApplyActivePowerInternal(active: true);
        }

        // 定期刷新阻挡器
        if (liftTicksRemaining % 60 == 0)
        {
            EnsureLiftBlocker();
        }

        // 定期播放粉尘特效
        int elapsed = liftTicksTotal - liftTicksRemaining;
        if (elapsed > 0 && elapsed % LiftFleckIntervalTicks == 0)
        {
            ThrowLiftDustFleck(burst: false);
        }

        // 倒计时
        liftTicksRemaining--;
        if (liftTicksRemaining > 0)
        {
            return;
        }

        // ==================== 升降完成 ====================

        ThrowLiftDustFleck(burst: true);

        LiftProcessState completedState = liftProcessState;
        bool shouldFinalize = liftFinalizeOnComplete;

        // 清理升降状态
        DestroyLiftBlockerIfAny();
        liftProcessState = LiftProcessState.None;
        liftTicksRemaining = 0;
        liftTicksTotal = 0;
        liftBlockerCell = IntVec3.Invalid;
        liftFinalizeOnComplete = false;
        ApplyActivePowerInternal(active: false);

        // 升起完成：执行最终化（生成建筑）
        if (completedState == LiftProcessState.Raising && shouldFinalize)
        {
            Map map = Map;
            if (map == null || !HasStored)
            {
                return;
            }

            Thing storedThing = StoredThing;
            if (storedThing == null)
            {
                storedCell = IntVec3.Invalid;
                storedThingMarketValueIgnoreHp = 0f;
                return;
            }

            IntVec3 spawnCell = storedCell.IsValid ? storedCell : Position;

            // 再次检查阻挡（可能在升降过程中有其他物体进入）
            if (!IsBlockedForRaise(map, spawnCell, storedThing))
            {
                TryRaiseNoMessage(map);
            }
        }
    }
}
