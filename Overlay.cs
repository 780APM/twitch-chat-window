using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
[assembly: AssemblyTitle("Twitch Chat Window")]
[assembly: AssemblyProduct("Twitch Chat Window")]
[assembly: AssemblyDescription("An always-on-top Twitch chat overlay for Windows")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace TwitchChatWindow
{
    internal sealed class Overlay : Form
    {
        private static readonly Color Background = Color.FromArgb(22, 21, 31);
        private static readonly Color Surface = Color.FromArgb(31, 29, 43);
        private static readonly Color Input = Color.FromArgb(44, 40, 60);
        private static readonly Color Foreground = Color.FromArgb(245, 242, 252);
        private static readonly Color Muted = Color.FromArgb(183, 175, 198);
        private static readonly Color Accent = Color.FromArgb(208, 184, 255);
        private static readonly Color Live = Color.FromArgb(133, 231, 185);
        private static readonly Color Warning = Color.FromArgb(255, 203, 140);
        private const string Shortcut = "Ctrl+Shift+F10";
        private const int HotkeyId = 1;

        private readonly Panel controls;
        private readonly Label status;
        private readonly Label shortcut;
        private readonly Label empty;
        private readonly Label opacityLabel;
        private readonly TextBox channel;
        private readonly RichTextBox feed;
        private readonly Button close;
        private readonly Button lockButton;
        private readonly Icon appIcon;
        private readonly Bitmap brandMark;
        private readonly Font nameFont;
        private readonly ToolTip tips = new ToolTip();
        private readonly Timer refresh = new Timer { Interval = 100 };
        private readonly List<ChatMessage> history = new List<ChatMessage>();
        private TwitchChat chat;
        private ChatStatus lastStatus;
        private bool hotkeyRegistered;
        private bool clickThrough;

        public Overlay()
        {
            SuspendLayout();
            Text = "Twitch Chat Window";
            Font = new Font("Segoe UI", 10f);
            nameFont = new Font(Font, FontStyle.Bold);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Background;
            ForeColor = Foreground;
            Opacity = 0.84;
            Padding = new Padding(8);
            ClientSize = new Size(400, 338);
            MinimumSize = new Size(350, 210);

            appIcon = GetAppIcon();
            if (appIcon != null)
            {
                Icon = appIcon;
                brandMark = appIcon.ToBitmap();
            }

            var header = new Panel { Dock = DockStyle.Top, Height = 36 };
            var mark = new PictureBox { Image = brandMark, SizeMode = PictureBoxSizeMode.Zoom, TabStop = false };
            mark.SetBounds(5, 4, 24, 24);
            mark.MouseDown += DragWindow;
            var title = MakeLabel("Twitch Chat", Accent, ContentAlignment.MiddleLeft);
            title.Font = new Font("Segoe UI Semibold", 13f);
            title.SetBounds(34, 0, 127, 32);
            close = MakeButton("×", Input, Foreground);
            close.AccessibleName = "Close";
            close.SetBounds(350, 1, 29, 28);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Click += (sender, args) => Close();
            shortcut = MakeLabel("", Muted, ContentAlignment.MiddleRight);
            shortcut.SetBounds(162, 0, 217, 32);
            shortcut.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            shortcut.Visible = false;
            header.Width = ClientSize.Width - Padding.Horizontal;
            header.Controls.AddRange(new Control[] { mark, title, shortcut, close });
            header.MouseDown += DragWindow;
            title.MouseDown += DragWindow;
            shortcut.MouseDown += DragWindow;
            tips.SetToolTip(title, "Drag to move. Drag a window edge to resize.");

            controls = new Panel { Dock = DockStyle.Top, Height = 76, Width = header.Width };
            var entry = new Panel { BackColor = Input, Location = new Point(6, 2), Size = new Size(286, 32) };
            entry.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            var prefix = MakeLabel("#", Accent, ContentAlignment.MiddleCenter);
            prefix.SetBounds(3, 0, 26, 32);
            channel = new TextBox
            {
                AccessibleName = "Twitch username",
                BorderStyle = BorderStyle.None,
                BackColor = Input,
                ForeColor = Foreground,
                Location = new Point(29, 7),
                Width = 249,
                MaxLength = 30,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            entry.Controls.AddRange(new Control[] { prefix, channel });
            var connect = MakeButton("Connect", Color.FromArgb(111, 65, 188), Color.White);
            connect.SetBounds(299, 2, 80, 32);
            connect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            connect.Click += (sender, args) => ConnectChannel();
            AcceptButton = connect;

            opacityLabel = MakeLabel("Opacity 84%", Muted, ContentAlignment.MiddleLeft);
            opacityLabel.SetBounds(7, 42, 95, 25);
            var opacitySlider = new TrackBar
            {
                AccessibleName = "Overlay opacity",
                Minimum = 30,
                Maximum = 95,
                Value = 84,
                TickStyle = TickStyle.None,
                AutoSize = false,
                BackColor = Background,
                Location = new Point(101, 44),
                Size = new Size(157, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            opacitySlider.ValueChanged += (sender, args) =>
            {
                Opacity = opacitySlider.Value / 100.0;
                opacityLabel.Text = "Opacity " + opacitySlider.Value + "%";
            };
            lockButton = MakeButton("Click through", Input, Accent);
            lockButton.SetBounds(266, 40, 113, 29);
            lockButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lockButton.Click += (sender, args) => ToggleClickThrough();
            tips.SetToolTip(lockButton, "Pass mouse clicks to the game. Press " + Shortcut + " to unlock.");
            controls.Controls.AddRange(new Control[] { entry, connect, opacityLabel, opacitySlider, lockButton });

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 28 };
            status = MakeLabel("Enter a Twitch username to start", Muted, ContentAlignment.MiddleLeft);
            status.Dock = DockStyle.Fill;
            status.Padding = new Padding(7, 3, 4, 0);
            footer.Controls.Add(status);

            var chatPanel = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(9, 7, 5, 7) };
            feed = new RichTextBox
            {
                AccessibleName = "Live chat messages",
                BackColor = Surface,
                ForeColor = Foreground,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = false,
                Visible = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                HideSelection = true
            };
            empty = MakeLabel("Messages appear here as they’re posted.", Muted, ContentAlignment.MiddleCenter);
            empty.Dock = DockStyle.Fill;
            empty.BackColor = Surface;
            chatPanel.Controls.Add(feed);
            chatPanel.Controls.Add(empty);
            empty.BringToFront();
            Controls.AddRange(new Control[] { chatPanel, footer, controls, header });
            ResumeLayout(true);

            refresh.Tick += (sender, args) => RefreshChat();
            Shown += (sender, args) =>
            {
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(work.Right - Width - 18, work.Top + 18);
                channel.Focus();
            };
        }

        private static Icon GetAppIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static Label MakeLabel(string text, Color color, ContentAlignment alignment)
        {
            return new Label { Text = text, ForeColor = color, TextAlign = alignment, AutoEllipsis = true, UseMnemonic = false };
        }

        private static Button MakeButton(string text, Color background, Color foreground)
        {
            var button = new Button
            {
                Text = text, BackColor = background, ForeColor = foreground, FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false, Cursor = Cursors.Hand, Margin = Padding.Empty
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void ConnectChannel()
        {
            string name = TwitchChat.NormalizeChannel(channel.Text);
            if (name == null)
            {
                ShowStatus("Use letters, numbers or underscores", Warning);
                channel.Focus();
                channel.SelectAll();
                return;
            }

            channel.Text = name;
            if (chat == null || chat.Channel != name)
            {
                history.Clear();
                feed.Clear();
                feed.Hide();
                empty.Show();
            }
            if (chat != null)
                chat.Dispose();
            chat = new TwitchChat(name);
            lastStatus = null;
            RefreshChat();
            refresh.Start();
        }

        private void RefreshChat()
        {
            ChatStatus current = chat.Status;
            if (current != lastStatus)
            {
                lastStatus = current;
                Color color = current.State == ConnectionState.Live ? Live : Muted;
                if (current.State == ConnectionState.Retrying || current.State == ConnectionState.Unavailable)
                    color = Warning;
                ShowStatus(current.Text, color);
            }

            IrcLine[] lines = chat.Drain();
            if (lines.Length == 0)
            {
                if (current.State == ConnectionState.Unavailable)
                    refresh.Stop();
                return;
            }
            int firstNew = history.Count;
            bool redraw = false;
            foreach (IrcLine line in lines)
            {
                switch (line.Command)
                {
                    case "PRIVMSG":
                        history.Add(new ChatMessage(line));
                        break;
                    case "CLEARMSG":
                        string id = line.Tag("target-msg-id");
                        if (id.Length > 0)
                            redraw |= history.RemoveAll(message => message.Id == id) > 0;
                        break;
                    case "CLEARCHAT":
                        if (String.IsNullOrEmpty(line.Trailing))
                        {
                            history.Clear();
                            redraw = true;
                        }
                        else
                            redraw |= history.RemoveAll(message => String.Equals(message.Login, line.Trailing,
                                StringComparison.OrdinalIgnoreCase)) > 0;
                        break;
                }
            }
            if (history.Count > 240)
            {
                history.RemoveRange(0, history.Count - 200);
                redraw = true;
            }
            if (redraw)
            {
                feed.Clear();
                firstNew = 0;
            }
            for (int index = firstNew; index < history.Count; index++)
            {
                ChatMessage message = history[index];
                AppendText(message.Time + "  ", Muted, Font);
                AppendText(message.Name, Accent, nameFont);
                AppendText(message.Text + "\n", Foreground, Font);
            }
            bool hasMessages = history.Count > 0;
            feed.Visible = hasMessages;
            empty.Visible = !hasMessages;
            if (hasMessages)
                feed.ScrollToCaret();
        }

        private void AppendText(string text, Color color, Font font)
        {
            feed.Select(feed.TextLength, 0);
            feed.SelectionColor = color;
            feed.SelectionFont = font;
            feed.SelectedText = text;
        }

        private void ShowStatus(string text, Color color)
        {
            status.Text = text;
            status.ForeColor = color;
        }

        private void DragWindow(object sender, MouseEventArgs args)
        {
            if (args.Button != MouseButtons.Left)
                return;
            Native.ReleaseCapture();
            Native.SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
        }

        private void ToggleClickThrough()
        {
            if (!hotkeyRegistered)
                return;
            clickThrough = !clickThrough;
            int style = Native.GetWindowLong(Handle, -20);
            const int passInput = 0x20 | 0x08000000; // WS_EX_TRANSPARENT | WS_EX_NOACTIVATE
            Native.SetWindowLong(Handle, -20, clickThrough ? style | passInput : style & ~passInput);
            controls.Visible = !clickThrough;
            close.Visible = !clickThrough;
            shortcut.Visible = clickThrough;
            shortcut.Text = clickThrough ? Shortcut + " to unlock" : "";
        }

        protected override void OnHandleCreated(EventArgs args)
        {
            base.OnHandleCreated(args);
            hotkeyRegistered = Native.RegisterHotKey(Handle, HotkeyId, 0x4006, (uint)Keys.F10);
            lockButton.Enabled = hotkeyRegistered;
            if (!hotkeyRegistered)
                tips.SetToolTip(lockButton, Shortcut + " is already in use.");
        }

        protected override void OnHandleDestroyed(EventArgs args)
        {
            if (hotkeyRegistered)
                Native.UnregisterHotKey(Handle, HotkeyId);
            base.OnHandleDestroyed(args);
        }

        protected override void OnFormClosed(FormClosedEventArgs args)
        {
            refresh.Stop();
            if (chat != null)
                chat.Dispose();
            base.OnFormClosed(args);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                refresh.Dispose();
                tips.Dispose();
                nameFont.Dispose();
                if (brandMark != null)
                    brandMark.Dispose();
                if (appIcon != null)
                    appIcon.Dispose();
            }
        }

        protected override void WndProc(ref Message message)
        {
            const int hitTest = 0x0084;
            const int hotkey = 0x0312;
            if (message.Msg == hotkey && message.WParam.ToInt32() == HotkeyId)
            {
                ToggleClickThrough();
                return;
            }
            base.WndProc(ref message);
            if (message.Msg != hitTest || clickThrough || message.Result.ToInt32() != 1)
                return;
            long position = message.LParam.ToInt64();
            Point point = PointToClient(new Point((short)position, (short)(position >> 16)));
            const int grip = 7;
            bool left = point.X < grip, right = point.X >= ClientSize.Width - grip;
            bool top = point.Y < grip, bottom = point.Y >= ClientSize.Height - grip;
            int hit = 1;
            if (top) hit = left ? 13 : right ? 14 : 12;
            else if (bottom) hit = left ? 16 : right ? 17 : 15;
            else if (left) hit = 10;
            else if (right) hit = 11;
            message.Result = new IntPtr(hit);
        }

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Overlay());
        }

        private static class Native
        {
            [DllImport("user32.dll")] public static extern bool ReleaseCapture();
            [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr window, int index);
            [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr window, int index, int value);
            [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
            [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window, int id);
        }
    }
}
