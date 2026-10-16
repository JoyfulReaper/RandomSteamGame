// TODO Clean up this file some
// It's ugly ad hell I know
// Maybe I'll clean it someday if anyone besides me uses it

using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace RandomSteamGame.IrcBot;

public class IrcClient
{
    private readonly string _server;
    private readonly int _port;
    private readonly string _nick;
    private readonly string _username;
    private readonly string _realName;
    private readonly string _password;
    private readonly string _channel;

    private readonly RandomSteamClient _randomSteamClient;

    // TODO consider putting cool down in config
    private static readonly TimeSpan RandomCommandCooldown =
        TimeSpan.FromSeconds(10);

    private static readonly TimeSpan ReconnectDelay =
        TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, DateTimeOffset> _randomCommandUsage =
        new(StringComparer.OrdinalIgnoreCase);

    public IrcClient(
        string server,
        int port,
        string nick,
        string username,
        string realName,
        string password,
        string channel,
        RandomSteamClient randomSteamClient)
    {
        _server = server;
        _port = port;
        _nick = nick;
        _username = username;
        _realName = realName;
        _password = password;
        _channel = channel;
        _randomSteamClient = randomSteamClient;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunConnectionAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IRC connection failed: {ex}");
            }

            if (ct.IsCancellationRequested)
                break;

            Console.WriteLine(
                $"IRC disconnected. Reconnecting in {ReconnectDelay.TotalSeconds:0}s...");

            try
            {
                await Task.Delay(ReconnectDelay, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunConnectionAsync(CancellationToken ct)
    {
        // Connect to server
        using var client = new TcpClient();
        await client.ConnectAsync(_server, _port, ct);

        await using var sslStream =
            new SslStream(
                client.GetStream(),
                leaveInnerStreamOpen: false);

        await sslStream.AuthenticateAsClientAsync(
            new SslClientAuthenticationOptions
            {
                TargetHost = _server
            },
            ct);

        using var reader =
            new StreamReader(
                sslStream,
                Encoding.UTF8);

        using var writer =
            new StreamWriter(
                sslStream,
                new UTF8Encoding(false))
            {
                NewLine = "\r\n"
            };

        // IRC Registration
        await SendAsync(
            writer,
            "CAP LS 302",
            ct);

        await SendAsync(
            writer,
            $"NICK {_nick}",
            ct);

        await SendAsync(
            writer,
            $"USER {_username} 0 * :{_realName}",
            ct);

        // Receive Loop
        while (!ct.IsCancellationRequested)
        {
            var line =
                await reader.ReadLineAsync(ct);

            if (line is null)
                break;

            if (line.StartsWith(
                    "PING ",
                    StringComparison.Ordinal))
            {
                await SendAsync(
                    writer,
                    $"PONG {line[5..]}",
                    ct);

                continue;
            }

            Console.WriteLine($"<< {line}");

            if (line.Contains(" CAP ") &&
                line.Contains(" LS ") &&
                line.Contains("sasl"))
            {
                await SendAsync(
                    writer,
                    "CAP REQ :sasl",
                    ct);
            }
            else if (line.Contains(" CAP ") &&
                     line.Contains(" ACK ") &&
                     line.Contains("sasl"))
            {
                await SendAsync(
                    writer,
                    "AUTHENTICATE PLAIN",
                    ct);
            }
            else if (line == "AUTHENTICATE +")
            {
                await SendSaslCredentialsAsync(
                    writer,
                    ct);
            }
            else if (line.Contains(" 903 "))
            {
                await SendAsync(
                    writer,
                    "CAP END",
                    ct);
            }
            else if (line.Contains(" 001 "))
            {
                await SendAsync(
                    writer,
                    $"JOIN {_channel}",
                    ct);
            }

            if (line.Contains(" PRIVMSG "))
            {
                var messageIndex =
                    line.IndexOf(
                        " :",
                        StringComparison.Ordinal);

                if (messageIndex >= 0)
                {
                    var message =
                        line[(messageIndex + 2)..];

                    Console.WriteLine(
                        $"MESSAGE: {message}");

                    if (message.Equals(
                            "!hello",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        await SendAsync(
                            writer,
                            $"PRIVMSG {_channel} :Hello!",
                            ct);
                    }

                    if (message.Equals(
                            "!random",
                            StringComparison.OrdinalIgnoreCase) ||
                        message.StartsWith(
                            "!random ",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        var identifier =
                            message.Length > "!random".Length
                                ? message["!random".Length..].Trim()
                                : string.Empty;

                        if (string.IsNullOrWhiteSpace(identifier))
                        {
                            await SendAsync(
                                writer,
                                $"PRIVMSG {_channel} :Usage: !random <SteamID or vanity name>",
                                ct);

                            continue;
                        }

                        if (identifier.Length > 100)
                        {
                            await SendAsync(
                                writer,
                                $"PRIVMSG {_channel} :That Steam ID or vanity name looks invalid.",
                                ct);

                            continue;
                        }

                        var sender =
                            GetSenderNick(line);

                        if (sender is not null &&
                            _randomCommandUsage.TryGetValue(
                                sender,
                                out var lastUsed))
                        {
                            var elapsed =
                                DateTimeOffset.UtcNow - lastUsed;

                            if (elapsed < RandomCommandCooldown)
                            {
                                var remaining =
                                    (int)Math.Ceiling(
                                        (RandomCommandCooldown - elapsed)
                                        .TotalSeconds);

                                await SendAsync(
                                    writer,
                                    $"PRIVMSG {_channel} :{sender}: please wait {remaining}s before using !random again.",
                                    ct);

                                continue;
                            }
                        }

                        if (sender is not null)
                        {
                            _randomCommandUsage[sender] =
                                DateTimeOffset.UtcNow;
                        }

                        try
                        {
                            var game =
                                await _randomSteamClient
                                    .GetRandomGameAsync(
                                        identifier,
                                        ct);

                            await SendAsync(
                                writer,
                                $"PRIVMSG {_channel} :🎲 You should play: {game?.Name} - https://store.steampowered.com/app/{game?.Id}/",
                                ct);
                        }
                        catch (OperationCanceledException)
                            when (ct.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            await SendAsync(
                                writer,
                                $"PRIVMSG {_channel} :Failed to pick game",
                                ct);

                            Console.WriteLine(
                                $"Failed to pick game: {identifier}\n{ex}");
                        }
                    }
                }
            }
        }
    }

    private async Task SendSaslCredentialsAsync(
        StreamWriter writer,
        CancellationToken ct)
    {
        var payload =
            $"{_username}\0{_username}\0{_password}";

        var bytes =
            Encoding.UTF8.GetBytes(payload);

        var base64 =
            Convert.ToBase64String(bytes);

        await SendAsync(
            writer,
            $"AUTHENTICATE {base64}",
            ct,
            log: false);
    }

    private async Task SendAsync(
        StreamWriter writer,
        string line,
        CancellationToken ct,
        bool log = true)
    {
        await writer.WriteLineAsync(
            line.AsMemory(),
            ct);

        await writer.FlushAsync(ct);

        if (log)
            Console.WriteLine($">> {line}");
    }

    private static string? GetSenderNick(
        string line)
    {
        if (!line.StartsWith(':'))
            return null;

        var separator =
            line.IndexOf('!');

        if (separator <= 1)
            return null;

        return line[1..separator];
    }
}