namespace Universal_Lift_Structure;

// 升降过程中临时生成的占位阻挡实体，由控制器在升降开始/结束时创建/销毁
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
