// TODO Clean up this file some

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

    public IrcClient(string server, int port, string nick, string username, string realName, string password)
    {
        _server = server;
        _port = port;
        _nick = nick;
        _username = username;
        _realName = realName;
        _password = password;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        // Connect to server
        using var client = new TcpClient();
        await client.ConnectAsync(_server, _port, ct);

        await using var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = _server
        }, ct);

        using var reader = new StreamReader(sslStream, Encoding.UTF8);
        using var writer = new StreamWriter(sslStream, new UTF8Encoding(false))
        {
            NewLine = "\r\n"
        };

        // IRC Registration
        await SendAsync(writer, "CAP LS 302", ct);
        await SendAsync(writer, $"NICK {_nick}", ct);
        await SendAsync(writer, $"USER {_username} 0 * :{_realName}", ct);

        // Recive Loop
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);

            if (line is null)
                break;

            if (line.StartsWith("PING ", StringComparison.Ordinal))
            {
                await SendAsync(writer, $"PONG {line[5..]}", ct);
                continue;
            }

            Console.WriteLine($"<< {line}");

            if (line.Contains(" CAP ") &&
                line.Contains(" LS ") &&
                line.Contains("sasl"))
            {
                await SendAsync(writer, "CAP REQ :sasl", ct);
            }
            else if (line.Contains(" CAP ") &&
                     line.Contains(" ACK ") &&
                     line.Contains("sasl"))
            {
                await SendAsync(writer, "AUTHENTICATE PLAIN", ct);
            }
            else if (line == "AUTHENTICATE +")
            {
                await SendSaslCredentialsAsync(writer, ct);
            }
            else if (line.Contains(" 903 "))
            {
                await SendAsync(writer, "CAP END", ct);
            }
        }

    }
    private async Task SendSaslCredentialsAsync(
        StreamWriter writer,
        CancellationToken ct)
    {
        var payload = $"{_username}\0{_username}\0{_password}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var base64 = Convert.ToBase64String(bytes);

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
        await writer.WriteLineAsync(line.AsMemory(), ct);
        await writer.FlushAsync(ct);

        if (log)
            Console.WriteLine($">> {line}");
    }
}
