namespace Universal_Lift_Structure;


/// 自动组目标类型：用于“接近自动开闭”的组级过滤。
/// 说明：该枚举作为 ID 被 XML/存档读取，名称保持英文。

public enum ULS_AutoGroupType
{
    Friendly,
    Hostile,
    Neutral
}


/// Properties 意图：为控制器变体提供“该组为自动组 + 过滤类型 + 扫描参数”的静态配置。
/// 注意：本功能不提供手动开关，所有参数仅通过 Def 变体配置。

public class CompProperties_ULS_AutoGroupMarker : CompProperties
{
    
    /// 该变体所属自动组类型。
    
    public ULS_AutoGroupType autoGroupType = ULS_AutoGroupType.Friendly;

    
    /// 扫描半径（方形，包含自身）。例如 2 表示 [-2..2] 的 5x5。
    
    public int maxRadius = 2;

    
    /// 扫描间隔（tick）。
    /// 推荐：60（1x≈1次/秒）；更灵敏可用 30。
    
    public int checkIntervalTicks = 60;

    
    /// 关闭延迟（tick）。目标离开后，延迟一段时间再触发关闭，避免路过抖动。
    
    public int closeDelayTicks = 120;

    
    /// 开/闭冷却（tick）。一次触发后在冷却期内不重复触发，避免失败场景下反复尝试。
    
    public int toggleCooldownTicks = 60;

    public CompProperties_ULS_AutoGroupMarker()
    {
        compClass = typeof(ULS_AutoGroupMarker);
    }
}


/// Comp 意图：仅作为“变体标记”。不 Tick，不存档。
/// MapComponent 会通过该 Comp 读取 Props 来决定自动组逻辑。

public class ULS_AutoGroupMarker : ThingComp
{
    public CompProperties_ULS_AutoGroupMarker Props => (CompProperties_ULS_AutoGroupMarker)props;
}
