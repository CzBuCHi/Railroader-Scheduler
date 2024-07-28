namespace Scheduler.Data;

using global::UI.Builder;
using Model;

/// <summary> Panel builder to build UI for command. </summary>
public interface IScheduleCommandPanelBuilder {

    void Configure(BaseLocomotive locomotive);

    void BuildPanel(UIPanelBuilder builder);

    IScheduleCommand CreateScheduleCommand();

}

public abstract class ScheduleCommandPanelBuilderBase : IScheduleCommandPanelBuilder {

    public virtual void Configure(BaseLocomotive locomotive) {
    }

    public virtual void BuildPanel(UIPanelBuilder builder) {
    }

    public abstract IScheduleCommand CreateScheduleCommand();

}