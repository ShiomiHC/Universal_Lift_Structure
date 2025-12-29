namespace Universal_Lift_Structure;

public class UniversalLiftStructureMod : Mod
{
    private const string HarmonyId = "shiomi.UniversalLiftStructure";


    private enum SettingsTab
    {
        General,
        Filter
    }

    private enum GeneralSection
    {
        Core,
        Visual,
        Performance
    }

    public static UniversalLiftStructureSettings Settings;

    private SettingsTab currentTab = SettingsTab.General;
    private GeneralSection currentSection = GeneralSection.Core;


    private string selectedModPackageId;
    private string selectedModName;

    private string blacklistSearch = string.Empty;
    private Vector2 blacklistTreeScrollPosition = Vector2.zero;
    private readonly HashSet<string> expandedThingClassKeys = new(StringComparer.Ordinal);

    private string groupMaxSizeBuffer = string.Empty;


    public UniversalLiftStructureMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<UniversalLiftStructureSettings>();

        Harmony harmony = new(HarmonyId);
        harmony.PatchAll();
    }

    public override string SettingsCategory()
    {
        return "ULS_SettingsCategory".Translate();
    }


    public override void DoSettingsWindowContents(Rect inRect)
    {
        Rect tabBaseRect = new(inRect.x, inRect.y + TabDrawer.TabHeight, inRect.width,
            inRect.height - TabDrawer.TabHeight);
        List<TabRecord> tabs = new()
        {
            new("ULS_Tab_General".Translate(), () => currentTab = SettingsTab.General,
                () => currentTab == SettingsTab.General),
            new("ULS_Tab_Filter".Translate(), () => currentTab = SettingsTab.Filter,
                () => currentTab == SettingsTab.Filter)
        };
        TabDrawer.DrawTabs(tabBaseRect, tabs);

        Listing_Standard listing = new();
        listing.Begin(tabBaseRect);

        if (currentTab is SettingsTab.General)
        {
            Rect contentRect = new Rect(tabBaseRect.x, tabBaseRect.y, tabBaseRect.width, tabBaseRect.height);

            // Left Navigation Column (160px width)
            float leftNavWidth = 160f;
            float gap = 10f;
            Rect leftNavRect = new Rect(contentRect.x, contentRect.y, leftNavWidth, contentRect.height);
            Rect rightContentRect = new Rect(contentRect.x + leftNavWidth + gap, contentRect.y,
                contentRect.width - leftNavWidth - gap, contentRect.height);

            // Draw Left Navigation
            Listing_Standard navListing = new Listing_Standard();
            navListing.Begin(leftNavRect);

            DrawSectionButton(navListing, GeneralSection.Core, "ULS_Section_Core".Translate());
            DrawSectionButton(navListing, GeneralSection.Visual, "ULS_Section_Visual".Translate());
            DrawSectionButton(navListing, GeneralSection.Performance, "ULS_Section_Performance".Translate());

            navListing.End();

            // Draw Right Content
            Listing_Standard listingSettings = new Listing_Standard();
            listingSettings.Begin(rightContentRect);

            switch (currentSection)
            {
                case GeneralSection.Core:
                    DrawCoreSection(listingSettings);
                    break;
                case GeneralSection.Visual:
                    DrawVisualSection(listingSettings);
                    break;
                case GeneralSection.Performance:
                    DrawPerformanceSection(listingSettings);
                    break;
            }

            listingSettings.GapLine();
            if (listingSettings.ButtonText("ULS_Settings_ResetToDefault".Translate()))
            {
                Settings.ResetToDefault();
                Settings.Write();
                groupMaxSizeBuffer = Settings.groupMaxSize.ToString();
            }

            listingSettings.End();

            // End the main listing started outside (if any, though here we replaced the whole block)
            listing.End();
            return;
        }


        listing.Label("ULS_Settings_DefNameBlacklist".Translate());

        listing.GapLine();
        listing.Label("ULS_Settings_BuiltInRules".Translate());


        bool allowNaturalRock = !Settings.excludeNaturalRock;
        listing.CheckboxLabeled("ULS_Settings_AllowNaturalRock".Translate(), ref allowNaturalRock);
        Settings.excludeNaturalRock = !allowNaturalRock;

        listing.GapLine();


        Rect searchRow = listing.GetRect(Text.LineHeight);
        Rect searchLabelRect = new(searchRow.x, searchRow.y, 60f, searchRow.height);
        const float modButtonWidth = 160f;
        Rect modButtonRect = new(searchRow.xMax - modButtonWidth, searchRow.y, modButtonWidth, searchRow.height);
        Rect searchFieldRect = new(searchLabelRect.xMax + 6f, searchRow.y,
            modButtonRect.xMin - (searchLabelRect.xMax + 6f) - 6f - 26f, searchRow.height);
        Rect searchClearRect = new(modButtonRect.xMin - 24f, searchRow.y, 24f, 24f);

        Widgets.Label(searchLabelRect, "ULS_Settings_Search".Translate());
        blacklistSearch = Widgets.TextField(searchFieldRect, blacklistSearch);
        if (Widgets.ButtonImage(searchClearRect, TexButton.CloseXSmall))
        {
            blacklistSearch = string.Empty;
        }

        string search = blacklistSearch?.Trim();
        string searchLower = search.NullOrEmpty() ? null : search?.ToLowerInvariant();

        string modPrefix = "ULS_Settings_ModFilter".Translate();
        string allLabel = "ULS_Settings_ModFilter_All".Translate();
        string modLabel = selectedModName.NullOrEmpty()
            ? $"{modPrefix}：{allLabel}"
            : $"{modPrefix}：{selectedModName}";
        if (Widgets.ButtonText(modButtonRect, modLabel))
        {
            OpenModFilterMenu();
        }

        listing.Gap(6f);


        ValidateSelectedMod();

        List<ThingClassGroup> groups = BuildThingClassGroups(searchLower, selectedModPackageId);

        float listHeight = Mathf.Min(360f, tabBaseRect.height - listing.CurHeight - 70f);
        Rect outerRect = listing.GetRect(listHeight);
        float viewHeight = GetThingClassTreeViewHeight(groups, searchLower);
        Rect viewRect = new(0f, 0f, outerRect.width - 16f, viewHeight);
        Widgets.BeginScrollView(outerRect, ref blacklistTreeScrollPosition, viewRect);

        float y = 0f;
        const float rowGap = 2f;
        float rowHeight = Text.LineHeight + rowGap;

        foreach (var group in groups)
        {
            bool searchActive = !searchLower.NullOrEmpty();
            bool expanded = searchActive || expandedThingClassKeys.Contains(group.key);


            Rect groupRow = new(0f, y, viewRect.width, Text.LineHeight);
            Rect expandRect = new(groupRow.x, groupRow.y, 18f, 18f);
            Rect checkboxRect = new(expandRect.xMax + 2f, groupRow.y, 24f, 24f);
            Rect labelRect = new(checkboxRect.xMax + 6f, groupRow.y, groupRow.width - (checkboxRect.xMax + 6f),
                groupRow.height);

            if (!searchActive && Widgets.ButtonImage(expandRect, expanded ? TexButton.Collapse : TexButton.Reveal))
            {
                if (expanded)
                {
                    expandedThingClassKeys.Remove(group.key);
                }
                else
                {
                    expandedThingClassKeys.Add(group.key);
                }

                expanded = !expanded;
            }

            var checkedCount = 0;
            foreach (var t in group.allDefs)
            {
                if (!Settings.IsDefNameBlacklisted(t.defName))
                {
                    checkedCount++;
                }
            }

            var state = checkedCount == 0
                ? MultiCheckboxState.Off
                : (checkedCount == group.allDefs.Count ? MultiCheckboxState.On : MultiCheckboxState.Partial);

            Texture2D stateTex = state switch
            {
                MultiCheckboxState.On => Widgets.CheckboxOnTex,
                MultiCheckboxState.Off => Widgets.CheckboxOffTex,
                _ => Widgets.CheckboxPartialTex
            };

            bool groupToggleClicked = Widgets.ButtonImage(checkboxRect, stateTex);
            if (groupToggleClicked)
            {
                IEnumerable<string> defNames = group.allDefs.Select(d => d.defName);

                var changed = state == MultiCheckboxState.On
                    ? Settings.AddDefNamesToBlacklist(defNames)
                    : Settings.RemoveDefNamesFromBlacklist(defNames);

                if (changed)
                {
                    Settings.defNameBlacklist.Sort(StringComparer.Ordinal);
                    Settings.Write();
                }
            }

            string groupLabel = $"{group.displayName}  ({checkedCount}/{group.allDefs.Count})";
            Widgets.Label(labelRect, groupLabel);
            TooltipHandler.TipRegion(labelRect, group.key);

            y += rowHeight;

            if (!expanded)
            {
                continue;
            }


            List<ThingDef> defsToShow = searchActive ? group.shownDefs : group.allDefs;
            foreach (var def in defsToShow)
            {
                Rect defRow = new(28f, y, viewRect.width - 28f, Text.LineHeight);

                bool checkedState = !Settings.IsDefNameBlacklisted(def.defName);

                string modName = def.modContentPack?.Name;
                string defLabel = modName.NullOrEmpty()
                    ? $"{def.LabelCap} ({def.defName})"
                    : $"{def.LabelCap} ({def.defName}) [{modName}]";

                bool before = checkedState;
                Widgets.CheckboxLabeled(defRow, defLabel, ref checkedState);
                if (checkedState != before)
                {
                    var changed = checkedState
                        ? Settings.RemoveDefNameFromBlacklist(def.defName)
                        : Settings.AddDefNameToBlacklist(def.defName);

                    if (changed)
                    {
                        Settings.defNameBlacklist.Sort(StringComparer.Ordinal);
                        Settings.Write();
                    }
                }

                y += rowHeight;
            }
        }

        Widgets.EndScrollView();

        listing.GapLine();
        if (listing.ButtonText("ULS_Settings_ResetToDefault".Translate()))
        {
            Settings.ResetToDefault();
            Settings.Write();
            groupMaxSizeBuffer = Settings.groupMaxSize.ToString();
        }

        listing.End();
    }

    private static string GetLiftControlModeLabel(LiftControlMode mode) => mode switch
    {
        LiftControlMode.Remote => "ULS_LiftControlMode_Remote".Translate(),
        LiftControlMode.Console => "ULS_LiftControlMode_Console".Translate(),
        LiftControlMode.Manual => "ULS_LiftControlMode_Manual".Translate(),
        _ => mode.ToString()
    };


    private struct ThingClassGroup
    {
        public string key;
        public string displayName;
        public List<ThingDef> allDefs;
        public List<ThingDef> shownDefs;
    }


    private List<ThingClassGroup> BuildThingClassGroups(string searchLower, string modPackageIdFilter)
    {
        List<ThingDef> allThingDefs = DefDatabase<ThingDef>.AllDefsListForReading;

        Dictionary<string, ThingClassGroup> groups = new Dictionary<string, ThingClassGroup>(StringComparer.Ordinal);
        foreach (var def in allThingDefs)
        {
            if (!IsEligibleEdificeDef(def))
            {
                continue;
            }

            if (!modPackageIdFilter.NullOrEmpty() && def.modContentPack?.PackageId != modPackageIdFilter)
            {
                continue;
            }

            Type thingClass = def.thingClass;
            if (thingClass == null)
            {
                continue;
            }

            string key = thingClass.FullName ?? thingClass.Name;
            if (!groups.TryGetValue(key, out ThingClassGroup group))
            {
                group = new ThingClassGroup
                {
                    key = key,
                    displayName = thingClass.Name,
                    allDefs = new List<ThingDef>(),
                    shownDefs = null
                };
            }

            group.allDefs.Add(def);
            groups[key] = group;
        }


        List<ThingClassGroup> result = groups.Values
            .OrderBy(g => g.displayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.key, StringComparer.Ordinal)
            .ToList();

        for (int gi = 0; gi < result.Count; gi++)
        {
            ThingClassGroup group = result[gi];
            group.allDefs = group.allDefs
                .OrderBy(d => d.label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.defName, StringComparer.Ordinal)
                .ToList();

            if (searchLower.NullOrEmpty())
            {
                group.shownDefs = group.allDefs;
                result[gi] = group;
                continue;
            }

            bool typeMatches = group.displayName != null && group.displayName.ToLowerInvariant().Contains(searchLower);
            bool fullTypeMatches = group.key != null && group.key.ToLowerInvariant().Contains(searchLower);
            if (typeMatches || fullTypeMatches)
            {
                group.shownDefs = group.allDefs;
                result[gi] = group;
                continue;
            }

            group.shownDefs = group.allDefs.Where(d => DefMatchesSearch(d, searchLower)).ToList();
            result[gi] = group;
        }

        if (!searchLower.NullOrEmpty())
        {
            result = result.Where(g => g.shownDefs is { Count: > 0 }).ToList();
        }

        return result;
    }


    private void OpenModFilterMenu()
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>
        {
            new FloatMenuOption(
                "ULS_Settings_ModFilter_All".Translate(),
                () =>
                {
                    selectedModPackageId = null;
                    selectedModName = null;
                })
        };


        List<ThingDef> allThingDefs = DefDatabase<ThingDef>.AllDefsListForReading;
        HashSet<string> availablePackageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var def in allThingDefs)
        {
            if (!IsEligibleEdificeDef(def))
            {
                continue;
            }

            string packageId = def.modContentPack?.PackageId;
            if (!packageId.NullOrEmpty())
            {
                availablePackageIds.Add(packageId);
            }
        }

        List<ModContentPack> mods = LoadedModManager.RunningModsListForReading;
        foreach (ModContentPack mod in mods.OrderBy(m => m.Name))
        {
            string label = mod.Name;
            string packageId = mod.PackageId;

            if (!availablePackageIds.Contains(packageId))
            {
                continue;
            }

            FloatMenuOption option = new FloatMenuOption(
                label,
                () =>
                {
                    selectedModPackageId = packageId;
                    selectedModName = label;
                })
            {
                tooltip = packageId
            };
            options.Add(option);
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }


    private void ValidateSelectedMod()
    {
        if (selectedModPackageId.NullOrEmpty())
        {
            return;
        }

        ModContentPack exists = LoadedModManager.RunningModsListForReading
            .FirstOrDefault(m => m.PackageId == selectedModPackageId);
        if (exists == null)
        {
            selectedModPackageId = null;
            selectedModName = null;
        }
    }


    private float GetThingClassTreeViewHeight(List<ThingClassGroup> groups, string searchLower)
    {
        bool searchActive = !searchLower.NullOrEmpty();
        int rows = 0;

        foreach (var group in groups)
        {
            rows++;

            bool expanded = searchActive || expandedThingClassKeys.Contains(group.key);
            if (!expanded)
            {
                continue;
            }

            rows += (searchActive ? group.shownDefs.Count : group.allDefs.Count);
        }

        return Mathf.Max(1f, rows * (Text.LineHeight + 2f));
    }


    private static bool IsEligibleEdificeDef(ThingDef def)
    {
        if (def == null)
        {
            return false;
        }


        if (def.building == null || !def.building.isEdifice)
        {
            return false;
        }

        if (!def.destroyable)
        {
            return false;
        }

        return true;
    }


    private static bool DefMatchesSearch(ThingDef def, string searchLower)
    {
        if (searchLower.NullOrEmpty())
        {
            return true;
        }

        string label = def.label;
        if (!label.NullOrEmpty() && label.ToLowerInvariant().Contains(searchLower))
        {
            return true;
        }

        string defName = def.defName;
        if (!defName.NullOrEmpty() && defName.ToLowerInvariant().Contains(searchLower))
        {
            return true;
        }

        string modName = def.modContentPack?.Name;
        if (!modName.NullOrEmpty() && modName != null && modName.ToLowerInvariant().Contains(searchLower))
        {
            return true;
        }

        return false;
    }

    private void DrawSectionButton(Listing_Standard listing, GeneralSection section, string label)
    {
        Rect rect = listing.GetRect(30f);
        if (currentSection == section)
        {
            Widgets.DrawHighlightSelected(rect);
        }
        else if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
        }

        if (Widgets.ButtonInvisible(rect))
        {
            currentSection = section;
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;

        listing.Gap(2f);
    }

    private void DrawCoreSection(Listing_Standard listing)
    {
        Rect liftModeRow = listing.GetRect(Text.LineHeight);
        Rect liftModeLabelRect = new(liftModeRow.x, liftModeRow.y, liftModeRow.width - 160f, liftModeRow.height);
        Rect liftModeButtonRect = new(liftModeRow.xMax - 160f, liftModeRow.y, 160f, liftModeRow.height);
        Widgets.Label(liftModeLabelRect, "ULS_Settings_LiftControlMode".Translate());

        string liftModeLabel = GetLiftControlModeLabel(Settings.liftControlMode);
        if (Widgets.ButtonText(liftModeButtonRect, liftModeLabel))
        {
            List<FloatMenuOption> options = new()
            {
                new("ULS_LiftControlMode_Remote".Translate(),
                    () => Settings.liftControlMode = LiftControlMode.Remote),
                new("ULS_LiftControlMode_Console".Translate(),
                    () => Settings.liftControlMode = LiftControlMode.Console),
                new("ULS_LiftControlMode_Manual".Translate(),
                    () => Settings.liftControlMode = LiftControlMode.Manual)
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        listing.Gap(6f);
        listing.Label("ULS_Settings_LiftControlMode_Desc".Translate());

        listing.Gap(6f);
        listing.CheckboxLabeled("ULS_Settings_EnableLiftPower".Translate(), ref Settings.enableLiftPower,
            "ULS_Settings_EnableLiftPower_Desc".Translate());

        listing.GapLine();

        if (groupMaxSizeBuffer.NullOrEmpty())
        {
            groupMaxSizeBuffer = Settings.groupMaxSize.ToString();
        }

        Rect groupMaxSizeRow = listing.GetRect(Text.LineHeight);
        Rect groupMaxSizeLabelRect = new Rect(groupMaxSizeRow.x, groupMaxSizeRow.y, groupMaxSizeRow.width - 100f,
            groupMaxSizeRow.height);
        Rect groupMaxSizeFieldRect =
            new Rect(groupMaxSizeRow.xMax - 100f, groupMaxSizeRow.y, 100f, groupMaxSizeRow.height);
        Widgets.Label(groupMaxSizeLabelRect, "ULS_Settings_GroupMaxSize".Translate());
        Widgets.TextFieldNumeric(groupMaxSizeFieldRect, ref Settings.groupMaxSize, ref groupMaxSizeBuffer, 1, 5000);
    }

    private void DrawVisualSection(Listing_Standard listing)
    {
        listing.CheckboxLabeled("ULS_Settings_OverlayDisplay_ShowStoredGhost".Translate(),
            ref Settings.showStoredGhostOverlay);
        listing.CheckboxLabeled("ULS_Settings_OverlayDisplay_ShowControllerCell".Translate(),
            ref Settings.ShowControllerCell);
        listing.CheckboxLabeled("ULS_Settings_OverlayDisplay_ShowAutoGroupDetectionProjection".Translate(),
            ref Settings.showAutoGroupDetectionProjection);
    }

    private void DrawPerformanceSection(Listing_Standard listing)
    {
        listing.Gap();
        Rect liftHpRow = listing.GetRect(Text.LineHeight);
        float hpSetBefore = Settings.liftDurationHpSet;
        string hpSetLabel = "ULS_Settings_LiftDurationHpSet".Translate(hpSetBefore.ToString("0.00"));
        float hpSetAfter =
            Widgets.HorizontalSlider(liftHpRow, hpSetBefore, 0f, 5f, false, hpSetLabel, "0", "5", 0.01f);
        if (Math.Abs(hpSetAfter - hpSetBefore) > 0.0001f)
        {
            Settings.liftDurationHpSet = hpSetAfter;
        }

        listing.Gap(6f);

        Rect liftMassRow = listing.GetRect(Text.LineHeight);
        float massSetBefore = Settings.liftDurationMassSet;
        string massSetLabel = "ULS_Settings_LiftDurationMassSet".Translate(massSetBefore.ToString("0.00"));
        float massSetAfter = Widgets.HorizontalSlider(liftMassRow, massSetBefore, 0f, 5f, false, massSetLabel, "0",
            "5", 0.01f);
        if (Math.Abs(massSetAfter - massSetBefore) > 0.0001f)
        {
            Settings.liftDurationMassSet = massSetAfter;
        }
    }
}