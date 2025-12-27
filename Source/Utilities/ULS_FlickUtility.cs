namespace Universal_Lift_Structure;

/// 文件意图：封装 flick 触发相关的底层操作。
/// 说明：原版 `CompFlickable.wantSwitchOn` 为私有字段；为了实现“脉冲式 flick”（每次请求都能派工一次），
/// 这里通过 Harmony FieldRef 写入该字段，并调用 `FlickUtility.UpdateFlickDesignation` 创建/更新 designation。

public static class ULS_FlickUtility
{
    private static readonly AccessTools.FieldRef<CompFlickable, bool> WantSwitchOnRef =
        AccessTools.FieldRefAccess<CompFlickable, bool>("wantSwitchOn");


    /// 方法意图：对目标触发一次“脉冲 flick”需求。
    /// - 通过设置 wantSwitchOn 与当前 SwitchIsOn 相反，使 `WantsFlick()` 为 true
    /// - 调用 `FlickUtility.UpdateFlickDesignation` 让原版系统生成 flick designation，从而自动派工
    public static void RequestPulseFlick(ThingWithComps thing)
    {
        if (thing == null)
        {
            return;
        }

        CompFlickable flickable = thing.GetComp<CompFlickable>();
        if (flickable == null)
        {
            Log.Error($"[ULS] RequestPulseFlick failed: target has no CompFlickable. target={thing}");
            return;
        }

        WantSwitchOnRef(flickable) = !flickable.SwitchIsOn;
        FlickUtility.UpdateFlickDesignation(thing);
    }

    /// 方法意图：获取/生成一个“绑定到指定格”的 flick 代理，并返回其触发器。
    /// 设计目的：
    /// - 让 flick 目标在 UI/派工语义上“看起来位于 ownerCell 对应的建筑本体”（控制器/控制台），而不是随机相邻格。
    /// - 原版 `JobDriver_Flick` 使用 `PathEndMode.Touch`，因此代理无需声明 InteractionCell。
    /// - 代理本体无电力组件，避免 `FlickedOff` 影响供电。
    public static ULS_FlickTrigger GetOrCreateFlickProxyTriggerAt(Map map, IntVec3 ownerCell)
    {
        if (map == null || !ownerCell.IsValid || !ownerCell.InBounds(map))
        {
            return null;
        }

        // 复用：ownerCell 同格若已有代理，直接用。
        List<Thing> list = map.thingGrid.ThingsListAt(ownerCell);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is ThingWithComps existing && existing.def == ULS_ThingDefOf.ULS_FlickProxy)
            {
                return existing.GetComp<ULS_FlickTrigger>();
            }
        }

        ThingWithComps proxy = ThingMaker.MakeThing(ULS_ThingDefOf.ULS_FlickProxy) as ThingWithComps;
        if (proxy == null)
        {
            return null;
        }

        // 固定旋转：代理不再承担交互点语义，Rotation 仅作为无关的展示字段。
        GenSpawn.Spawn(proxy, ownerCell, map, Rot4.North);
        return proxy.GetComp<ULS_FlickTrigger>();
    }
}
