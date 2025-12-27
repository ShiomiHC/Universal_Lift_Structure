namespace Universal_Lift_Structure;

/// 文件意图：渲染资产加载与注入 —— ShaderTypeDefOf：DefOf 类型，用于在代码中方便引用通过 XML 定义的 ULS_GhostEdgeDotted。
[DefOf]
public static class ULS_ShaderTypeDefOf
{
    static ULS_ShaderTypeDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_ShaderTypeDefOf));
    }

    public static ShaderTypeDef ULS_GhostEdgeDotted;
}