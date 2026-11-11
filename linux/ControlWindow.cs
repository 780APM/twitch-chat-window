using System.Reflection;
using Gtk;

namespace TwitchChatWindow;

// Layer-shell surfaces are deliberately excluded from Plasma's taskbar.
// This regular application window represents the running overlay there.
internal sealed class ControlWindow : ApplicationWindow
{
    public ControlWindow(Application app, Overlay overlay) : base(app)
    {
        Title = "Twitch Chat Window";
        SetDefaultSize(380, 230);
        Resizable = false;
        SkipTaskbarHint = false;
        SkipPagerHint = false;

        using var icon = new Gdk.Pixbuf(Assembly.GetExecutingAssembly(), "mark.png", 64, 64);
        Icon = icon;

        var root = new Box(Orientation.Vertical, 12) { BorderWidth = 18 };
        var title = new Label { Markup = "<b>Twitch Chat Window</b>", Xalign = 0 };
        root.PackStart(title, false, false, 0);
        root.PackStart(new Label("Set your channel and position in the chat overlay.\nMinimize this window while you play; the overlay stays visible.")
        {
            Xalign = 0, Wrap = true, MaxWidthChars = 44
        }, false, false, 0);

        var clickThrough = new CheckButton("Let clicks pass through the overlay");
        clickThrough.Toggled += (_, _) =>
        {
            if (clickThrough.Active != overlay.IsClickThrough)
                overlay.ToggleClickThrough();
        };
        void SyncState(object sender, System.EventArgs args) => clickThrough.Active = overlay.IsClickThrough;
        overlay.ClickThroughChanged += SyncState;
        Destroyed += (_, _) => overlay.ClickThroughChanged -= SyncState;
        root.PackStart(clickThrough, false, false, 0);

        var buttons = new Box(Orientation.Horizontal, 8);
        var unlock = new Button("Unlock overlay");
        unlock.Clicked += (_, _) => overlay.Unlock();
        var minimize = new Button("Minimize");
        minimize.Clicked += (_, _) => Iconify();
        var close = new Button("Quit");
        close.Clicked += (_, _) => Destroy();
        buttons.PackStart(unlock, true, true, 0);
        buttons.PackStart(minimize, false, false, 0);
        buttons.PackEnd(close, false, false, 0);
        root.PackStart(buttons, false, false, 0);
        Add(root);
    }
}
