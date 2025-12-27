namespace Universal_Lift_Structure;

/// 文件意图：作为 ULS 内部“脉冲式 flick 触发器”的 flickable 组件。
/// 说明：继承原版 `CompFlickable`，但隐藏其默认的开关 Gizmo，避免玩家看到与误操作“开/关”逻辑。
public class ULS_Flickable_Pulse : CompFlickable
{
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        yield break;
    }
}
