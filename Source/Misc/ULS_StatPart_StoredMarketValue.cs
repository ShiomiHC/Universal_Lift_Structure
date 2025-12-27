namespace Universal_Lift_Structure;

/// 文件意图：统计补丁。通过 StatPart 机制将控制器内被收纳建筑的缓存价值叠加到控制器自身的 MarketValueIgnoreHp 上，
/// 使游戏财富统计（WealthWatcher）能正确计入这些 DeSpawn 状态的建筑。
public class ULS_StatPart_StoredMarketValue : StatPart
{
    public override void TransformValue(StatRequest req, ref float val)
    {
        if (req is { HasThing: true, Thing: Building_WallController { HasStored: true } controller })
        {
            float extra = controller.StoredThingMarketValueIgnoreHp;
            if (extra > 0f)
            {
                val += extra;
            }
        }
    }

    public override string ExplanationPart(StatRequest req)
    {
        // 不输出文本，避免引入新的翻译键（开发测试阶段也无需展示）。
        return null;
    }
}
