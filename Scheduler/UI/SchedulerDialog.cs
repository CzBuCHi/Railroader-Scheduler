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

/*

record commands:

MOVE                    * AutoEngineerOrdersHelper.SetOrdersValue(AutoEngineerMode? mode = null, bool? forward = null,int? maxSpeedMph = null, float? distance = null)
SET_SWITCH              Messenger.Default.Send<SwitchThrownDidChange>(new SwitchThrownDidChange(this));
UNCOUPLE                ? void ApplyEndGearChange(Car.LogicalEnd logicalEnd, Car.EndGearStateKey endGearStateKey, float f)
SET_HANDBRAKE           * CarPropertyChanges.SetHandbrake(this Car car, bool apply)

special commands: (game do not have buttons for those - need to add them manually in SchedulerDialog

CONNECT_AIR             * Jobs.ConnectAir
RELEASE_HANDBRAKES      * Jobs.ReleaseAllHandbrakes
RESTORE_SWITCH

 */
