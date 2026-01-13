namespace Universal_Lift_Structure;

public class Building_LiftBlocker : Building
{
    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        if (this.Destroyed)
        {
            return;
        }

        base.Destroy(mode);
    }
}
