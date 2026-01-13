namespace Universal_Lift_Structure;

// 用于设置控制器编组ID的对话框窗口
public class ULS_Dialog_SetControllerGroupId : Window
{
    // 当前输入的组ID
    private int groupId;

    // 用于输入框的字符串缓冲区
    private string groupIdBuffer;

    // 是否已经点击了确认接受
    private bool accepted;

    // 是否早已自动聚焦到输入框
    private bool focused;

    // 接受时执行的回调，参数为新的组ID
    private readonly Action<int> onAccept;

    // 窗口的初始化大小
    public override Vector2 InitialSize => new(420f, 220f);

    // 关闭按钮（底部按钮）的文本，这里作为“确认”按钮使用
    public override string CloseButtonText => "ULS_GroupSetId_Accept".Translate();

    // 构造函数
    // initialGroupId: 初始显示的组ID
    // onAccept: 点击确认后的回调动作
    public ULS_Dialog_SetControllerGroupId(int initialGroupId, Action<int> onAccept)
    {
        // 强制游戏暂停（如果是单人游戏）
        forcePause = true;
        // 阻止点击窗口周围的区域
        absorbInputAroundWindow = true;
        // 点击外部关闭窗口
        closeOnClickedOutside = true;
        // 显示底部的关闭/确认按钮
        doCloseButton = true;
        // 显示右上角的关闭X号
        doCloseX = true;

        // 确保初始ID至少为1
        groupId = initialGroupId < 1 ? 1 : initialGroupId;
        groupIdBuffer = groupId.ToString();
        this.onAccept = onAccept;
    }


    // 关闭窗口时的逻辑
    public override void Close(bool doCloseSound = true)
    {
        // 如果还没有处理过接受逻辑（通常是通过点击按钮触发Base.Close）
        if (!accepted)
        {
            accepted = true;
            // 触发回调，传递当前的groupId
            onAccept?.Invoke(groupId);
        }

        base.Close(doCloseSound);
    }

    // 绘制窗口内容
    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;

        float y = 0f;
        // 描述文本区域
        Rect descRect = new(0f, y, inRect.width, Text.LineHeight * 2f);
        Widgets.Label(descRect, "ULS_GroupSetId_Desc".Translate());
        y = descRect.yMax + 10f;

        // 输入行区域
        Rect rowRect = new(0f, y, inRect.width, Text.LineHeight);
        // 标签区域
        Rect labelRect = new(rowRect.x, rowRect.y, rowRect.width - 120f, rowRect.height);
        // 输入框区域
        Rect fieldRect = new(rowRect.xMax - 120f, rowRect.y, 120f, rowRect.height);

        Widgets.Label(labelRect, "ULS_GroupSetId_Field".Translate());

        // 设置控件名以便聚焦
        GUI.SetNextControlName("ULS_GroupIdField");
        // 数字输入框，限制范围1到20亿
        Widgets.TextFieldNumeric(fieldRect, ref groupId, ref groupIdBuffer, 1, 2000000000);

        // 打开后的第一帧自动聚焦到输入框
        if (!focused)
        {
            focused = true;
            GUI.FocusControl("ULS_GroupIdField");
        }
    }
}