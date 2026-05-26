namespace Universal_Lift_Structure;

// [StaticConstructorOnStartup] 确保在游戏启动时预加载所有 Gizmo 纹理，避免运行时开销
[StaticConstructorOnStartup]
public static class ULS_GizmoTextures
{
    public static readonly Texture2D MergeGroups = ContentFinder<Texture2D>.Get("UI/Commands/LinkStorageSettings");
    public static readonly Texture2D SplitGroup = ContentFinder<Texture2D>.Get("UI/Commands/UnlinkStorageSettings");
    public static readonly Texture2D SetGroupId = ContentFinder<Texture2D>.Get("UI/TagSet");
    public static readonly Texture2D SetAutoGroupFilter = ContentFinder<Texture2D>.Get("UI/ScanSet");
    public static readonly Texture2D RaiseGroup = ContentFinder<Texture2D>.Get("UI/Up");
    public static readonly Texture2D LowerGroup = ContentFinder<Texture2D>.Get("UI/Down");
    public static readonly Texture2D SelectLinked = ContentFinder<Texture2D>.Get("UI/Commands/SelectAllLinked");
    public static readonly Texture2D ToggleInvertedMode = ContentFinder<Texture2D>.Get("UI/LiftMode");
}