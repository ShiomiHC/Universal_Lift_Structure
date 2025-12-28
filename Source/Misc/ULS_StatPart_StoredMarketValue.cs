namespace Universal_Lift_Structure;

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