namespace Universal_Lift_Structure;

// 将控制器内已存储建筑的市场价值累加到控制器本身的总价值上
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
        return null;
    }
}