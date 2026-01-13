namespace Universal_Lift_Structure;

// ============================================================
// 【Mod 入口类】
// ============================================================
// 此类是 Mod 的入口点，负责初始化、配置管理和设置界面
//
// 【继承关系】
// - 继承自 Mod：RimWorld 的 Mod 基类
//
// 【核心职责】
// 1. Mod 初始化：在构造函数中应用 Harmony 补丁和加载设置
// 2. 设置管理：提供静态访问点 Settings 供全局使用
// 3. 设置界面：实现复杂的多标签页设置窗口
//
// 【设置界面结构】
// - General（常规）标签：包含三个子分区（核心/视觉/性能）
// - Filter（过滤器）标签：黑名单管理，支持按 Mod 筛选和搜索
// - Other（其他）标签：重置按钮等杂项功能
//
// 【UI 系统说明】
// - 使用 TabDrawer 绘制标签页头部
// - 使用 Listing_Standard 进行垂直布局
// - 使用 Widgets 绘制各种 UI 控件
// - 使用 ScrollView 处理长列表内容
//
// 【Harmony 集成】
// - 在构造函数中自动扫描并应用所有 Harmony 补丁
// - Harmony ID: shiomi.UniversalLiftStructure
// ============================================================
public class UniversalLiftStructureMod : Mod
{
    // Harmony 补丁的唯一标识符
    // 用于区分不同 Mod 的补丁，避免冲突
    private const string HarmonyId = "shiomi.UniversalLiftStructure";


    // ============================================================
    // 【内部枚举定义】
    // ============================================================

    // 设置窗口的主标签页类型
    private enum SettingsTab
    {
        General, // 常规设置（包含核心/视觉/性能三个子分区）
        Filter, // 过滤器设置（黑名单管理）
        Other // 其他设置（重置等）
    }

    // General 标签页的子分区类型
    private enum GeneralSection
    {
        Core, // 核心设置（控制模式、分组大小等）
        Visual, // 视觉设置（覆盖层显示等）
        Performance // 性能设置（升降时长系数等）
    }

    // ============================================================
    // 【静态字段】
    // ============================================================

    // 全局设置实例
    // 整个 Mod 通过此静态字段访问配置
    public static UniversalLiftStructureSettings Settings;

    // ============================================================
    // 【UI 状态字段】
    // ============================================================
    // 这些字段仅用于跟踪 UI 状态，不会被序列化保存

    // 当前选中的标签页
    private SettingsTab currentTab = SettingsTab.General;

    // General 标签页中当前选中的子分区
    private GeneralSection currentSection = GeneralSection.Core;


    // Filter 标签页：当前选中的 Mod 过滤器
    private string selectedModPackageId; // Mod 的唯一 ID
    private string selectedModName; // Mod 的显示名称

    // Filter 标签页：黑名单搜索和 UI 状态
    private string blacklistSearch = string.Empty; // 搜索框内容
    private Vector2 blacklistTreeScrollPosition = Vector2.zero; // 滚动位置
    private readonly HashSet<string> expandedThingClassKeys = new(StringComparer.Ordinal); // 展开的分类

    // General 标签页：分组大小输入缓冲
    private string groupMaxSizeBuffer = string.Empty;

    // ============================================================
    // 【构造函数】
    // ============================================================
    // RimWorld 在加载 Mod 时自动调用此构造函数
    // 【执行内容】
    // 1. 加载设置：从 XML 文件读取用户配置
    // 2. 应用补丁：扫描并应用所有 Harmony 补丁类
    // ============================================================
    public UniversalLiftStructureMod(ModContentPack content) : base(content)
    {
        // 加载或创建设置实例
        // 如果是首次运行，会使用默认值
        Settings = GetSettings<UniversalLiftStructureSettings>();

        // 创建 Harmony 实例并应用所有补丁
        // PatchAll() 会自动扫描程序集中所有带 [HarmonyPatch] 特性的类
        Harmony harmony = new(HarmonyId);
        harmony.PatchAll();
    }

    // ============================================================
    // 【设置类别名称】
    // ============================================================
    // 在游戏的 Mod 设置列表中显示的名称
    // ============================================================
    public override string SettingsCategory()
    {
        // 返回本地化的 Mod 名称
        return "ULS_SettingsCategory".Translate();
    }


    // ============================================================
    // 【设置窗口绘制方法】
    // ============================================================
    // RimWorld 在打开 Mod 设置窗口时调用此方法
    // 【参数】
    // inRect：可用的绘制区域
    // 【实现】
    // 使用 switch 根据当前标签页绘制不同内容
    // ============================================================
    public override void DoSettingsWindowContents(Rect inRect)
    {
        // 计算标签页内容区域（排除标签页头部高度）
        Rect tabBaseRect = new(inRect.x, inRect.y + TabDrawer.TabHeight, inRect.width,
            inRect.height - TabDrawer.TabHeight);

        // 创建标签页列表
        // 每个 TabRecord 包含：显示文本、点击回调、是否选中的判断函数
        List<TabRecord> tabs = new()
        {
            new("ULS_Tab_General".Translate(), () => currentTab = SettingsTab.General,
                () => currentTab == SettingsTab.General),
            new("ULS_Tab_Filter".Translate(), () => currentTab = SettingsTab.Filter,
                () => currentTab == SettingsTab.Filter),
            new("ULS_Tab_Other".Translate(), () => currentTab = SettingsTab.Other,
                () => currentTab == SettingsTab.Other)
        };

        // 绘制标签页头部
        TabDrawer.DrawTabs(tabBaseRect, tabs);

        // ============================================================
        // 【Listing_Standard 布局工具】
        // ============================================================
        // Listing_Standard 是 RimWorld UI 的核心布局工具
        // 它会自动管理垂直布局和元素间距
        //
        // 【基本用法】
        // 1. 创建实例：Listing_Standard listing = new();
        // 2. 开始绘制：listing.Begin(区域);
        // 3. 添加元素：listing.Label(), listing.CheckboxLabeled() 等
        // 4. 结束绘制：listing.End();
        //
        // 【常用方法】
        // - Label(text)：显示文本
        // - CheckboxLabeled(text, ref value)：复选框
        // - Gap(pixels)：添加间距
        // - GapLine()：添加分隔线
        // - GetRect(height)：获取指定高度的矩形区域
        // - ButtonText(text)：按钮，返回是否被点击
        // ============================================================
        Listing_Standard listing = new();
        listing.Begin(tabBaseRect);

        switch (currentTab)
        {
            case SettingsTab.General:
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

                listingSettings.End();

                // End the main listing started outside (if any, though here we replaced the whole block)
                listing.End();
                return;
            }
            case SettingsTab.Filter:
            {
                listing.Label("ULS_Settings_DefNameBlacklist".Translate());

                listing.GapLine();
                listing.Label("ULS_Settings_BuiltInRules".Translate());


                bool allowNaturalRock = !Settings.excludeNaturalRock;
                listing.CheckboxLabeled("ULS_Settings_AllowNaturalRock".Translate(), ref allowNaturalRock);
                Settings.excludeNaturalRock = !allowNaturalRock;

                listing.GapLine();

                // ============================================================
                // 【手动布局示例】
                // ============================================================
                // 有时 Listing_Standard 的自动布局不够灵活
                // 这时可以用 GetRect() 获取区域，然后手动分割
                // ============================================================
                Rect searchRow = listing.GetRect(Text.LineHeight);
                Rect searchLabelRect = new(searchRow.x, searchRow.y, 60f, searchRow.height);
                const float modButtonWidth = 160f;
                Rect modButtonRect = new(searchRow.xMax - modButtonWidth, searchRow.y, modButtonWidth,
                    searchRow.height);
                Rect searchFieldRect = new(searchLabelRect.xMax + 6f, searchRow.y,
                    modButtonRect.xMin - (searchLabelRect.xMax + 6f) - 6f - 26f, searchRow.height);
                Rect searchClearRect = new(modButtonRect.xMin - 24f, searchRow.y, 24f, 24f);

                // ============================================================
                // 【Widgets 控件库】
                // ============================================================
                // Widgets 是 RimWorld UI 的核心控件库，提供各种 UI 元素
                //
                // 【常用方法】
                // - Widgets.Label(rect, text)：在指定区域显示文本
                // - Widgets.TextField(rect, text)：文本输入框，返回新的文本值
                // - Widgets.ButtonText(rect, text)：文本按钮，返回是否被点击
                // - Widgets.ButtonImage(rect, texture)：图片按钮，返回是否被点击
                // - Widgets.CheckboxLabeled(rect, text, ref value)：带标签的复选框
                // - Widgets.DrawHighlight(rect)：绘制高亮背景
                // ============================================================
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
                // ============================================================
                // 【ScrollView 滚动视图】
                // ============================================================
                // 当内容超过可视区域时，使用滚动视图
                //
                // 【用法】
                // 1. BeginScrollView(外部区域, ref 滚动位置, 内部视图区域)
                // 2. 在内部绘制内容
                // 3. EndScrollView()
                //
                // 【注意】
                // - blacklistTreeScrollPosition 是 Vector2，存储滚动位置
                // - viewRect 定义了完整的内容尺寸
                // ============================================================
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

                    if (!searchActive &&
                        Widgets.ButtonImage(expandRect, expanded ? TexButton.Collapse : TexButton.Reveal))
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

                listing.End();
                return;
            }
            case SettingsTab.Other:
            {
                listing.Gap();
                if (listing.ButtonText("ULS_Settings_ResetToDefault".Translate()))
                {
                    Settings.ResetToDefault();
                    ClearAllPendingLiftStates();
                    Settings.Write();
                    groupMaxSizeBuffer = Settings.groupMaxSize.ToString();
                }

                listing.End();
                return;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

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
        listing.Label("ULS_Settings_LiftControlMode".Translate());

        if (listing.RadioButton("ULS_LiftControlMode_Remote".Translate(),
                Settings.liftControlMode == LiftControlMode.Remote))
        {
            if (Settings.liftControlMode != LiftControlMode.Remote)
            {
                Settings.liftControlMode = LiftControlMode.Remote;
                ClearAllPendingLiftStates();
            }
        }

        if (listing.RadioButton("ULS_LiftControlMode_Console".Translate(),
                Settings.liftControlMode == LiftControlMode.Console))
        {
            if (Settings.liftControlMode != LiftControlMode.Console)
            {
                Settings.liftControlMode = LiftControlMode.Console;
                ClearAllPendingLiftStates();
            }
        }

        if (listing.RadioButton("ULS_LiftControlMode_Manual".Translate(),
                Settings.liftControlMode == LiftControlMode.Manual))
        {
            if (Settings.liftControlMode != LiftControlMode.Manual)
            {
                Settings.liftControlMode = LiftControlMode.Manual;
                ClearAllPendingLiftStates();
            }
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

    private void ClearAllPendingLiftStates()
    {
        if (Current.Game == null)
        {
            return;
        }

        List<Map> maps = Find.Maps;
        if (maps == null)
        {
            return;
        }

        foreach (var map in maps)
        {
            // 1. 清空全局请求队列
            var mapComp = map.GetComponent<ULS_LiftRequestMapComponent>();
            mapComp?.ClearAllRequests();

            // 2. 清除所有控制器的状态
            List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
            foreach (var t in things)
            {
                if (t is Building_WallController controller && !controller.Destroyed)
                {
                    controller.CancelLiftAction();
                }
            }
        }
    }
}