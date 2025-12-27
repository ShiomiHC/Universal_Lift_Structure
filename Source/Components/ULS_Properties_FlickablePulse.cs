namespace Universal_Lift_Structure;


/// 文件意图：为 ULS 的“脉冲式 flick 触发器”提供 XML 可引用的 Flickable CompProperties。
/// 说明：原版 `CompProperties_Flickable` 默认 compClass 为 `CompFlickable`，会提供开关 Gizmo；
/// 本类将 compClass 绑定到 `ULS_Flickable_Pulse` 以隐藏该 Gizmo。

public class ULS_Properties_FlickablePulse : CompProperties_Flickable
{
    public ULS_Properties_FlickablePulse()
    {
        compClass = typeof(ULS_Flickable_Pulse);
    }
}
