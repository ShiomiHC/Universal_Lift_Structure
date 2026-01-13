namespace Universal_Lift_Structure;

// ============================================================
// 【墙体控制器 - 电力系统部分】
// ============================================================
// 此 partial 类处理控制器的电力相关功能
//
// 【核心功能】
// 1. 管理电力组件的访问和缓存
// 2. 控制待机和激活状态的电力消耗
// 3. 处理升降过程中的电力检查和断电事件
//
// 【电力模式】
// - 待机功率：由建筑定义中的 PowerConsumption 指定
// - 激活功率：待机功率 + ActiveLiftPower（1000W）
//
// 【重要机制】
// - 电力功能可通过 Mod 设置启用/禁用
// - 启用时，升降过程中需要额外 1000W 电力
// - 断电时会中断升降过程，并尝试回升已降下的建筑
// ============================================================
public partial class Building_WallController
{
    // ============================================================
    // 【电力常量】
    // ============================================================

    // 激活状态的额外电力消耗（瓦特）
    // 用途：升降过程中需要额外消耗 1000W 电力
    private const float ActiveLiftPower = 1000f;

    // ============================================================
    // 【电力相关字段】
    // ============================================================

    // 电力交易组件（缓存）
    // 用途：避免重复调用 GetComp<CompPowerTrader>()
    private CompPowerTrader compPower;

    // 待机电力消耗值（缓存）
    // 用途：存储建筑定义中的基础电力消耗值
    private float idlePowerConsumption;

    // 是否已应用激活电力
    // 用途：追踪当前是否处于激活电力状态，避免重复设置
    private bool activePowerApplied;

    // 电力功能是否启用
    // 用途：读取 Mod 设置，判断是否需要额外电力消耗
    private bool PowerFeatureEnabled => UniversalLiftStructureMod.Settings?.enableLiftPower ?? false;

    // 电力组件是否已检查
    // 用途：延迟加载标志，避免重复获取组件
    private bool compPowerChecked;

    // ============================================================
    // 【电力相关属性】
    // ============================================================

    // 电力交易组件（延迟加载）
    // 用途：首次访问时获取并缓存 CompPowerTrader
    // 同时缓存待机电力消耗值
    public CompPowerTrader PowerTraderComp
    {
        get
        {
            if (!compPowerChecked)
            {
                // 获取电力组件
                compPower = GetComp<CompPowerTrader>();
                if (compPower != null)
                {
                    // 缓存待机电力消耗值（确保为正数）
                    idlePowerConsumption = Mathf.Max(0f, compPower.Props.PowerConsumption);
                }

                compPowerChecked = true;
            }

            return compPower;
        }
    }

    // 是否有电力组件
    // 用途：快速检查建筑是否有电力需求
    private bool HasPowerComp => PowerTraderComp != null;

    // 电力是否开启
    // 用途：检查电力网络是否为控制器供电
    // 返回：true = 有电，false = 无电或无电力组件
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

    // ============================================================
    // 【刷新电力缓存和输出】
    // ============================================================
    // 在 SpawnSetup 时调用，重新获取电力组件并设置正确的功率输出
    //
    // 【执行流程】
    // 1. 重新获取电力组件
    // 2. 缓存待机功率值
    // 3. 如果电力功能启用且正在升降，应用激活功率
    //
    // 【调用时机】
    // - SpawnSetup：建筑生成或加载时
    // - 需要强制刷新电力状态时
    //
    // 【注意事项】
    // - 会覆盖 comp Power 和 idlePowerConsumption 缓存
    // ============================================================
    private void RefreshPowerCacheAndOutput()
    {
        // 重新获取电力组件并缓存
        CompPowerTrader comp = (compPower = GetComp<CompPowerTrader>());
        idlePowerConsumption = (comp != null) ? Mathf.Max(0f, comp.Props.PowerConsumption) : 0f;

        // 如果电力功能启用且有电力组件，根据当前状态设置功率
        if (PowerFeatureEnabled && comp != null)
        {
            ApplyActivePowerInternal(InLiftProcess);
        }
    }

    // ============================================================
    // 【应用激活电力状态】
    // ============================================================
    // 设置控制器的功率输出为待机或激活状态
    //
    // 【参数说明】
    // - active: true = 激活状态（额外消耗 1000W），false = 待机状态
    //
    // 【功率计算】
    // - 待机状态：- idlePowerConsumption
    // - 激活状态：- (idlePowerConsumption + ActiveLiftPower)
    //
    // 【性能优化】
    // - 仅在功率值实际改变时才更新，避免频繁触发电力系统刷新
    //
    // 【调用时机】
    // - 升降过程开始时（切换到激活状态）
    // - 升降过程结束时（切换回待机状态）
    //
    // 【注意事项】
    // - 仅在 PowerFeatureEnabled = true 时生效
    // - 使用 Mathf.Abs 比较避免浮点数精度问题
    // ============================================================
    private void ApplyActivePowerInternal(bool active)
    {
        if (PowerFeatureEnabled)
        {
            CompPowerTrader comp = PowerTraderComp;
            if (comp != null)
            {
                // 计算目标功率输出
                float idlePower = idlePowerConsumption;
                float targetOutput = active ? (0f - (idlePower + ActiveLiftPower)) : (0f - idlePower);

                // 只有当实际输出需要改变时才更新，避免频繁触发电力系统刷新
                if (Mathf.Abs(comp.PowerOutput - targetOutput) > 0.01f)
                {
                    comp.PowerOutput = targetOutput;
                }

                // 更新激活状态标志
                activePowerApplied = active;
            }
        }
    }

    // ============================================================
    // 【确保待机电力（如果功能禁用）】
    // ============================================================
    // 当电力功能在运行时被禁用时，将功率输出恢复为待机状态
    //
    // 【调用时机】
    // - Mod 设置中禁用电力功能后
    // - 用于确保状态一致性
    //
    // 【执行条件】
    // - 电力功能已禁用
    // - 当前处于激活电力状态
    // - 有电力组件
    //
    // 【注意事项】
    // - 防止禁用功能后仍然消耗额外电力
    // ============================================================
    private void EnsureIdlePowerIfFeatureDisabled()
    {
        if (!PowerFeatureEnabled && activePowerApplied && HasPowerComp)
        {
            // 恢复为待机功率
            compPower.PowerOutput = 0f - idlePowerConsumption;
            activePowerApplied = false;
        }
    }

    // ============================================================
    // 【检查是否准备好升降电力】
    // ============================================================
    // 检查控制器是否满足电力需求以执行升降操作
    //
    // 【返回值】
    // - true: 有电力组件且当前有电
    // - false: 无电力组件或无电
    //
    // 【注意事项】
    // - 此方法检查的是额外电力需求，不阻止无电力组件的建筑升降
    // - 仅在 enableLiftPower = true 时，此检查才会影响升降操作
    // ============================================================
    public bool IsReadyForLiftPower()
    {
        // 电力功能是额外电力需求！
        // 检查电力组件
        if (!HasPowerComp) return false;
        return PowerOn;
    }

    // ============================================================
    // 【处理升降过程中的断电事件】
    // ============================================================
    // 当升降过程中失去电力时调用，中断过程并尝试恢复
    //
    // 【执行流程】
    // 1. 记录之前的升降状态
    // 2. 批量停止组内所有成员的升降过程
    // 3. 如果之前在降下且有存储物，尝试回升
    // 4. 显示断电消息
    //
    // 【恢复机制】
    // - 如果在降下过程中断电：
    //   - 清除升降流程
    //   - 尝试自动回升已降下的建筑（避免建筑丢失）
    //
    // - 如果在升起过程中断电：
    //   - 仅清除升降流程
    //
    // 【批量处理】
    // - 使用 GetMultiCellMemberControllersOrSelf 获取所有组成员
    // - 确保整个组的状态一致性
    //
    // 【调用时机】
    // - Tick 检测到电力关闭且正在升降过程中
    //
    // 【注意事项】
    // - 使用 PooledHashSet 避免内存分配
    // - 仅触发一个成员的回升，回升逻辑会自动同步到全组
    // ============================================================
    private void HandlePowerLossDuringLift()
    {
        // 记录之前的状态，用于判断是否需要回升
        LiftProcessState previousState = liftProcessState;
        Map map = Map;

        if (map != null)
        {
            // 批量停止组内所有成员，避免后续 Tick 重复触发该逻辑
            using var _ = new PooledHashSet<Building_WallController>(out var members);
            GetMultiCellMemberControllersOrSelf(map, members);

            // 清除所有成员的升降流程
            foreach (var member in members)
            {
                if (member.InLiftProcess)
                {
                    member.ClearLiftProcessAndRemoveBlocker();
                }
            }

            // 如果之前在降下且持有存储物，尝试触发其中一个成员回升（回升逻辑会自动同步到全组）
            if (previousState == LiftProcessState.Lowering)
            {
                foreach (var member in members)
                {
                    if (member.HasStored)
                    {
                        // 触发无消息回升，避免消息重复
                        member.TryRaiseNoMessage(map);
                        break;
                    }
                }
            }
        }
        else
        {
            // 如果 Map 为空，仅清除自身流程
            ClearLiftProcessAndRemoveBlocker();
        }

        // 显示断电消息
        MessageReject("ULS_PowerLost", this);
    }
}
