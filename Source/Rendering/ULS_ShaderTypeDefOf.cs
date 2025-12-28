namespace Universal_Lift_Structure;

[DefOf]
public static class ULS_ShaderTypeDefOf
{
    static ULS_ShaderTypeDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_ShaderTypeDefOf));
    }

    public static ShaderTypeDef ULS_GhostEdgeDotted;
}