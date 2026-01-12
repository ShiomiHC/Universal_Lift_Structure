using UnityEngine;
using Verse;

namespace Universal_Lift_Structure;

/// <summary>
/// Gizmo 图标纹理缓存
/// [StaticConstructorOnStartup] 特性确保在游戏启动时加载所有纹理
/// </summary>
[StaticConstructorOnStartup]
public static class ULS_GizmoTextures
{
    // ====== 复用原版 RimWorld 图标 ======

    /// <summary>
    /// 合并编组图标 - 复用原版"链接存储设置"图标
    /// </summary>
    public static readonly Texture2D MergeGroups = ContentFinder<Texture2D>.Get("UI/Commands/LinkStorageSettings");

    /// <summary>
    /// 拆分编组图标 - 复用原版"解除链接存储设置"图标
    /// </summary>
    public static readonly Texture2D SplitGroup = ContentFinder<Texture2D>.Get("UI/Commands/UnlinkStorageSettings");

    // ====== 自定义 UI 图标 ======

    /// <summary>
    /// 设定 ID 图标 - 自定义标签/标记图标
    /// </summary>
    public static readonly Texture2D SetGroupId = ContentFinder<Texture2D>.Get("UI/TagSet");

    /// <summary>
    /// 设定自动编组过滤图标 - 自定义扫描/过滤图标
    /// </summary>
    public static readonly Texture2D SetAutoGroupFilter = ContentFinder<Texture2D>.Get("UI/ScanSet");

    /// <summary>
    /// 升起编组图标 - 自定义向上箭头图标
    /// </summary>
    public static readonly Texture2D RaiseGroup = ContentFinder<Texture2D>.Get("UI/Up");

    /// <summary>
    /// 降下编组图标 - 自定义向下箭头图标
    /// </summary>
    public static readonly Texture2D LowerGroup = ContentFinder<Texture2D>.Get("UI/Down");

    // ====== 未来可能添加的其他图标 ======

    // /// <summary>
    // /// 选择编组内所有成员图标（可选）
    // /// </summary>
    // public static readonly Texture2D SelectLinked = ContentFinder<Texture2D>.Get("UI/Commands/SelectAllLinked");
}
