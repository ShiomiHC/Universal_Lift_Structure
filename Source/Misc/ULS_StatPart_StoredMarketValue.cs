namespace Universal_Lift_Structure;

// 统计项目：MarketValue (市场价值) 的补充部分
// 用于将“已存储在控制器内部的建筑”的价值加到控制器本身的总价值上
public class ULS_StatPart_StoredMarketValue : StatPart
{
    // 修改统计数值
    public override void TransformValue(StatRequest req, ref float val)
    {
        // 检查请求对象是否为其实例，且该控制其中确实存储了东西
        if (req is { HasThing: true, Thing: Building_WallController { HasStored: true } controller })
        {
            // 获取存储物的市场价值（忽略血量折损，通常取全额或按需计算）
            float extra = controller.StoredThingMarketValueIgnoreHp;
            if (extra > 0f)
            {
                // 累加到总价值中
                val += extra;
            }
        }
    }

    // 解释部分（通常显示在详细信息Tooltip中）
    // 返回null表示不额外显示解释文本，因为系统会自动统计总值，或者我们认为不需要额外说明
    public override string ExplanationPart(StatRequest req)
    {
        return null;
    }
}