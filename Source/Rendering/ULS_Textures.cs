namespace Universal_Lift_Structure;

/// 文件意图：辅助贴图工具。在静态构造阶段通过代码动态生成紫色占位图标，用于右下角 Toggle 按钮，避免了对外部 PNG 资产文件的强制依赖。
[StaticConstructorOnStartup]
internal static class ULS_Textures
{
    private static readonly Texture2D showStoredGhostOverlay;

    static ULS_Textures()
    {
        // 在静态构造函数中生成贴图，确保由 StaticConstructorOnStartup 在主线程触发。
        showStoredGhostOverlay = CreateShowStoredGhostOverlay();
    }

    internal static Texture2D ShowStoredGhostOverlay => showStoredGhostOverlay;

    private static Texture2D CreateShowStoredGhostOverlay()
    {
        // 32x32：紫色底 + 白色描边，确保在小尺寸下仍可辨识。
        const int size = 32;
        Texture2D tex = new(size, size, TextureFormat.ARGB32, false)
        {
            name = "ULS_ShowStoredGhostOverlay",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32 fill = new(170, 60, 220, 220);
        Color32 border = new(255, 255, 255, 255);
        Color32 clear = new(0, 0, 0, 0);

        // 外圈透明，避免图标顶到按钮边界。
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool outer = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                if (outer)
                {
                    tex.SetPixel(x, y, clear);
                    continue;
                }

                bool isBorder = x == 1 || y == 1 || x == size - 2 || y == size - 2;
                tex.SetPixel(x, y, isBorder ? border : fill);
            }
        }

        tex.Apply(false, true);
        return tex;
    }
}
