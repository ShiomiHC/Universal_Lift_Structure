namespace Universal_Lift_Structure;

/// 文件意图：辅助贴图工具。在静态构造阶段通过代码动态生成紫色占位图标，用于右下角 Toggle 按钮，避免了对外部 PNG 资产文件的强制依赖。
[StaticConstructorOnStartup]
internal static class ULS_Textures
{
    private static readonly Texture2D showStoredGhostOverlay;
    private static readonly Texture2D showControllerCell;
    private static readonly Texture2D showAutoGroupDetectionProjection;

    private static readonly Texture2D overlayDisplayMasterToggle;

    static ULS_Textures()
    {
        // 在静态构造函数中生成贴图，确保由 StaticConstructorOnStartup 在主线程触发。
        showStoredGhostOverlay = CreateShowStoredGhostOverlay();
        showControllerCell = CreateShowControllerCellFill();
        showAutoGroupDetectionProjection = CreateShowAutoGroupDetectionProjection();

        // 叠加层显示：总开关图标。
        // 用户将后续替换为正式 PNG；当前使用占位路径（缺失时按 RimWorld 资源系统报错）。
        overlayDisplayMasterToggle = ContentFinder<Texture2D>.Get("UI/ULS_OverlayDisplayMasterToggle");
    }

    internal static Texture2D OverlayDisplayMasterToggle => overlayDisplayMasterToggle;

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

    private static Texture2D CreateShowControllerCellFill()
    {
        // 32x32：紫色底 + 白色描边 + 中心“方框”强调“地块边框”语义。
        const int size = 32;
        Texture2D tex = new(size, size, TextureFormat.ARGB32, false)
        {
            name = "ULS_ShowControllerCellFill",
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
                if (isBorder)
                {
                    tex.SetPixel(x, y, border);
                    continue;
                }

                // 中心再画一个小方框（3px 宽），强调“边框”而不是“整块填充”。
                int cx0 = size / 2 - 6;
                int cx1 = size / 2 + 5;
                int cy0 = size / 2 - 6;
                int cy1 = size / 2 + 5;
                bool innerBorder = (x == cx0 || x == cx1 || y == cy0 || y == cy1) && x >= cx0 && x <= cx1 && y >= cy0 && y <= cy1;
                tex.SetPixel(x, y, innerBorder ? border : fill);
            }
        }

        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateShowAutoGroupDetectionProjection()
    {
        // 32x32：紫色底 + 白色描边 + 3 条竖线，表示“扫描/检测区域”。
        const int size = 32;
        Texture2D tex = new(size, size, TextureFormat.ARGB32, false)
        {
            name = "ULS_ShowAutoGroupDetectionProjection",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32 fill = new(170, 60, 220, 220);
        Color32 border = new(255, 255, 255, 255);
        Color32 clear = new(0, 0, 0, 0);

        int barX0 = size / 2 - 7;
        int barX1 = size / 2;
        int barX2 = size / 2 + 7;
        int barY0 = 8;
        int barY1 = size - 9;

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
                if (isBorder)
                {
                    tex.SetPixel(x, y, border);
                    continue;
                }

                bool inBarY = y >= barY0 && y <= barY1;
                bool isBar = inBarY && (x == barX0 || x == barX1 || x == barX2);
                tex.SetPixel(x, y, isBar ? border : fill);
            }
        }

        tex.Apply(false, true);
        return tex;
    }
}
