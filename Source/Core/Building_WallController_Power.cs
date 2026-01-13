namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    private const float ActiveLiftPower = 1000f;


    private CompPowerTrader compPower;
    private float idlePowerConsumption;
    private bool activePowerApplied;


    private bool PowerFeatureEnabled => UniversalLiftStructureMod.Settings?.enableLiftPower ?? false;


    private bool compPowerChecked;

    public CompPowerTrader PowerTraderComp
    {
        get
        {
            if (!compPowerChecked)
            {
                compPower = GetComp<CompPowerTrader>();
                if (compPower != null)
                {
                    idlePowerConsumption = Mathf.Max(0f, compPower.Props.PowerConsumption);
                }

                compPowerChecked = true;
            }

            return compPower;
        }
    }


    private bool HasPowerComp => PowerTraderComp != null;


    private bool PowerOn
    {
        get
        {
            if (HasPowerComp)
            {
                return compPower.PowerOn;
            }

            return false;
        }
    }


    private void RefreshPowerCacheAndOutput()
    {
        CompPowerTrader comp = (compPower = GetComp<CompPowerTrader>());
        idlePowerConsumption = (comp != null) ? Mathf.Max(0f, comp.Props.PowerConsumption) : 0f;

        if (PowerFeatureEnabled && comp != null)
        {
            ApplyActivePowerInternal(InLiftProcess);
        }
    }


    private void ApplyActivePowerInternal(bool active)
    {
        if (PowerFeatureEnabled)
        {
            CompPowerTrader comp = PowerTraderComp;
            if (comp != null)
            {
                float idlePower = idlePowerConsumption;
                float targetOutput = active ? (0f - (idlePower + ActiveLiftPower)) : (0f - idlePower);

                // 只有当实际输出需要改变时才更新，避免频繁触发布局系统的电力刷新
                if (Mathf.Abs(comp.PowerOutput - targetOutput) > 0.01f)
                {
                    comp.PowerOutput = targetOutput;
                }

                activePowerApplied = active;
            }
        }
    }


    private void EnsureIdlePowerIfFeatureDisabled()
    {
        if (!PowerFeatureEnabled && activePowerApplied && HasPowerComp)
        {
            compPower.PowerOutput = 0f - idlePowerConsumption;
            activePowerApplied = false;
        }
    }


    public bool IsReadyForLiftPower()
    {
        // 电力功能是额外电力需求!
        // 检查电力组件
        if (!HasPowerComp) return false;
        return PowerOn;
    }


    private void HandlePowerLossDuringLift()
    {
        LiftProcessState previousState = liftProcessState;
        Map map = Map;

        if (map != null)
        {
            // 批量停止组内所有成员，避免后续 Tick 重复触发该逻辑
            using var _ = new PooledHashSet<Building_WallController>(out var members);
            GetMultiCellMemberControllersOrSelf(map, members);
            foreach (var member in members)
            {
                if (member.InLiftProcess)
                {
                    member.ClearLiftProcessAndRemoveBlocker();
                }
            }

            // 如果之前在下降且持有存储物，尝试触发其中一个成员回升（回升逻辑会自动同步到全组）
            if (previousState == LiftProcessState.Lowering)
            {
                foreach (var member in members)
                {
                    if (member.HasStored)
                    {
                        member.TryRaiseNoMessage(map);
                        break;
                    }
                }
            }
        }
        else
        {
            ClearLiftProcessAndRemoveBlocker();
        }

        MessageReject("ULS_PowerLost", this);
    }
}
