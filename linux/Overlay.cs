using System;
using System.Reflection;
using Gtk;

namespace TwitchChatWindow;

internal sealed class Overlay : ApplicationWindow
{
    private const int WindowWidth = 400;
    private const int WindowHeight = 350;
    private readonly ChatHistory history = new();
    private readonly Box controls = new(Orientation.Vertical, 7);
    private readonly Label status = LabelFor("Enter a Twitch username to start", "muted");
    private readonly Label lockedHint = LabelFor("Ctrl+Shift+F10 to unlock", "muted");
    private readonly Button pass = new("Click through");
    private readonly Button close = new("×");
    private readonly Button minimize = new("−");
    private readonly Entry channel = new() { PlaceholderText = "Twitch username", MaxLength = 30 };
    private readonly TextView feed = new()
    {
        Editable = false, CursorVisible = false, WrapMode = WrapMode.WordChar,
        LeftMargin = 9, RightMargin = 9, TopMargin = 8, BottomMargin = 8
    };
    private readonly Stack chatView = new();
    private readonly TextTag timestamp = new("timestamp") { Foreground = "#B7AFC6" };
    private readonly TextTag sender = new("sender") { Foreground = "#D0B8FF", Weight = Pango.Weight.Bold };
    private readonly TextMark end;
    private TwitchChat chat;
    private ChatStatus lastStatus;
    private uint refreshTimer;
    private bool clickThrough;

    public Overlay(Application app) : base(app)
    {
        Title = "Twitch Chat Window";
        Decorated = false;
        Visual = Screen.RgbaVisual;
        FocusOnMap = false;
        SkipTaskbarHint = false;
        SkipPagerHint = false;
        KeepAbove = true;
        SetDefaultSize(WindowWidth, WindowHeight);
        FocusInEvent += (_, _) => Unlock();

        using var css = new CssProvider();
        css.LoadFromData(Styles);
        StyleContext.AddProviderForScreen(Screen, css, 600);
        StyleContext.AddClass("overlay-window");

        var root = new Box(Orientation.Vertical, 8) { BorderWidth = 8, Opacity = 0.84 };
        root.StyleContext.AddClass("overlay-card");
        Add(root);

        using var icon = new Gdk.Pixbuf(Assembly.GetExecutingAssembly(), "mark.png", 24, 24);
        var header = new Box(Orientation.Horizontal, 7);
        Icon = icon;
        var title = new Box(Orientation.Horizontal, 7);
        title.PackStart(new Image(icon), false, false, 0);
        title.PackStart(LabelFor("Twitch Chat", "title"), false, false, 0);
        var drag = new EventBox { VisibleWindow = false, AboveChild = true, TooltipText = "Drag to move anywhere on your desktop" };
        drag.Add(title);
        drag.AddEvents((int)Gdk.EventMask.ButtonPressMask);
        drag.ButtonPressEvent += (_, args) =>
        {
            if (args.Event.Button != 1 || clickThrough)
                return;
            BeginMoveDrag(1, (int)args.Event.XRoot, (int)args.Event.YRoot, args.Event.Time);
            args.RetVal = true;
        };
        header.PackStart(drag, true, true, 0);
        lockedHint.NoShowAll = true;
        header.PackEnd(lockedHint, false, false, 0);
        close.TooltipText = "Close";
        close.Clicked += (_, _) => Destroy();
        header.PackEnd(close, false, false, 0);
        minimize.TooltipText = "Minimize to taskbar";
        minimize.Clicked += (_, _) => Iconify();
        header.PackEnd(minimize, false, false, 0);
        root.PackStart(header, false, false, 0);

        var connect = new Button("Connect");
        connect.StyleContext.AddClass("connect");
        connect.Clicked += (_, _) => ConnectChannel();
        channel.Activated += (_, _) => ConnectChannel();
        var channelRow = new Box(Orientation.Horizontal, 7);
        channelRow.PackStart(channel, true, true, 0);
        channelRow.PackEnd(connect, false, false, 0);
        controls.PackStart(channelRow, false, false, 0);

        var opacityText = LabelFor("Opacity 84%", "muted");
        var opacity = new Scale(Orientation.Horizontal, 30, 95, 1) { DrawValue = false, Value = 84 };
        opacity.TooltipText = "Overlay opacity";
        opacity.ValueChanged += (_, _) =>
        {
            root.Opacity = opacity.Value / 100;
            opacityText.Text = $"Opacity {opacity.Value:0}%";
        };
        pass.TooltipText = "Press Ctrl+Shift+F10 to unlock the running overlay.";
        pass.Clicked += (_, _) => ToggleClickThrough();
        var options = new Box(Orientation.Horizontal, 7);
        options.PackStart(opacityText, false, false, 0);
        options.PackStart(opacity, true, true, 0);
        options.PackEnd(pass, false, false, 0);
        controls.PackStart(options, false, false, 0);

        root.PackStart(controls, false, false, 0);

        feed.Buffer.TagTable.Add(timestamp);
        feed.Buffer.TagTable.Add(sender);
        end = feed.Buffer.CreateMark(null, feed.Buffer.EndIter, false);
        var scroller = new ScrolledWindow { HscrollbarPolicy = PolicyType.Never, VscrollbarPolicy = PolicyType.Automatic };
        scroller.Add(feed);
        var empty = new Label("Messages appear here as they’re posted.");
        empty.StyleContext.AddClass("empty");
        chatView.AddNamed(empty, "empty");
        chatView.AddNamed(scroller, "chat");
        chatView.VisibleChildName = "empty";
        root.PackStart(chatView, true, true, 0);
        var footer = new Box(Orientation.Horizontal, 7);
        footer.PackStart(status, true, true, 0);
        var resize = new EventBox { VisibleWindow = false, TooltipText = "Drag to resize" };
        resize.Add(new Label("◢"));
        resize.AddEvents((int)Gdk.EventMask.ButtonPressMask);
        resize.ButtonPressEvent += (_, args) =>
        {
            if (args.Event.Button != 1 || clickThrough)
                return;
            BeginResizeDrag(Gdk.WindowEdge.SouthEast, 1, (int)args.Event.XRoot, (int)args.Event.YRoot, args.Event.Time);
            args.RetVal = true;
        };
        footer.PackEnd(resize, false, false, 0);
        root.PackStart(footer, false, false, 0);

        Destroyed += (_, _) =>
        {
            if (refreshTimer != 0)
                GLib.Source.Remove(refreshTimer);
            chat?.Dispose();
        };
    }

    public void ToggleClickThrough() => SetClickThrough(!clickThrough);
    public void Unlock() => SetClickThrough(false);
    public void SetUnlockHint(string hint)
    {
        lockedHint.Text = hint;
        pass.TooltipText = hint;
    }

    private void SetClickThrough(bool enabled)
    {
        if (clickThrough == enabled)
            return;
        clickThrough = enabled;
        controls.Visible = !enabled;
        close.Visible = !enabled;
        minimize.Visible = !enabled;
        lockedHint.Visible = enabled;
        WindowPlatform.SetClickThrough(this, enabled);
        // Hiding controls queues GTK layout work which can rebuild the native
        // input region. Apply it again once that work has completed.
        GLib.Idle.Add(() =>
        {
            if (IsRealized && clickThrough == enabled)
                WindowPlatform.SetClickThrough(this, enabled);
            return false;
        });
    }

    private void ConnectChannel()
    {
        var name = TwitchChat.NormalizeChannel(channel.Text);
        if (name == null)
        {
            SetStatus("Use letters, numbers or underscores", "warning");
            channel.GrabFocus();
            return;
        }
        channel.Text = name;
        if (chat?.Channel != name)
        {
            history.Clear();
            feed.Buffer.Text = "";
            chatView.VisibleChildName = "empty";
        }
        chat?.Dispose();
        chat = new TwitchChat(name);
        lastStatus = null;
        RefreshChat();
        if (refreshTimer == 0)
            refreshTimer = GLib.Timeout.Add(100, RefreshChat);
    }

    private bool RefreshChat()
    {
        var current = chat.Status;
        if (current != lastStatus)
        {
            lastStatus = current;
            var style = current.State switch
            {
                ConnectionState.Live => "live",
                ConnectionState.Unavailable or ConnectionState.Retrying => "warning",
                _ => "muted"
            };
            SetStatus(current.Text, style);
        }

        var lines = chat.Drain();
        if (lines.Length > 0)
        {
            int firstNew = history.Apply(lines);
            var buffer = feed.Buffer;
            if (firstNew == 0)
                buffer.Text = "";
            var cursor = buffer.EndIter;
            for (int index = firstNew; index < history.Count; index++)
            {
                var message = history[index];
                buffer.InsertWithTags(ref cursor, message.Time + "  ", timestamp);
                buffer.InsertWithTags(ref cursor, message.Name, sender);
                buffer.InsertWithTags(ref cursor, message.Text + "\n", Array.Empty<TextTag>());
            }
            chatView.VisibleChildName = history.Count > 0 ? "chat" : "empty";
            feed.ScrollMarkOnscreen(end);
        }
        if (current.State != ConnectionState.Unavailable)
            return true;
        refreshTimer = 0;
        return false;
    }

    private static Label LabelFor(string text, string style)
    {
        var label = new Label(text) { Xalign = 0 };
        label.StyleContext.AddClass(style);
        return label;
    }

    private void SetStatus(string text, string style)
    {
        status.Text = text;
        status.StyleContext.RemoveClass("muted");
        status.StyleContext.RemoveClass("warning");
        status.StyleContext.RemoveClass("live");
        status.StyleContext.AddClass(style);
    }

    private const string Styles = """
        .overlay-window { background: transparent; }
        .overlay-card { background: #16151F; color: #F5F2FC; border-radius: 9px; padding: 8px; }
        .title { color: #D0B8FF; font-size: 16px; font-weight: bold; }
        .muted, .empty { color: #B7AFC6; }
        .warning { color: #FFCB8C; }
        .live { color: #85E7B9; }
        .overlay-card entry, .overlay-card button { background: #2C283C; color: #F5F2FC; border-color: #493664; }
        .overlay-card button:hover { background: #493664; }
        .overlay-card .connect { background: #6F41BC; }
        .overlay-card .connect:hover { background: #884DEB; }
        .empty, .overlay-card textview text { background: #1F1D2B; }
        .overlay-card textview text { color: #F5F2FC; }
        """;
}
