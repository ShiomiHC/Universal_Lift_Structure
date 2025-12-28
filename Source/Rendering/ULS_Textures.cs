namespace Universal_Lift_Structure;

[StaticConstructorOnStartup]
internal static class ULS_Textures
{
    static ULS_Textures()
    {
        OverlayDisplayMasterToggle = ContentFinder<Texture2D>.Get("UI/ULS_OverlayDisplayMasterToggle");
    }

    internal static Texture2D OverlayDisplayMasterToggle { get; }
}