namespace Universal_Lift_Structure;

[DefOf]
public static class ULS_ShaderTypeDefOf
{
    static ULS_ShaderTypeDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_ShaderTypeDefOf));
    }

    // 用于收纳建筑虚影的点状虚线边缘效果，对应 Defs/ShaderTypeDefs/ULS_GhostEdgeDotted.xml
    public static ShaderTypeDef ULS_GhostEdgeDotted;
}