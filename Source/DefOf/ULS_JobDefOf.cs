using RimWorld;
using Verse;

namespace Universal_Lift_Structure
{
    [DefOf]
    public static class ULS_JobDefOf
    {
        public static JobDef ULS_FlickLiftStructure;

        static ULS_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ULS_JobDefOf));
        }
    }
}
