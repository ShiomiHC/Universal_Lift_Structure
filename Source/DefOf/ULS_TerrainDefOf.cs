namespace Universal_Lift_Structure;

[DefOf]
public static class ULS_TerrainDefOf
{
    static ULS_TerrainDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_TerrainDefOf));
    }

    // 桥版控制器在"桥铺设态"下铺设的可承重桥地形
    public static TerrainDef ULS_LiftBridge;
}
