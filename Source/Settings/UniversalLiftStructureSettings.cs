namespace Universal_Lift_Structure;


/// 文件意图：Mod 设置数据结构（ModSettings）。用于控制"结构侧降下 Gizmo"的注入范围：
/// 排除自然岩壁、排除门、按 ThingDef.defName 的 DefName 过滤（对外以白名单呈现，内部以黑名单存储）、组规模上限、以及收纳虚影显示开关。
/// 内部使用 HashSet 缓存黑名单以优化查询性能。

public class UniversalLiftStructureSettings : ModSettings
{
     // 是否排除自然岩壁（默认 true）。
    public bool excludeNaturalRock = true;

     // 是否排除门（默认 true）。
    public bool excludeDoors = true;

     // 内部黑名单列表（List），对外以白名单方式呈现。
    public List<string> defNameBlacklist = new();

     // 运行时 HashSet 缓存，不参与序列化，用于加速 O(1) 查询。
    private HashSet<string> defNameBlacklistSet;

     // 组升起/降下的可操作规模阈值（默认 20）。注意：不限制分组本身的成员数量。
    public int groupMaxSize = 20;

     // 是否显示收纳结构虚影（类蓝图 ghost）的可视化提示（默认 true）。
    public bool showStoredGhostOverlay = true;

    // 总开关：是否启用“叠加层显示”功能组（默认 true）。
    // 说明：具体叠加层是否绘制仍取决于各子项开关；实际生效条件为“总开关=true 且子项=true”。
    public bool enableOverlayDisplay = true;

    // 是否显示“控制器所在地块描边”（MetaOverlays 边框）。
    // 默认 false：避免默认 UI 噪音；由右下角 Overlay Toggle 控制。
    public bool ShowControllerCell = false;

    // 是否显示“自动控制器检测区域投影”（MetaOverlays 区域提示）。
    // 默认 false：避免默认 UI 噪音；由右下角 Overlay Toggle 控制。
    public bool showAutoGroupDetectionProjection = false;

    
    /// 升降控制模式（默认 Remote）：
    /// - Remote：Gizmo 直接触发
    /// - Console：要求控制台 flick
    /// - Manual：要求控制器本体 flick
    
    public LiftControlMode liftControlMode = LiftControlMode.Remote;

    
    /// 升降耗时系数（HP 部分，默认 1.0）。
    /// - 公式：ticks = round(MaxHP * 0.2 * liftDurationHpSet + Mass * 50 * liftDurationMassSet)
    /// - 允许为 0（即禁用该项贡献），但整体仍会受最小 tick 钳制。
    
    public float liftDurationHpSet = 1.0f;

    
    /// 升降耗时系数（Mass 部分，默认 1.0）。
    /// - 公式：ticks = round(MaxHP * 0.2 * liftDurationHpSet + Mass * 50 * liftDurationMassSet)
    /// - 允许为 0（即禁用该项贡献），但整体仍会受最小 tick 钳制。
    
    public float liftDurationMassSet = 1.0f;

    // 是否启用升降耗电/断电回退逻辑（默认启用）。关闭时不做缺电检查、不追加耗电、不在断电时回退。
    public bool enableLiftPower = true;

    
    /// 方法意图：负责设置项的存档读写。在 PostLoadInit 阶段执行以下操作：
    /// - 确保 defNameBlacklist 非空，避免旧档/异常状态导致 NRE。
    /// - 调用 CleanupAndNormalizeBlacklist() 清理无效 defName（mod 卸载/改名）并去重排序。
    /// - 调用 RebuildBlacklistCache() 重建 HashSet 缓存。
    /// - 确保 groupMaxSize 至少为 1。
    
    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref excludeNaturalRock, "excludeNaturalRock", true);
        Scribe_Values.Look(ref excludeDoors, "excludeDoors", true);
        Scribe_Collections.Look(ref defNameBlacklist, "defNameBlacklist", LookMode.Value);
        Scribe_Values.Look(ref groupMaxSize, "groupMaxSize", 20);
        Scribe_Values.Look(ref showStoredGhostOverlay, "showStoredGhostOverlay", true);
        Scribe_Values.Look(ref enableOverlayDisplay, "enableOverlayDisplay", true);
        Scribe_Values.Look(ref ShowControllerCell, "ShowControllerCell", false);
        Scribe_Values.Look(ref showAutoGroupDetectionProjection, "showAutoGroupDetectionProjection", false);
        Scribe_Values.Look(ref liftControlMode, "liftControlMode");
        Scribe_Values.Look(ref liftDurationHpSet, "liftDurationHpSet", 1.0f);
        Scribe_Values.Look(ref liftDurationMassSet, "liftDurationMassSet", 1.0f);
        Scribe_Values.Look(ref enableLiftPower, "enableLiftPower", true);

        if (Scribe.mode is LoadSaveMode.PostLoadInit && defNameBlacklist is null)
        {
            defNameBlacklist = new();
        }

        // PostLoad：清理无效 defName（mod 卸载/改名/版本变化）并去重排序
        if (Scribe.mode is LoadSaveMode.PostLoadInit)
        {
            CleanupAndNormalizeBlacklist();
            RebuildBlacklistCache();
        }

        if (Scribe.mode is LoadSaveMode.PostLoadInit && groupMaxSize < 1)
        {
            groupMaxSize = 20;
        }
    }

        
        /// 方法意图：判定 defName 是否在黑名单（内部未允许列表）。通过 HashSet 缓存实现 O(1) 查询，空字符串视为黑名单。
        
        public bool IsDefNameBlacklisted(string defName)
        {
            if (defName.NullOrEmpty())
            {
                return true;
            }

            EnsureBlacklistCache();
            return defNameBlacklistSet.Contains(defName);
        }

        
        /// 方法意图：尝试添加 defName 到黑名单（内部未允许列表）。同时更新 List 和 HashSet，返回是否发生变化。
        
        public bool AddDefNameToBlacklist(string defName)
        {
            if (defName.NullOrEmpty())
            {
                return false;
            }

            EnsureBlacklistCache();
            if (defNameBlacklistSet.Add(defName))
            {
                defNameBlacklist.Add(defName);
                return true;
            }

            return false;
        }

        
        /// 方法意图：尝试从黑名单（内部未允许列表）移除 defName。同时更新 List 和 HashSet，返回是否发生变化。
        
        public bool RemoveDefNameFromBlacklist(string defName)
        {
            if (defName.NullOrEmpty())
            {
                return false;
            }

            EnsureBlacklistCache();
            if (defNameBlacklistSet.Remove(defName))
            {
                defNameBlacklist.Remove(defName);
                return true;
            }

            return false;
        }

        
        /// 方法意图：批量添加 defName 到黑名单。用于设置界面中按组操作，返回是否发生变化。
        
        public bool AddDefNamesToBlacklist(IEnumerable<string> defNames)
        {
            if (defNames is null)
            {
                return false;
            }

            EnsureBlacklistCache();
            bool changed = false;
            foreach (string defName in defNames)
            {
                if (defName.NullOrEmpty())
                {
                    continue;
                }

                if (defNameBlacklistSet.Add(defName))
                {
                    defNameBlacklist.Add(defName);
                    changed = true;
                }
            }

            return changed;
        }

        
        /// 方法意图：批量从黑名单移除 defName。用于设置界面中按组操作，返回是否发生变化。
        
        public bool RemoveDefNamesFromBlacklist(IEnumerable<string> defNames)
        {
            if (defNames is null)
            {
                return false;
            }

            EnsureBlacklistCache();
            bool changed = false;
            foreach (string defName in defNames)
            {
                if (defName.NullOrEmpty())
                {
                    continue;
                }

                if (defNameBlacklistSet.Remove(defName))
                {
                    defNameBlacklist.Remove(defName);
                    changed = true;
                }
            }

            return changed;
        }

        
        /// 方法意图：清理黑名单中的无效条目（空字符串或 DefDatabase 中不存在的 defName），并去重排序。在 PostLoadInit 阶段自动调用，确保数据一致性。
        
        public void CleanupAndNormalizeBlacklist()
        {
            if (defNameBlacklist is null)
            {
                defNameBlacklist = new();
                return;
            }

            // 移除无效条目
            for (int i = defNameBlacklist.Count - 1; i >= 0; i--)
            {
                string defName = defNameBlacklist[i];
                if (defName.NullOrEmpty() || DefDatabase<ThingDef>.GetNamedSilentFail(defName) is null)
                {
                    defNameBlacklist.RemoveAt(i);
                }
            }

            // 去重排序
            defNameBlacklist = defNameBlacklist
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
        }

        
        /// 方法意图：从 defNameBlacklist 列表重建 defNameBlacklistSet 缓存。在 PostLoadInit 和缓存失效时调用。
        
        public void RebuildBlacklistCache()
        {
            defNameBlacklistSet = new HashSet<string>(defNameBlacklist ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        }

        
        /// 方法意图：惰性初始化黑名单缓存。若缓存不存在则调用 RebuildBlacklistCache()，确保查询方法始终有可用缓存。
        
        private void EnsureBlacklistCache()
        {
            if (defNameBlacklistSet != null)
            {
                return;
            }

            RebuildBlacklistCache();
        }

        
        /// 方法意图：将设置恢复到预设默认值：
        /// - excludeNaturalRock = true
        /// - excludeDoors = true
        /// - defNameBlacklist 清空（即白名单全允许）
        /// - defNameBlacklistSet 清空
        /// - groupMaxSize = 20
        /// - showStoredGhostOverlay = true
        /// - enableOverlayDisplay = true
        /// - liftControlMode = Remote
        /// - enableLiftPower = true
        
        public void ResetToDefault()
        {
            excludeNaturalRock = true;
            excludeDoors = true;
            defNameBlacklist.Clear();
            defNameBlacklistSet?.Clear();
            groupMaxSize = 20;
            showStoredGhostOverlay = true;
            enableOverlayDisplay = true;
            liftControlMode = LiftControlMode.Remote;
            liftDurationHpSet = 1.0f;
            liftDurationMassSet = 1.0f;
            enableLiftPower = true;
        }
    }
