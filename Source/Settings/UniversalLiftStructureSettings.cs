namespace Universal_Lift_Structure;

public class UniversalLiftStructureSettings : ModSettings
{
    // --- 过滤器设置 ---
    public bool excludeNaturalRock = true;
    public List<string> defNameBlacklist = new();

    // defNameBlacklist 的 HashSet 缓存，不序列化；O(1) 查找
    private HashSet<string> defNameBlacklistSet;

    // --- 核心设置 ---
    public int groupMaxSize = 20;

    // --- 视觉设置 ---
    public bool showStoredGhostOverlay = true;
    public bool enableOverlayDisplay = true;
    public bool ShowControllerCell;
    public bool showAutoGroupDetectionProjection;
    public bool hideControllerWhenStored; // 降下时隐藏控制器（显露地面）

    public LiftControlMode liftControlMode = LiftControlMode.Console;

    // --- 性能设置 ---
    public float liftDurationHpSet = 1.0f;
    public float liftDurationMassSet = 1.0f;

    public bool enableLiftPower = true;
    public bool lowerButtonOnController; // 控制器本体也显示降下按钮（兼容其他 Mod 时的备用入口）

    // 令敌人 AI 忽略升降控制器（通过将 useHitPoints 置 false 实现免疫）；启动时由 LongEventHandler 重新应用到 def
    public bool enemiesIgnoreLiftController;

    // 子选项，仅 enemiesIgnoreLiftController 开启时生效
    public bool enemiesIgnoreLiftConsole;

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref excludeNaturalRock, "excludeNaturalRock", true);
        Scribe_Collections.Look(ref defNameBlacklist, "defNameBlacklist", LookMode.Value);
        Scribe_Values.Look(ref groupMaxSize, "groupMaxSize", 20);
        Scribe_Values.Look(ref showStoredGhostOverlay, "showStoredGhostOverlay", true);
        Scribe_Values.Look(ref enableOverlayDisplay, "enableOverlayDisplay", true);
        Scribe_Values.Look(ref ShowControllerCell, "ShowControllerCell");
        Scribe_Values.Look(ref showAutoGroupDetectionProjection, "showAutoGroupDetectionProjection");
        Scribe_Values.Look(ref hideControllerWhenStored, "hideControllerWhenStored");
        Scribe_Values.Look(ref liftControlMode, "liftControlMode");
        Scribe_Values.Look(ref liftDurationHpSet, "liftDurationHpSet", 1.0f);
        Scribe_Values.Look(ref liftDurationMassSet, "liftDurationMassSet", 1.0f);
        Scribe_Values.Look(ref enableLiftPower, "enableLiftPower", true);
        Scribe_Values.Look(ref lowerButtonOnController, "lowerButtonOnController");
        Scribe_Values.Look(ref enemiesIgnoreLiftController, "enemiesIgnoreLiftController");
        Scribe_Values.Look(ref enemiesIgnoreLiftConsole, "enemiesIgnoreLiftConsole");

        if (Scribe.mode is LoadSaveMode.PostLoadInit && defNameBlacklist is null)
        {
            defNameBlacklist = new();
        }

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

    // 空字符串视为已在黑名单中（安全默认）
    public bool IsDefNameBlacklisted(string defName)
    {
        if (defName.NullOrEmpty())
        {
            return true;
        }

        EnsureBlacklistCache();
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


    public void ResetToDefault()
    {
        excludeNaturalRock = true;
        defNameBlacklist.Clear();
        defNameBlacklistSet?.Clear();

        groupMaxSize = 20;
        liftControlMode = LiftControlMode.Console;
        enableLiftPower = true;
        lowerButtonOnController = false;

        enemiesIgnoreLiftController = false;
        enemiesIgnoreLiftConsole = false;

        showStoredGhostOverlay = true;
        enableOverlayDisplay = true;
        ShowControllerCell = false;
        showAutoGroupDetectionProjection = false;

        liftDurationHpSet = 1.0f;
        liftDurationMassSet = 1.0f;
    }
}