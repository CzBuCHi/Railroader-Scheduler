namespace Scheduler.UI;

using global::UI.Builder;
using global::UI.Common;
using Model;

public sealed class SchedulerDialog {

    private readonly Window _Window = SchedulerPlugin.UiHelper.CreateWindow(1000, 600, Window.Position.Center);
    private bool _Populated;

    private void BuildWindow(UIPanelBuilder builder) {
        builder.AddSection("Record new schedule", section => { section.ButtonStrip(strip => { }); });
    }

    public bool IsShown => _Window.IsShown;

    public void ShowWindow(Car car) {
        _Window.Title = $"AI scheduler {car.DisplayName}";
        if (!_Populated) {
            SchedulerPlugin.UiHelper.PopulateWindow(_Window, BuildWindow);
            _Populated = true;
        }

        if (!_Window.IsShown) {
            _Window.ShowWindow();
        }
    }

    public void CloseWindow() {
        _Window.CloseWindow();
    }

}