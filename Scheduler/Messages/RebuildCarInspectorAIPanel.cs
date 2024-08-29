namespace Scheduler.Messages;

public struct RebuildCarInspectorAIPanel;
public struct RebuildSchedulePanel;
public struct SelectedSwitchChanged;

public struct CommandIndexChanged
{
    public int CommandIndex { get; set; }
}