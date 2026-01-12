namespace Universal_Lift_Structure;

[StaticConstructorOnStartup]
internal static class ULS_Materials
{
    static ULS_Materials()
    {
        LiftShaftBase = MaterialPool.MatFrom("Things/Building/Linked/ULS_LiftShaft_Atlas", ShaderDatabase.Cutout);
    }

    internal static Material LiftShaftBase { get; }
}
