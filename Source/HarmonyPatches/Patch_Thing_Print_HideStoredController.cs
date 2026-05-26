namespace Universal_Lift_Structure;

// 当 hideControllerWhenStored 开启时，跳过已收纳（非升降中）控制器的渲染，显露下方地面。
// 仅隐藏建筑图形，选择框保持可见。
[HarmonyPatch(typeof(Thing), nameof(Thing.Print))]
public static class Patch_Thing_Print_HideStoredController
{
    public static bool Prefix(Thing __instance)
    {
        if (__instance is not Building_WallController controller)
        {
            return true;
        }

        return !ULS_ControllerHideHelper.ShouldHideController(controller);
    }
}
