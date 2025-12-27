namespace Universal_Lift_Structure;

/// 文件意图：UI 对话框：为控制器显式分组提供“输入数字组ID”的窗口。
/// - 仅负责数字输入与回调；具体分组逻辑由调用方执行。
/// - 所有 UI 文本使用翻译键（Keyed）。
public class ULS_Dialog_SetControllerGroupId : Window
{
    private int groupId;
    private string groupIdBuffer;

    private bool accepted;
    private bool focused;

    private readonly Action<int> onAccept;

    public override Vector2 InitialSize => new(420f, 220f);

    public override string CloseButtonText => "ULS_GroupSetId_Accept".Translate();

    public ULS_Dialog_SetControllerGroupId(int initialGroupId, Action<int> onAccept)
    {
        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        doCloseButton = true;
        doCloseX = true;

        groupId = initialGroupId < 1 ? 1 : initialGroupId;
        groupIdBuffer = groupId.ToString();
        this.onAccept = onAccept;
    }

    /// 方法意图：合并“关闭/确定”的轻量逻辑。
    /// - 所有关闭入口（底部按钮 / 右上角 X / 点击外部 / Accept/Cancel 键）都视为“确认并关闭”。
    /// - 为避免重复触发回调，使用 `accepted` 保证仅执行一次。
    public override void Close(bool doCloseSound = true)
    {
        if (!accepted)
        {
            accepted = true;
            onAccept?.Invoke(groupId);
        }

        base.Close(doCloseSound);
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;

        float y = 0f;
        Rect descRect = new(0f, y, inRect.width, Text.LineHeight * 2f);
        Widgets.Label(descRect, "ULS_GroupSetId_Desc".Translate());
        y = descRect.yMax + 10f;

        Rect rowRect = new(0f, y, inRect.width, Text.LineHeight);
        Rect labelRect = new(rowRect.x, rowRect.y, rowRect.width - 120f, rowRect.height);
        Rect fieldRect = new(rowRect.xMax - 120f, rowRect.y, 120f, rowRect.height);
        Widgets.Label(labelRect, "ULS_GroupSetId_Field".Translate());

        GUI.SetNextControlName("ULS_GroupIdField");
        Widgets.TextFieldNumeric(fieldRect, ref groupId, ref groupIdBuffer, 1, 2000000000);

        if (!focused)
        {
            focused = true;
            GUI.FocusControl("ULS_GroupIdField");
        }
    }
}
