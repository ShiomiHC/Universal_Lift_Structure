namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    // 升降过程中额外消耗 1000W
    private const float ActiveLiftPower = 1000f;

    private CompPowerTrader compPower;
    private float idlePowerConsumption;
    private bool activePowerApplied;
    private bool PowerFeatureEnabled => UniversalLiftStructureMod.Settings?.enableLiftPower ?? false;
    private bool compPowerChecked;

    // 首次访问时获取并缓存 CompPowerTrader 及待机功率
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

                // 仅在实际值变化时更新，避免频繁触发电力系统刷新
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
        if (!HasPowerComp) return false;
        return PowerOn;
    }

    // 降下中断电时尝试回升已降下的建筑，防止建筑丢失；仅触发一个成员，回升逻辑自动同步全组
    private void HandlePowerLossDuringLift()
    {
        LiftProcessState previousState = liftProcessState;
        Map map = Map;

        if (map != null)
        {
            using var _ = new PooledHashSet<Building_WallController>(out var members);
            GetMultiCellMemberControllersOrSelf(map, members);

            foreach (var member in members)
            {
                if (member.InLiftProcess)
                {
                    member.ClearLiftProcessAndRemoveBlocker();
                }
            }

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

        // 显示断电消息
        MessageReject("ULS_PowerLost", this);
    }
}
