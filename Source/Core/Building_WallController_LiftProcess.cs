namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    private enum LiftProcessState
    {
        None,
        Raising,
        Lowering
    }


    private LiftProcessState liftProcessState;


    private int liftTicksRemaining;


    private int liftTicksTotal;


    private IntVec3 liftBlockerCell = IntVec3.Invalid;


    private bool liftFinalizeOnComplete;


    private const int LiftFleckIntervalTicks = 20;
    private const float LiftFleckRadius = 0.7f;
    private const float LiftFleckScale = 1f;
    private const int LiftBurstCount = 6;
    private const float LiftBurstRadius = 1.3f;
    private const float LiftBurstScale = 1.3f;


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
        EnsureLiftBlocker();
        ThrowLiftDustFleck(burst: true);
    }


    internal void ClearLiftProcessAndRemoveBlocker()
    {
        DestroyLiftBlockerIfAny();

        liftProcessState = LiftProcessState.None;
        liftTicksRemaining = 0;
        liftTicksTotal = 0;
        liftBlockerCell = IntVec3.Invalid;
        liftFinalizeOnComplete = false;

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


        using var _ = new PooledHashSet<Building_WallController>(out var memberControllers);
        GetMultiCellMemberControllersOrSelf(map, memberControllers);


        foreach (Building_WallController member in memberControllers)
        {
            if (member == null || member.InLiftProcess)
            {
                return false;
            }
        }


        foreach (Building_WallController member in memberControllers)
        {
            bool finalizeOnComplete = (member == this);
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


        EnsureIdlePowerIfFeatureDisabled();

        if (!InLiftProcess)
        {
            return;
        }


        if (PowerFeatureEnabled)
        {
            if (!PowerOn)
            {
                HandlePowerLossDuringLift();
                return;
            }

            ApplyActivePowerInternal(active: true);
        }


        if (liftTicksRemaining % 60 == 0)
        {
            EnsureLiftBlocker();
        }


        int elapsed = liftTicksTotal - liftTicksRemaining;
        if (elapsed > 0 && elapsed % LiftFleckIntervalTicks == 0)
        {
            ThrowLiftDustFleck(burst: false);
        }


        liftTicksRemaining--;
        if (liftTicksRemaining > 0)
        {
            return;
        }


        ThrowLiftDustFleck(burst: true);

        LiftProcessState completedState = liftProcessState;
        bool shouldFinalize = liftFinalizeOnComplete;


        ClearLiftProcessAndRemoveBlocker();


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


            if (!IsBlockedForRaise(map, spawnCell, storedThing))
            {
                TryRaiseNoMessage(map);
            }
            else
            {
                Log.Warning("[ULS] 预期外行为: 结构控制器升起结构时在其上建造了结构");
            }
        }
    }
}