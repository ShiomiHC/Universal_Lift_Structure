namespace Universal_Lift_Structure;

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