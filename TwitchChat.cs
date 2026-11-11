using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchChatWindow
{
    internal enum ConnectionState { Connecting, Live, Retrying, Unavailable }

    internal sealed class ChatStatus
    {
        public readonly ConnectionState State;
        public readonly string Text;

        public ChatStatus(ConnectionState state, string text)
        {
            State = state;
            Text = text;
        }
    }

    internal sealed class TwitchChat : IDisposable
    {
        private static readonly Uri Endpoint = new Uri("wss://irc-ws.chat.twitch.tv:443");
        private static readonly Regex ChannelName = new Regex(@"\A[a-z0-9_]{1,25}\z");
        private static readonly IrcLine[] NoLines = new IrcLine[0];
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly Queue<IrcLine> pending = new Queue<IrcLine>();
        private readonly Task worker;
        private volatile ChatStatus status;

        public readonly string Channel;
        public ChatStatus Status { get { return status; } }

        public static string NormalizeChannel(string input)
        {
            string channel = input.Trim().ToLowerInvariant();
            if (channel.StartsWith("@") || channel.StartsWith("#"))
                channel = channel.Substring(1);
            return ChannelName.IsMatch(channel) ? channel : null;
        }

        public TwitchChat(string channel)
        {
            Channel = channel;
            SetStatus(ConnectionState.Connecting, "Connecting to #" + channel + "...");
            worker = Task.Run(() => RunAsync(stop.Token));
        }

        public IrcLine[] Drain()
        {
            lock (pending)
            {
                if (pending.Count == 0)
                    return NoLines;
                IrcLine[] lines = pending.ToArray();
                pending.Clear();
                return lines;
            }
        }

        private void SetStatus(ConnectionState state, string text)
        {
            status = new ChatStatus(state, text);
        }

        private async Task RunAsync(CancellationToken cancellation)
        {
            int delay = 1;
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    await ListenAsync(cancellation).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    if (!(ex is WebSocketException || ex is IOException || ex is OperationCanceledException ||
                          ex is System.Net.WebException || ex is System.Net.Http.HttpRequestException))
                        throw;
                }

                if (cancellation.IsCancellationRequested)
                    return;
                bool lostConnection = Status.State == ConnectionState.Live;
                if (lostConnection)
                    delay = 1;
                string reason = lostConnection ? "Connection lost" : "Chat unavailable";
                SetStatus(ConnectionState.Retrying, reason + " · retrying in " + delay + "s");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), cancellation).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                delay = Math.Min(delay * 2, 30);
                SetStatus(ConnectionState.Connecting, "Connecting to #" + Channel + "...");
            }
        }

        private async Task ListenAsync(CancellationToken cancellation)
        {
            string nick = "justinfan" + new Random().Next(10000000, 99999999);
            string room = "#" + Channel;
            bool welcomed = false;
            bool joined = false;
            using (var socket = new ClientWebSocket())
            using (var watchdog = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (var packet = new MemoryStream())
            {
                watchdog.CancelAfter(TimeSpan.FromSeconds(15));
                CancellationToken token = watchdog.Token;
                await socket.ConnectAsync(Endpoint, token).ConfigureAwait(false);
                await SendAsync(socket, "CAP REQ :twitch.tv/tags twitch.tv/commands", token).ConfigureAwait(false);
                await SendAsync(socket, "PASS SCHMOOPIIE", token).ConfigureAwait(false);
                await SendAsync(socket, "NICK " + nick, token).ConfigureAwait(false);
                await SendAsync(socket, "JOIN " + room, token).ConfigureAwait(false);

                var buffer = new ArraySegment<byte>(new byte[8192]);
                while (true)
                {
                    WebSocketReceiveResult result;
                    try
                    {
                        result = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (!cancellation.IsCancellationRequested && welcomed && !joined)
                        {
                            SetStatus(ConnectionState.Unavailable, "No public chat found for " + room);
                            return;
                        }
                        throw;
                    }
                    if (result.MessageType == WebSocketMessageType.Close)
                        throw new IOException("Twitch closed the chat connection.");
                    if (result.MessageType != WebSocketMessageType.Text || packet.Length + result.Count > 256 * 1024)
                        throw new IOException("Unexpected Twitch chat response.");
                    packet.Write(buffer.Array, buffer.Offset, result.Count);
                    if (!result.EndOfMessage)
                        continue;

                    string payload = Encoding.UTF8.GetString(packet.GetBuffer(), 0, (int)packet.Length);
                    packet.SetLength(0);
                    foreach (string raw in payload.Split('\n'))
                    {
                        IrcLine line = IrcLine.Parse(raw.TrimEnd('\r'));
                        if (line == null)
                            continue;
                        if (line.Command == "PING")
                        {
                            await SendAsync(socket, "PONG :" + (line.Trailing ?? line.Target), token).ConfigureAwait(false);
                            continue;
                        }
                        if (line.Command == "RECONNECT")
                            throw new IOException("Twitch requested a reconnect.");
                        if (line.Command == "001")
                            welcomed = true;
                        if (!joined && line.Command == "NOTICE" && line.Target == "*")
                        {
                            SetStatus(ConnectionState.Unavailable, "Twitch declined the guest connection");
                            return;
                        }
                        if (!joined && line.Command == "ROOMSTATE" && line.Target == room)
                        {
                            joined = true;
                            SetStatus(ConnectionState.Live, "Live chat · " + room);
                        }
                        if (line.Target != room)
                            continue;
                        if (!joined && line.Command == "NOTICE")
                        {
                            SetStatus(ConnectionState.Unavailable, "Twitch could not open " + room);
                            return;
                        }
                        if (line.Command == "PRIVMSG" || line.Command == "CLEARMSG" || line.Command == "CLEARCHAT")
                        {
                            lock (pending)
                            {
                                if (pending.Count == 512)
                                    pending.Dequeue();
                                pending.Enqueue(line);
                            }
                        }
                    }
                    if (joined)
                        watchdog.CancelAfter(TimeSpan.FromMinutes(7));
                }
            }
        }

        private static Task SendAsync(ClientWebSocket socket, string command, CancellationToken cancellation)
        {
            byte[] data = Encoding.UTF8.GetBytes(command + "\r\n");
            return socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cancellation);
        }

        public void Dispose()
        {
            stop.Cancel();
            worker.ContinueWith(task => stop.Dispose(), TaskScheduler.Default);
        }
    }

    internal sealed class IrcLine
    {
        private readonly string tags;
        public readonly string Login;
        public readonly string Command;
        public readonly string Target;
        public readonly string Trailing;

        private IrcLine(string tags, string login, string command, string target, string trailing)
        {
            this.tags = tags;
            Login = login;
            Command = command;
            Target = target;
            Trailing = trailing;
        }

        public static IrcLine Parse(string line)
        {
            if (line.Length == 0)
                return null;
            string tags = "", login = "";
            int start = 0;
            if (line[start] == '@')
            {
                int end = line.IndexOf(' ', start);
                if (end < 0 || end + 1 == line.Length)
                    return null;
                tags = line.Substring(1, end - 1);
                start = end + 1;
            }
            if (line[start] == ':')
            {
                int end = line.IndexOf(' ', start);
                if (end < 0 || end + 1 == line.Length)
                    return null;
                int bang = line.IndexOf('!', start, end - start);
                login = line.Substring(start + 1, (bang < 0 ? end : bang) - start - 1);
                start = end + 1;
            }
            int separator = line.IndexOf(' ', start);
            if (separator < 0)
                return new IrcLine(tags, login, line.Substring(start), "", null);
            string command = line.Substring(start, separator - start);
            string arguments = line.Substring(separator + 1);
            int trailing = arguments.Length > 0 && arguments[0] == ':' ? 0 : arguments.IndexOf(" :", StringComparison.Ordinal);
            int argumentEnd = arguments.IndexOf(' ');
            string target = trailing == 0 ? "" : argumentEnd < 0 ? arguments : arguments.Substring(0, argumentEnd);
            string text = trailing < 0 ? null : arguments.Substring(trailing == 0 ? 1 : trailing + 2);
            return new IrcLine(tags, login, command, target, text);
        }

        public string Tag(string key)
        {
            foreach (string tag in tags.Split(';'))
            {
                if (tag.StartsWith(key + "=", StringComparison.Ordinal))
                    return tag.Substring(key.Length + 1);
            }
            return "";
        }
    }

    internal sealed class ChatHistory
    {
        private readonly List<ChatMessage> messages = new List<ChatMessage>();

        public int Count { get { return messages.Count; } }
        public ChatMessage this[int index] { get { return messages[index]; } }
        public void Clear() { messages.Clear(); }

        // Return the first row to render. A removal requires rebuilding the text buffer.
        public int Apply(IrcLine[] lines)
        {
            int firstNew = messages.Count;
            foreach (IrcLine line in lines)
            {
                switch (line.Command)
                {
                    case "PRIVMSG":
                        messages.Add(new ChatMessage(line));
                        break;
                    case "CLEARMSG":
                        string id = line.Tag("target-msg-id");
                        if (id.Length > 0 && messages.RemoveAll(message => message.Id == id) > 0)
                            firstNew = 0;
                        break;
                    case "CLEARCHAT":
                        if (String.IsNullOrEmpty(line.Trailing))
                        {
                            messages.Clear();
                            firstNew = 0;
                        }
                        else if (messages.RemoveAll(message => String.Equals(message.Login, line.Trailing,
                            StringComparison.OrdinalIgnoreCase)) > 0)
                            firstNew = 0;
                        break;
                }
            }
            if (messages.Count > 240)
            {
                messages.RemoveRange(0, messages.Count - 200);
                firstNew = 0;
            }
            return firstNew;
        }
    }

    internal sealed class ChatMessage
    {
        public readonly string Id;
        public readonly string Login;
        public readonly string Name;
        public readonly string Text;
        public readonly string Time;

        public ChatMessage(IrcLine line)
        {
            Id = line.Tag("id");
            Login = line.Login;
            string displayName = line.Tag("display-name");
            Name = displayName.Length == 0 ? Login : displayName;
            string body = line.Trailing ?? "";
            bool action = body.StartsWith("\u0001ACTION ", StringComparison.Ordinal) && body.EndsWith("\u0001", StringComparison.Ordinal);
            Text = action ? " " + body.Substring(8, body.Length - 9) : ": " + body;

            long milliseconds;
            DateTimeOffset timestamp = DateTimeOffset.Now;
            if (long.TryParse(line.Tag("tmi-sent-ts"), NumberStyles.None, CultureInfo.InvariantCulture, out milliseconds) &&
                milliseconds <= 253402300799999L)
                timestamp = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToLocalTime();
            Time = timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
