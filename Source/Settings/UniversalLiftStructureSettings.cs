namespace Universal_Lift_Structure;

// ============================================================
// 【Mod 设置数据类】
// ============================================================
// 此类负责存储和管理所有的 Mod 配置选项
//
// 【继承关系】
// - 继承自 ModSettings：RimWorld 的设置基类，提供序列化支持
//
// 【核心职责】
// 1. 数据持久化：通过 ExposeData() 序列化/反序列化设置到 XML 文件
// 2. 黑名单管理：提供添加、删除、查询黑名单的 API
// 3. 缓存优化：使用 HashSet 缓存黑名单以提高查询性能
//
// 【设置分类】
// - 过滤器设置：excludeNaturalRock, defNameBlacklist
// - 核心设置：groupMaxSize, liftControlMode, enableLiftPower
// - 视觉设置：showStoredGhostOverlay, enableOverlayDisplay 等
// - 性能设置：liftDurationHpSet, liftDurationMassSet
//
// 【序列化机制】
// - 可序列化字段：public 字段（会被保存到 XML）
// - 不可序列化字段：private 字段（仅作为运行时缓存）
// - 序列化方法：ExposeData() - 处理保存和加载逻辑
//
// 【使用方式】
// - 通过 UniversalLiftStructureMod.Settings 静态属性访问
// - 例如：UniversalLiftStructureMod.Settings.groupMaxSize
// ============================================================

// 继承自 ModSettings，可持久化的设置类
public class UniversalLiftStructureSettings : ModSettings
{
    // ============================================================
    // 【字段说明】
    // ============================================================
    // public 字段：会被序列化保存到 XML 文件
    // private 字段：仅作为运行时缓存，不会被保存
    // 所有需要保存的字段必须在 ExposeData() 中注册
    // ============================================================

    // --- 过滤器设置 ---
    public bool excludeNaturalRock = true; // 是否排除天然岩石（默认 true）


    public List<string> defNameBlacklist = new(); // 黑名单列表（需要序列化）


    // 【私有缓存字段】
    // 这个字段不会被序列化保存！
    // 它只是 defNameBlacklist 的 HashSet 版本，用于快速查找
    // 为什么用 HashSet？因为 List.Contains() 是 O(n)，而 HashSet.Contains() 是 O(1)
    private HashSet<string> defNameBlacklistSet;


    // --- 核心设置 ---
    public int groupMaxSize = 20; // 分组最大尺寸


    // --- 视觉设置 ---
    public bool showStoredGhostOverlay = true; // 显示收纳建筑虚影


    public bool enableOverlayDisplay = true; // 启用覆盖层显示


    public bool ShowControllerCell; // 显示控制器单元格（默认 false，所以不需要写 = false）


    public bool showAutoGroupDetectionProjection; // 显示自动分组检测投影


    // 【枚举字段】
    // LiftControlMode 是自定义枚举（定义在 LiftControlMode.cs 中）
    // 枚举会被序列化为其名称字符串
    public LiftControlMode liftControlMode = LiftControlMode.Console;


    // --- 性能设置 ---
    public float liftDurationHpSet = 1.0f; // HP 时长系数


    public float liftDurationMassSet = 1.0f; // 质量时长系数


    public bool enableLiftPower = true; // 启用升降功率消耗


    // ============================================================
    // 【ExposeData() 方法】★★★ 核心重点 ★★★
    // ============================================================
    // 这是 Scribe 序列化系统的入口点。
    //
    // 【调用时机】
    // - 保存设置时（Scribe.mode == LoadSaveMode.Saving）
    // - 加载设置时（Scribe.mode == LoadSaveMode.LoadingVars）
    // - 加载完成后（Scribe.mode == LoadSaveMode.PostLoadInit）
    //
    // 【工作原理】
    // - 保存时：Scribe 读取字段值并写入 XML
    // - 加载时：Scribe 从 XML 读取值并赋给字段
    // - 同一个方法处理两种情况！（通过 ref 参数实现）
    // ============================================================
    public override void ExposeData()
    {
        // 【重要】必须调用基类的 ExposeData()
        // 基类可能有自己需要序列化的数据
        base.ExposeData();

        Scribe_Values.Look(ref excludeNaturalRock, "excludeNaturalRock", true);
        Scribe_Collections.Look(ref defNameBlacklist, "defNameBlacklist", LookMode.Value);
        Scribe_Values.Look(ref groupMaxSize, "groupMaxSize", 20);
        Scribe_Values.Look(ref showStoredGhostOverlay, "showStoredGhostOverlay", true);
        Scribe_Values.Look(ref enableOverlayDisplay, "enableOverlayDisplay", true);
        Scribe_Values.Look(ref ShowControllerCell, "ShowControllerCell");
        Scribe_Values.Look(ref showAutoGroupDetectionProjection, "showAutoGroupDetectionProjection");
        Scribe_Values.Look(ref liftControlMode, "liftControlMode");
        Scribe_Values.Look(ref liftDurationHpSet, "liftDurationHpSet", 1.0f);
        Scribe_Values.Look(ref liftDurationMassSet, "liftDurationMassSet", 1.0f);
        Scribe_Values.Look(ref enableLiftPower, "enableLiftPower", true);

        // 加载完成后初始化
        // 【Scribe.mode 的所有值】
        // - Inactive：未进行序列化操作
        // - Saving：正在保存
        // - LoadingVars：正在加载变量
        // - ResolvingCrossRefs：正在解析交叉引用（如 Def 引用）
        // - PostLoadInit：加载完成后的初始化
        // ============================================================
        if (Scribe.mode is LoadSaveMode.PostLoadInit && defNameBlacklist is null)
        {
            // 防御性编程：如果加载后列表为 null，创建新的空列表
            defNameBlacklist = new();
        }


        if (Scribe.mode is LoadSaveMode.PostLoadInit)
        {
            // 【加载后处理】
            // 1. 清理无效条目（如已删除的 Mod 中的 defName）
            // 2. 重建 HashSet 缓存以提高查找性能
            CleanupAndNormalizeBlacklist();
            RebuildBlacklistCache();
        }

        if (Scribe.mode is LoadSaveMode.PostLoadInit && groupMaxSize < 1)
        {
            // 【数据验证】确保 groupMaxSize 至少为 1
            groupMaxSize = 20;
        }
    }


    // ============================================================
    // 【工具方法区域】
    // ============================================================
    // 以下方法用于管理黑名单的增删查操作。
    //
    // 【缓存策略】
    // - defNameBlacklist（List）：用于序列化保存
    // - defNameBlacklistSet（HashSet）：用于快速查询
    // - 两者必须保持同步！
    // ============================================================


    /// 检查某个 defName 是否在黑名单中
    public bool IsDefNameBlacklisted(string defName)
    {
        // 空字符串视为在黑名单中（安全处理）
        if (defName.NullOrEmpty())
        {
            return true;
        }

        // 【惰性初始化】确保缓存已创建
        EnsureBlacklistCache();
        // 使用 HashSet 进行 O(1) 查找
        return defNameBlacklistSet.Contains(defName);
    }


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


    public void CleanupAndNormalizeBlacklist()
    {
        if (defNameBlacklist is null)
        {
            defNameBlacklist = new();
            return;
        }


        for (int i = defNameBlacklist.Count - 1; i >= 0; i--)
        {
            string defName = defNameBlacklist[i];
            if (defName.NullOrEmpty() || DefDatabase<ThingDef>.GetNamedSilentFail(defName) is null)
            {
                defNameBlacklist.RemoveAt(i);
            }
        }


        defNameBlacklist = defNameBlacklist
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }


    public void RebuildBlacklistCache()
    {
        defNameBlacklistSet =
            new HashSet<string>(defNameBlacklist ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
    }


    private void EnsureBlacklistCache()
    {
        if (defNameBlacklistSet != null)
        {
            return;
        }

        RebuildBlacklistCache();
    }


    /// 重置所有设置项为默认值
    public void ResetToDefault()
    {
        // 过滤器设置 | Filter Settings
        excludeNaturalRock = true; // 默认排除天然岩石 | Default: exclude natural rock
        defNameBlacklist.Clear(); // 清空黑名单列表 | Clear blacklist
        defNameBlacklistSet?.Clear(); // 清空黑名单缓存 | Clear blacklist cache

        // 核心设置 | Core Settings
        groupMaxSize = 20; // 默认组最大尺寸：20 | Default group max size: 20
        liftControlMode = LiftControlMode.Console; // 默认控制模式：控制台 | Default control mode: Console
        enableLiftPower = true; // 默认启用升降功率消耗 | Default: enable lift power consumption

        // 视觉设置 | Visual Settings
        showStoredGhostOverlay = true; // 默认显示收纳的建筑虚影 | Default: show stored ghost overlay
        enableOverlayDisplay = true; // 默认启用覆盖层显示 | Default: enable overlay display
        ShowControllerCell = false; // 默认不显示控制器单元格 | Default: hide controller cell
        showAutoGroupDetectionProjection = false; // 默认不显示自动分组检测投影 | Default: hide auto-group detection projection

        // 性能设置 | Performance Settings
        liftDurationHpSet = 1.0f; // 默认生命值时长系数：1.0 | Default HP duration multiplier: 1.0
        liftDurationMassSet = 1.0f; // 默认质量时长系数：1.0 | Default mass duration multiplier: 1.0
    }
}