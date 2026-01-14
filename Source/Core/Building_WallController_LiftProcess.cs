namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    // ============================================================
    // 【升降过程状态枚举】
    // ============================================================
    private enum LiftProcessState
    {
        None, // 无状态
        Raising, // 正在升起
        Lowering // 正在降下
    }


    private LiftProcessState liftProcessState; // 当前升降状态
    private int liftTicksRemaining; // 剩余Tick数
    private int liftTicksTotal; // 总Tick数
    private IntVec3 liftBlockerCell = IntVec3.Invalid; // 阻挡器位置
    private bool liftFinalizeOnComplete; // 完成后是否执行最终逻辑

    // 视觉效果常量定义
    private const int LiftFleckIntervalTicks = 20;
    private const float LiftFleckRadius = 0.7f;
    private const float LiftFleckScale = 1f;
    private const int LiftBurstCount = 6;
    private const float LiftBurstRadius = 1.3f;
    private const float LiftBurstScale = 1.3f;


    // ============================================================
    // 【是否处于升降过程中】
    // ============================================================
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


    internal bool InLiftProcessForUI => InLiftProcess;


    // ============================================================
    // 【获取升降进度】
    // ============================================================
    // 获取当前升降进度的归一化值 (0-1)
    //
    // 【参数说明】
    // - progress01: 输出进度值
    // - isRaising: 输出是否为升起状态
    //
    // 【返回值】
    // - true: 获取成功（处于升降过程中）
    // ============================================================
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
            progress01 = 1f - remainingTicks / totalTicks;
        }
        else if (liftProcessState == LiftProcessState.Lowering)
        {
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


    // ============================================================
    // 【计算升降Tick数】
    // ============================================================
    // 根据物体属性计算升降所需Tick数
    //
    // 【参数说明】
    // - thing: 目标物体
    //
    // 【返回值】
    // - 计算出的Tick数（最小60）
    // ============================================================
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


    // ============================================================
    // 【播放灰尘特效】
    // ============================================================
    // 播放升降过程中的灰尘特效
    //
    // 【参数说明】
    // - burst: 是否为爆发效果
    // ============================================================
    private void ThrowLiftDustFleck(bool burst)
    {
        Map map = Map;
        if (map == null || !Spawned)
        {
            return;
        }

        Vector3 basePos = Position.ToVector3Shifted();
        float altitude = AltitudeLayer.MoteLow.AltitudeFor();

        // 统一参数设置
        int count = burst ? LiftBurstCount : 1;
        float radius = burst ? LiftBurstRadius : LiftFleckRadius;
        float scale = burst ? LiftBurstScale : LiftFleckScale;

        for (int i = 0; i < count; i++)
        {
            Vector3 drawPos = (basePos + Gen.RandomHorizontalVector(radius)).WithY(altitude);
            FleckMaker.ThrowDustPuff(drawPos, map, scale);
        }
    }


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
        cachedGroupComp?.RegisterAnimatingController(this);
        EnsureLiftBlocker();
        ThrowLiftDustFleck(burst: true);
    }


    // ============================================================
    // 【清理升降过程】
    // ============================================================
    // 清理升降过程状态并移除阻挡器
    // ============================================================
    internal void ClearLiftProcessAndRemoveBlocker()
    {
        DestroyLiftBlockerIfAny();

        liftProcessState = LiftProcessState.None;
        liftTicksRemaining = 0;
        liftTicksTotal = 0;
        liftBlockerCell = IntVec3.Invalid;
        liftFinalizeOnComplete = false;

        cachedGroupComp?.DeregisterAnimatingController(this);
        ApplyActivePowerInternal(active: false);
    }


    private void TryStartLoweringProcess(IntVec3 blockerCell, int ticks)
    {
        if (InLiftProcess)
        {
            return;
        }

        BeginLiftProcess(LiftProcessState.Lowering, blockerCell, ticks, finalizeOnComplete: false);
    }


    // ============================================================
    // 【尝试开始升起流程】
    // ============================================================
    // 尝试开始单一控制器的升起流程 (被组调用)
    //
    // 【参数说明】
    // - map: 地图
    //
    // 【返回值】
    // - true: 成功启动
    // ============================================================
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

        // 获取多格结构的所有相关控制器
        using var _ = new PooledHashSet<Building_WallController>(out var memberControllers);
        GetMultiCellMemberControllersOrSelf(map, memberControllers);

        // 检查所有成员是否都空闲
        foreach (Building_WallController member in memberControllers)
        {
            if (member == null || member.InLiftProcess)
            {
                return false;
            }
        }

        // 启动所有成员的升起流程
        foreach (Building_WallController member in memberControllers)
        {
            bool finalizeOnComplete = (member == this); // 仅主控者在完成后执行最终逻辑
            member.BeginLiftProcess(LiftProcessState.Raising, member.Position, ticksTotal, finalizeOnComplete);
        }

        return true;
    }


    private void EnsureLiftBlocker()
    {
        Map map = Map;
        if (map != null && liftBlockerCell.IsValid && liftBlockerCell.InBounds(map))
        {
            Building existing = map.edificeGrid[liftBlockerCell];
            if (existing != null && existing.def == ULS_ThingDefOf.ULS_LiftBlocker)
            {
                return;
            }

            GenSpawn.Spawn(ThingMaker.MakeThing(ULS_ThingDefOf.ULS_LiftBlocker), liftBlockerCell, map,
                WipeMode.VanishOrMoveAside);
        }
    }


    private void DestroyLiftBlockerIfAny()
    {
        Map map = Map;
        if (map != null && liftBlockerCell.IsValid && liftBlockerCell.InBounds(map))
        {
            Building blocker = map.edificeGrid[liftBlockerCell];
            if (blocker != null && blocker.def == ULS_ThingDefOf.ULS_LiftBlocker && !blocker.Destroyed)
            {
                blocker.Destroy();
            }
        }
    }


    protected override void Tick()
    {
        base.Tick();

        // 检查空闲状态下的电力消耗
        EnsureIdlePowerIfFeatureDisabled();

        if (!InLiftProcess)
        {
            return;
        }

        // 升降过程中的电力消耗与处理
        if (PowerFeatureEnabled)
        {
            if (!PowerOn)
            {
                HandlePowerLossDuringLift();
                return;
            }

            ApplyActivePowerInternal(active: true);
        }

        // 定期生成阻挡器，防止物体移动到升降区域
        if (liftTicksRemaining % 60 == 0)
        {
            EnsureLiftBlocker();
        }

        // 播放过程特效
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

        // 完成时的特效和逻辑
        ThrowLiftDustFleck(burst: true);

        LiftProcessState completedState = liftProcessState;
        bool shouldFinalize = liftFinalizeOnComplete;

        // 清理状态
        ClearLiftProcessAndRemoveBlocker();

        // 如果是升起过程结束，执行物体生成逻辑
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

            // 再次检查生成点是否被阻挡
            if (!IsBlockedForRaise(map, spawnCell, storedThing))
            {
                TryRaiseNoMessage(map); // 执行实际生成
            }
            else
            {
                Log.Warning("[ULS] 预期外行为: 结构控制器升起结构时在其上建造了结构");
            }
        }
    }
}