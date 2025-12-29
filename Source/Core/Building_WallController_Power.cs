namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    private const float ActiveLiftPower = 1000f;


    private CompPowerTrader compPower;
    private float idlePowerConsumption;
    private bool activePowerApplied;


    private bool PowerFeatureEnabled => UniversalLiftStructureMod.Settings?.enableLiftPower ?? false;


    public CompPowerTrader PowerTraderComp
    {
        get
        {
            if (compPower == null)
            {
                compPower = GetComp<CompPowerTrader>();
                if (compPower != null)
                {
                    idlePowerConsumption = Mathf.Max(0f, compPower.Props.PowerConsumption);
                }
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
                comp.PowerOutput = active
                    ? (0f - (idlePower + ActiveLiftPower))
                    : (0f - idlePower);
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


    private bool IsReadyForLiftPower()
    {
        if (!PowerFeatureEnabled)
        {
            return true;
        }

        if (HasPowerComp)
        {
            return PowerOn;
        }

        return false;
    }


    private void HandlePowerLossDuringLift()
    {
        LiftProcessState previousState = liftProcessState;


        ClearLiftProcessAndRemoveBlocker();

        Map map = Map;
        if (map != null && previousState == LiftProcessState.Lowering && HasStored)
        {
            TryRaiseNoMessage(map);
        }

        MessageReject("ULS_PowerLost", this);
    }
}
