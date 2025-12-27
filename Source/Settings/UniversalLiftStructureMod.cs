namespace Universal_Lift_Structure;

/// 文件意图：Mod 入口与设置 UI。负责 Harmony.PatchAll()、设置持久化对象初始化，以及在设置窗口中提供分标签页（常规/筛选）的复杂设置界面，
/// 包括"内置规则 + DefName 白名单（允许列表）"的树形编辑能力（底层仍以 defNameBlacklist 黑名单存储）。
public class UniversalLiftStructureMod : Mod
{
    public const string HarmonyId = "shiomi.UniversalLiftStructure";

    /// 枚举意图：定义设置窗口的标签页类型（General=常规、Filter=筛选）。
    private enum SettingsTab
    {
        General,
        Filter
    }

    public static UniversalLiftStructureSettings Settings;

    private SettingsTab currentTab = SettingsTab.Filter;

    // UI：按 Mod 过滤（仅当前设置窗口有效；不写入 Settings）
    private string selectedModPackageId;
    private string selectedModName;

    private string blacklistSearch = string.Empty;
    private Vector2 blacklistTreeScrollPosition = Vector2.zero;
    private readonly HashSet<string> expandedThingClassKeys = new(StringComparer.Ordinal);

    private string groupMaxSizeBuffer = string.Empty;

    /// 方法意图：初始化 Settings 并对当前程序集执行 Harmony 全量补丁加载。
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


        
        /// 方法意图：绘制分标签页设置界面。
        /// - 常规标签页：组规模上限（groupMaxSize）数值输入、重置默认按钮。
        /// - 筛选标签页：内置规则勾选（允许门/允许自然岩壁，白名单语义）、DefName 白名单树形勾选（按 ThingClass 分组、支持搜索/按 Mod 过滤/展开折叠/多选框状态）、重置默认按钮。
        /// - 所有 UI 文本通过翻译键获取。
        
    public override void DoSettingsWindowContents(Rect inRect)
    {
        // Tabs
        Rect tabBaseRect = new(inRect.x, inRect.y + TabDrawer.TabHeight, inRect.width, inRect.height - TabDrawer.TabHeight);
        List<TabRecord> tabs = new()
        {
            new("ULS_Tab_General".Translate(), () => currentTab = SettingsTab.General, () => currentTab == SettingsTab.General),
            new("ULS_Tab_Filter".Translate(), () => currentTab = SettingsTab.Filter, () => currentTab == SettingsTab.Filter)
        };
        TabDrawer.DrawTabs(tabBaseRect, tabs);

        Listing_Standard listing = new();
        listing.Begin(tabBaseRect);

        if (currentTab is SettingsTab.General)
        {
            // 常规
            Rect liftModeRow = listing.GetRect(Text.LineHeight);
            Rect liftModeLabelRect = new(liftModeRow.x, liftModeRow.y, liftModeRow.width - 160f, liftModeRow.height);
            Rect liftModeButtonRect = new(liftModeRow.xMax - 160f, liftModeRow.y, 160f, liftModeRow.height);
            Widgets.Label(liftModeLabelRect, "ULS_Settings_LiftControlMode".Translate());

            string liftModeLabel = GetLiftControlModeLabel(Settings.liftControlMode);
            if (Widgets.ButtonText(liftModeButtonRect, liftModeLabel))
            {
                List<FloatMenuOption> options = new()
                {
                    new("ULS_LiftControlMode_Remote".Translate(), () => Settings.liftControlMode = LiftControlMode.Remote),
                    new("ULS_LiftControlMode_Console".Translate(), () => Settings.liftControlMode = LiftControlMode.Console),
                    new("ULS_LiftControlMode_Manual".Translate(), () => Settings.liftControlMode = LiftControlMode.Manual)
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(6f);
            listing.Label("ULS_Settings_LiftControlMode_Desc".Translate());

            listing.Gap(6f);
            listing.CheckboxLabeled("ULS_Settings_EnableLiftPower".Translate(), ref Settings.enableLiftPower, "ULS_Settings_EnableLiftPower_Desc".Translate());

            listing.GapLine();

            // 叠加层显示
            listing.Label("ULS_Settings_OverlayDisplay".Translate());
            listing.CheckboxLabeled("ULS_Settings_OverlayDisplay_Master".Translate(), ref Settings.enableOverlayDisplay);

            // 子项：具体显示项（实际生效条件：总开关=true 且子项=true）
            listing.CheckboxLabeled("ULS_Settings_OverlayDisplay_ShowStoredGhost".Translate(), ref Settings.showStoredGhostOverlay);
            listing.CheckboxLabeled("ULS_Settings_OverlayDisplay_ShowControllerCell".Translate(), ref Settings.ShowControllerCell);
            listing.CheckboxLabeled("ULS_Settings_OverlayDisplay_ShowAutoGroupDetectionProjection".Translate(), ref Settings.showAutoGroupDetectionProjection);

            listing.GapLine();

                if (groupMaxSizeBuffer.NullOrEmpty())
                {
                    groupMaxSizeBuffer = Settings.groupMaxSize.ToString();
                }

                Rect groupMaxSizeRow = listing.GetRect(Text.LineHeight);
                Rect groupMaxSizeLabelRect = new Rect(groupMaxSizeRow.x, groupMaxSizeRow.y, groupMaxSizeRow.width - 100f, groupMaxSizeRow.height);
                Rect groupMaxSizeFieldRect = new Rect(groupMaxSizeRow.xMax - 100f, groupMaxSizeRow.y, 100f, groupMaxSizeRow.height);
                Widgets.Label(groupMaxSizeLabelRect, "ULS_Settings_GroupMaxSize".Translate());
                Widgets.TextFieldNumeric(groupMaxSizeFieldRect, ref Settings.groupMaxSize, ref groupMaxSizeBuffer, 1, 5000);

                listing.Gap();

                // 升降耗时倍率（HP）
                Rect liftHpRow = listing.GetRect(Text.LineHeight);
                float hpSetBefore = Settings.liftDurationHpSet;
                string hpSetLabel = "ULS_Settings_LiftDurationHpSet".Translate(hpSetBefore.ToString("0.00"));
                float hpSetAfter = Widgets.HorizontalSlider(liftHpRow, hpSetBefore, 0f, 5f, false, hpSetLabel, "0", "5", 0.01f);
                if (Math.Abs(hpSetAfter - hpSetBefore) > 0.0001f)
                {
                    Settings.liftDurationHpSet = hpSetAfter;
                }

                // 升降耗时倍率（Mass）
                Rect liftMassRow = listing.GetRect(Text.LineHeight);
                float massSetBefore = Settings.liftDurationMassSet;
                string massSetLabel = "ULS_Settings_LiftDurationMassSet".Translate(massSetBefore.ToString("0.00"));
                float massSetAfter = Widgets.HorizontalSlider(liftMassRow, massSetBefore, 0f, 5f, false, massSetLabel, "0", "5", 0.01f);
                if (Math.Abs(massSetAfter - massSetBefore) > 0.0001f)
                {
                    Settings.liftDurationMassSet = massSetAfter;
                }

                listing.GapLine();
                if (listing.ButtonText("ULS_Settings_ResetToDefault".Translate()))
                {
                    Settings.ResetToDefault();
                    Settings.Write();
                }

                listing.End();
                return;
            }

        // 筛选页
        listing.Label("ULS_Settings_DefNameBlacklist".Translate());

        listing.GapLine();
        listing.Label("ULS_Settings_BuiltInRules".Translate());

        // 固定采用“白名单语义显示（勾选=允许）”，内部仍以 Settings.exclude* + defNameBlacklist（内部黑名单）存储。
        bool allowDoors = !Settings.excludeDoors;
        bool allowNaturalRock = !Settings.excludeNaturalRock;
        listing.CheckboxLabeled("ULS_Settings_AllowDoors".Translate(), ref allowDoors);
        listing.CheckboxLabeled("ULS_Settings_AllowNaturalRock".Translate(), ref allowNaturalRock);
        Settings.excludeDoors = !allowDoors;
        Settings.excludeNaturalRock = !allowNaturalRock;

        listing.GapLine();

        // 搜索框 + Mod 过滤（默认折叠，通过搜索定位）
        Rect searchRow = listing.GetRect(Text.LineHeight);
        Rect searchLabelRect = new(searchRow.x, searchRow.y, 60f, searchRow.height);
        const float modButtonWidth = 160f;
        Rect modButtonRect = new(searchRow.xMax - modButtonWidth, searchRow.y, modButtonWidth, searchRow.height);
        Rect searchFieldRect = new(searchLabelRect.xMax + 6f, searchRow.y, modButtonRect.xMin - (searchLabelRect.xMax + 6f) - 6f - 26f, searchRow.height);
        Rect searchClearRect = new(modButtonRect.xMin - 24f, searchRow.y, 24f, 24f);

        Widgets.Label(searchLabelRect, "ULS_Settings_Search".Translate());
        blacklistSearch = Widgets.TextField(searchFieldRect, blacklistSearch);
        if (Widgets.ButtonImage(searchClearRect, TexButton.CloseXSmall))
        {
            blacklistSearch = string.Empty;
        }

        string search = blacklistSearch?.Trim();
        string searchLower = search.NullOrEmpty() ? null : search.ToLowerInvariant();

        string modPrefix = "ULS_Settings_ModFilter".Translate();
        string allLabel = "ULS_Settings_ModFilter_All".Translate();
        string modLabel = selectedModName.NullOrEmpty()
            ? $"{modPrefix}：{allLabel}"
            : $"{modPrefix}：{selectedModName}";
        if (Widgets.ButtonText(modButtonRect, modLabel))
        {
            // 口径 2：只显示“在 eligibility 条件下有候选建筑”的 mod（不受搜索词影响）
            OpenModFilterMenu();
        }

        listing.Gap(6f);

        // 仅在 UI 构建时做“筛选 mod 是否仍存在”的保护
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

        for (int g = 0; g < groups.Count; g++)
        {
            ThingClassGroup group = groups[g];
            bool searchActive = !searchLower.NullOrEmpty();
            bool expanded = searchActive || expandedThingClassKeys.Contains(group.key);

            // 组行
            Rect groupRow = new(0f, y, viewRect.width, Text.LineHeight);
            Rect expandRect = new(groupRow.x, groupRow.y, 18f, 18f);
            Rect checkboxRect = new(expandRect.xMax + 2f, groupRow.y, 24f, 24f);
            Rect labelRect = new(checkboxRect.xMax + 6f, groupRow.y, groupRow.width - (checkboxRect.xMax + 6f), groupRow.height);

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

            int checkedCount;
            MultiCheckboxState state;
            // 固定白名单语义：勾选=允许 => checked 表示“允许（未命中内部黑名单）”
            checkedCount = 0;
            for (int i = 0; i < group.allDefs.Count; i++)
            {
                if (!Settings.IsDefNameBlacklisted(group.allDefs[i].defName))
                {
                    checkedCount++;
                }
            }

            state = checkedCount == 0
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
                bool changed;
                IEnumerable<string> defNames = group.allDefs.Select(d => d.defName);
                // 白名单：On=全允许 -> 点击变为全不允许（写入内部黑名单）
                changed = state == MultiCheckboxState.On
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

            // 子项行
            List<ThingDef> defsToShow = searchActive ? group.shownDefs : group.allDefs;
            for (int i = 0; i < defsToShow.Count; i++)
            {
                ThingDef def = defsToShow[i];
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
                    bool changed;

                    // 勾选=允许 => 取消勾选写入内部黑名单；勾选移出内部黑名单
                    changed = checkedState
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

        
        /// 结构体意图：用于设置界面中按 thingClass 分组显示 ThingDef 列表，包含分组键、显示名、全部 Def 列表、以及搜索过滤后的显示列表。
        
        private struct ThingClassGroup
        {
            public string key;
            public string displayName;
            public List<ThingDef> allDefs;
            public List<ThingDef> shownDefs;
        }

        
        /// 方法意图：构建 ThingClass 分组列表，用于筛选标签页的树形显示。根据搜索词和 Mod 过滤条件筛选符合条件的 edifice ThingDef，并按 thingClass 分组。
        
        private List<ThingClassGroup> BuildThingClassGroups(string searchLower, string modPackageIdFilter)
        {
            List<ThingDef> allThingDefs = DefDatabase<ThingDef>.AllDefsListForReading;

            Dictionary<string, ThingClassGroup> groups = new Dictionary<string, ThingClassGroup>(StringComparer.Ordinal);
            for (int i = 0; i < allThingDefs.Count; i++)
            {
                ThingDef def = allThingDefs[i];
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

            // 排序：组名、组内 Def label
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
                result = result.Where(g => g.shownDefs != null && g.shownDefs.Count > 0).ToList();
            }

            return result;
        }

        
        /// 方法意图：打开 Mod 过滤下拉菜单，列出所有包含符合条件 edifice 的 Mod，供用户选择以缩小显示范围。
        
        private void OpenModFilterMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            options.Add(new FloatMenuOption(
                "ULS_Settings_ModFilter_All".Translate(),
                () =>
                {
                    selectedModPackageId = null;
                    selectedModName = null;
                }));

            // 只显示“在 eligibility 条件下能产生候选项”的 mod
            // 注意：这里不考虑搜索词（口径 2），避免出现“先选 mod 再搜索”时可选 mod 被搜索词误过滤掉。
            List<ThingDef> allThingDefs = DefDatabase<ThingDef>.AllDefsListForReading;
            HashSet<string> availablePackageIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < allThingDefs.Count; i++)
            {
                ThingDef def = allThingDefs[i];
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
                    });
                option.tooltip = packageId;
                options.Add(option);
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        
        /// 方法意图：在 UI 构建时验证当前选中的 Mod 是否仍然存在（防止 Mod 卸载后导致过滤失效），若不存在则清空选择。
        
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

        
        /// 方法意图：计算树形视图的总高度，用于设置滚动视图的 viewRect 高度（根据分组数量、展开状态、搜索过滤后的子项数量计算）。
        
        private float GetThingClassTreeViewHeight(List<ThingClassGroup> groups, string searchLower)
        {
            bool searchActive = !searchLower.NullOrEmpty();
            int rows = 0;

            for (int i = 0; i < groups.Count; i++)
            {
                ThingClassGroup group = groups[i];
                rows++; // group row

                bool expanded = searchActive || expandedThingClassKeys.Contains(group.key);
                if (!expanded)
                {
                    continue;
                }

                rows += (searchActive ? group.shownDefs.Count : group.allDefs.Count);
            }

            return Mathf.Max(1f, rows * (Text.LineHeight + 2f));
        }

        
        /// 方法意图：判定 ThingDef 是否为"符合条件的 edifice"（可摧毁、isEdifice、非 Frame），用于筛选标签页中只显示可被控制器收纳的建筑类型。
        
        private static bool IsEligibleEdificeDef(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }

            // 与 ULS_Utility.CanInjectLowerGizmo 尽量对齐
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

        
        /// 方法意图：判定 ThingDef 是否匹配搜索词（检查 label、defName、Mod 名称），用于搜索过滤功能。
        
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
            if (!modName.NullOrEmpty() && modName.ToLowerInvariant().Contains(searchLower))
            {
                return true;
            }

            return false;
        }
    }
