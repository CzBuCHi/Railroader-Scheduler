namespace Scheduler.Messages;

public struct RebuildCarInspectorAIPanel;

public struct SelectedSwitchChanged;

public struct CommandIndexChanged
{
    public int CommandIndex { get; set; }
}

//new

public struct RebuildScheduleDialog;
public struct RebuildSchedulePanel;
public struct RebuildCommandEditorPanel;
public struct RebuildTopButtonStrip;
public struct RebuildBottomButtonStrip;

public struct NoticeDismissed
{
    public string Key { get; set; }
}