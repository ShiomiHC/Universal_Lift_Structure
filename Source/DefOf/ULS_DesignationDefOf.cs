using RimWorld;
using Verse;

namespace Universal_Lift_Structure
{
    [DefOf]
    public static class ULS_DesignationDefOf
    {
        public static DesignationDef ULS_FlickLiftStructure;

        static ULS_DesignationDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ULS_DesignationDefOf));
        }
    }
}
