namespace Universal_Lift_Structure;

public enum ULS_AutoGroupType
{
    Friendly,
    Hostile,
    Neutral
}

public class CompProperties_ULS_AutoGroupMarker : CompProperties
{
    public ULS_AutoGroupType autoGroupType = ULS_AutoGroupType.Friendly;

    // 检测半径越大，性能开销越大
    public int maxRadius = 2;

    public int checkIntervalTicks = 60;

    // Pawn 离开检测范围后延迟关闭，防止在边缘反复进出导致频繁开关
    public int closeDelayTicks = 120;

    public int toggleCooldownTicks = 60;

    public CompProperties_ULS_AutoGroupMarker()
    {
        compClass = typeof(ULS_AutoGroupMarker);
    }
}

// 实际检测与触发逻辑在 ULS_AutoGroupMapComponent 中实现；此组件作为标记和配置容器
public class ULS_AutoGroupMarker : ThingComp
{
    public CompProperties_ULS_AutoGroupMarker Props => (CompProperties_ULS_AutoGroupMarker)props;
}
