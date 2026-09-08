using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace JustConvert.Core;

public record QueueIpcMessage(List<string> Files, string TargetFormat, string? OutputPath);

public static class ConversionQueueIpc
{
    private static string GetPipeName() => $"JustConvert_Queue_{Environment.UserName}";

    public static bool TrySend(IReadOnlyList<string> files, string targetFormat, string? outputPath = null, int timeoutMs = 1500)
    {
        if (files.Count == 0) return false;

        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", GetPipeName(), PipeDirection.InOut);
                var remaining = (int)Math.Max(50, deadline - Environment.TickCount64);
                client.Connect(remaining);

                var message = new QueueIpcMessage([.. files], targetFormat, outputPath);
                var json = JsonSerializer.Serialize(message);

                using var writer = new StreamWriter(client, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
                writer.WriteLine(json);

                using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);
                var response = reader.ReadLine();
                return response == "OK";
            }
            catch
            {
                if (Environment.TickCount64 >= deadline) break;
                Thread.Sleep(50);
            }
        }

        return false;
    }

    public static IDisposable StartServer(Action<QueueIpcMessage> onMessageReceived)
    {
        var cts = new CancellationTokenSource();
        var thread = new Thread(() => ServerLoop(onMessageReceived, cts.Token))
        {
            IsBackground = true,
            Name = "JustConvert_IpcServer"
        };
        thread.Start();

        return new ServerSubscription(cts);
    }

    private static void ServerLoop(Action<QueueIpcMessage> onMessageReceived, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    GetPipeName(),
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                server.WaitForConnectionAsync(ct).GetAwaiter().GetResult();

                var connectedServer = server;
                server = null;

                _ = Task.Run(() => HandleClient(connectedServer, onMessageReceived), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (ct.IsCancellationRequested) break;
                Thread.Sleep(50);
            }
            finally
            {
                try
                {
                    server?.Dispose();
                }
                catch { }
            }
        }
    }

    private static void HandleClient(NamedPipeServerStream server, Action<QueueIpcMessage> onMessageReceived)
    {
        try
        {
            using (server)
            {
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);
                var line = reader.ReadLine();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    var msg = JsonSerializer.Deserialize<QueueIpcMessage>(line);

                    using var writer = new StreamWriter(server, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
                    writer.WriteLine("OK");

                    if (msg != null && msg.Files.Count > 0)
                    {
                        onMessageReceived(msg);
                    }
                }
            }
        }
        catch { }
    }

    private sealed class ServerSubscription(CancellationTokenSource cts) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch { }
        }
    }
}