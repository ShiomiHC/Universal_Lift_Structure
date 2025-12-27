namespace Universal_Lift_Structure;

/// 文件意图：Building_WallController - 电力系统管理。
/// 包含：电力相关字段、Flick 代理管理、功率控制方法、断电处理。
public partial class Building_WallController
{
    // ==================== 电力字段 ====================

    // Flick 代理：用于手动/控制台模式的开关触发
    private ThingWithComps flickProxy;
    private IntVec3 flickProxyCell = IntVec3.Invalid;

    // 电力常量
    private const float ActiveLiftPower = 1000f; // 升降过程额外功耗

    // 电力组件缓存
    private CompPowerTrader compPower;
    private float idlePowerConsumption;         // 空闲功耗
    private bool activePowerApplied;            // 是否已应用升降功耗

    // ==================== 电力属性 ====================

    /// 电力特性是否启用（从 Mod 设置读取）
    private bool PowerFeatureEnabled => UniversalLiftStructureMod.Settings?.enableLiftPower ?? false;

    /// 获取电力组件（延迟初始化）
    private CompPowerTrader PowerCompInternal
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

    /// 是否有电力组件
    private bool HasPowerComp => PowerCompInternal != null;

    /// 电力是否开启
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

    // ==================== 电力控制方法 ====================

    /// 刷新电力缓存和输出
    private void RefreshPowerCacheAndOutput()
    {
        CompPowerTrader comp = (compPower = GetComp<CompPowerTrader>());
        idlePowerConsumption = (comp != null) ? Mathf.Max(0f, comp.Props.PowerConsumption) : 0f;

        if (PowerFeatureEnabled && comp != null)
        {
            ApplyActivePowerInternal(InLiftProcess);
        }
    }

    /// 应用升降功耗（内部）
    /// <param name="active">true=升降中（高功耗），false=空闲（低功耗）</param>
    private void ApplyActivePowerInternal(bool active)
    {
        if (PowerFeatureEnabled)
        {
            CompPowerTrader comp = PowerCompInternal;
            if (comp != null)
            {
                float idlePower = idlePowerConsumption;
                comp.PowerOutput = active
                    ? (0f - (idlePower + ActiveLiftPower))  // 升降中：空闲功耗 + 升降功耗
                    : (0f - idlePower);                     // 空闲：只消耗空闲功耗
                activePowerApplied = active;
            }
        }
    }

    /// 确保电力特性禁用时恢复空闲功耗
    private void EnsureIdlePowerIfFeatureDisabled()
    {
        if (!PowerFeatureEnabled && activePowerApplied && HasPowerComp)
        {
            compPower.PowerOutput = 0f - idlePowerConsumption;
            activePowerApplied = false;
        }
    }

    /// 检查是否准备好进行升降（电力检查）
    private bool IsReadyForLiftPower()
    {
        if (!PowerFeatureEnabled)
        {
            return true; // 电力特性未启用，总是准备好
        }

        if (HasPowerComp)
        {
            return PowerOn; // 检查是否有电
        }

        return false; // 无电力组件
    }

    /// 处理升降过程中的断电
    private void HandlePowerLossDuringLift()
    {
        LiftProcessState previousState = liftProcessState;

        // 清理升降过程
        ClearLiftProcessAndRemoveBlocker();

        Map map = Map;
        if (map != null && previousState == LiftProcessState.Lowering && HasStored)
        {
            // 降下过程中断电：尝试自动升起（回滚）
            TryRaiseNoMessage(map);
        }

        MessageReject("ULS_PowerLost", this);
    }

    // ==================== Flick 代理管理 ====================

    /// 确保 Flick 代理存在（用于手动/控制台模式）
    private ThingWithComps EnsureFlickProxy()
    {
        Map map = Map;
        if (map == null)
        {
            return null;
        }

        flickProxyCell = Position;

        // 检查现有代理是否有效
        if (flickProxy != null && !flickProxy.Destroyed && flickProxy.Spawned)
        {
            return flickProxy;
        }

        // 尝试从格子上找到代理
        if (flickProxyCell.IsValid)
        {
            List<Thing> things = map.thingGrid.ThingsListAt(flickProxyCell);
            ThingWithComps firstProxy = null;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].def == ULS_ThingDefOf.ULS_FlickProxy)
                {
                    // 最后保险：清理冗余代理
                    // 这里保留第一个，删除其余，确保后续引用稳定。
                    if (firstProxy == null)
                    {
                        firstProxy = things[i] as ThingWithComps;
                        continue;
                    }

                    things[i].Destroy();
                }
            }

            if (firstProxy != null && !firstProxy.Destroyed)
            {
                flickProxy = firstProxy;
                return flickProxy;
            }
        }

        // 创建新代理
        ULS_FlickUtility.GetOrCreateFlickProxyTriggerAt(map, flickProxyCell);

        // 再次查找
        List<Thing> thingsAfterCreate = map.thingGrid.ThingsListAt(flickProxyCell);
        for (int j = 0; j < thingsAfterCreate.Count; j++)
        {
            if (thingsAfterCreate[j].def == ULS_ThingDefOf.ULS_FlickProxy)
            {
                flickProxy = thingsAfterCreate[j] as ThingWithComps;
                return flickProxy;
            }
        }

        return null;
    }

    /// 销毁 Flick 代理（如果存在）
    private void DestroyFlickProxyIfAny()
    {
        if (flickProxy != null && !flickProxy.Destroyed)
        {
            flickProxy.Destroy();
        }
        flickProxy = null;
    }

    /// 获取代理的 FlickTrigger 组件
    internal ULS_FlickTrigger GetProxyFlickTrigger()
    {
        return EnsureFlickProxy()?.GetComp<ULS_FlickTrigger>();
    }
}
