namespace Universal_Lift_Structure;

public class UniversalLiftStructureSettings : ModSettings
{
    public bool excludeNaturalRock = true;


    public List<string> defNameBlacklist = new();


    private HashSet<string> defNameBlacklistSet;


    public int groupMaxSize = 20;


    public bool showStoredGhostOverlay = true;


    public bool enableOverlayDisplay = true;


    public bool ShowControllerCell;


    public bool showAutoGroupDetectionProjection;


    public LiftControlMode liftControlMode = LiftControlMode.Remote;


    public float liftDurationHpSet = 1.0f;


    public float liftDurationMassSet = 1.0f;


    public bool enableLiftPower = true;


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
        Scribe_Values.Look(ref liftControlMode, "liftControlMode");
        Scribe_Values.Look(ref liftDurationHpSet, "liftDurationHpSet", 1.0f);
        Scribe_Values.Look(ref liftDurationMassSet, "liftDurationMassSet", 1.0f);
        Scribe_Values.Look(ref enableLiftPower, "enableLiftPower", true);

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
        showStoredGhostOverlay = true;
        enableOverlayDisplay = true;
        liftControlMode = LiftControlMode.Remote;
        liftDurationHpSet = 1.0f;
        liftDurationMassSet = 1.0f;
        enableLiftPower = true;
    }
}