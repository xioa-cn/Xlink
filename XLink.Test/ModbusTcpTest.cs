using XLinkCore;
using XLinkCore.ModbusTcp;
using System.Net;
using System.Net.Sockets;

namespace XLink.Test;

public class ModbusTcpTest : IDisposable
{
    public ModbusTcp ModbusTcp { get; } = new ModbusTcp("127.0.0.1", 502)
    {
        ConnectTimeOut = 2000,
        ReceiveTimeOut = 2000,
    };

    [Fact]
    public void ScalarReadWrite_AllSupportedTypes()
    {
        AssertSuccess(ModbusTcp.Connect());
        WriteReadAssert("hr:0", (short)-123);
        WriteReadAssert("hr:10", (short)-123);
        WriteReadAssert("hr:11", (ushort)123);
        WriteReadAssert("hr:12", -123456);
        WriteReadAssert("hr:14", 123456u);
        WriteReadAssert("hr:16", -1234567890123L);
        WriteReadAssert("hr:20", 1234567890123UL);
        WriteReadAssert("hr:24", 12.5f);
        WriteReadAssert("hr:26", 1234.5678d);
        WriteReadAssert("hr:30", (byte)88);
        WriteReadAssertString("hr:31", "TEST", 2);
        WriteReadAssert("coil:1", true);
    }

    [Fact]
    public void ArrayReadWrite_AllSupportedTypes()
    {
        AssertSuccess(ModbusTcp.Connect());

        WriteReadArrayAssert("hr:100", new short[] { -1, 2, 3, 4 });
        WriteReadArrayAssert("hr:110", new ushort[] { 1, 2, 3, 4 });
        WriteReadArrayAssert("hr:120", new[] { -1, 2, 3, 4 });
        WriteReadArrayAssert("hr:140", new uint[] { 1, 2, 3, 4 });
        WriteReadArrayAssert("hr:160", new[] { -1L, 2L, 3L, 4L });
        WriteReadArrayAssert("hr:190", new[] { 1UL, 2UL, 3UL, 4UL });
        WriteReadArrayAssert("hr:220", new[] { 1.1f, 2.2f, 3.3f, 4.4f });
        WriteReadArrayAssert("hr:240", new[] { 1.11d, 2.22d, 3.33d, 4.44d });
        WriteReadArrayAssert("hr:280", new byte[] { 1, 2, 3, 4 });
        WriteReadArrayAssert("coil:20", new[] { true, false, true, true });
    }

    [Fact]
    public async Task ScalarReadWriteAsync_AllSupportedTypes()
    {
        AssertSuccess(await ModbusTcp.ConnectAsync());

        await WriteReadAssertAsync("hr:10", (short)-123);
        await WriteReadAssertAsync("hr:11", (ushort)123);
        await WriteReadAssertAsync("hr:12", -123456);
        await WriteReadAssertAsync("hr:14", 123456u);
        await WriteReadAssertAsync("hr:16", -1234567890123L);
        await WriteReadAssertAsync("hr:20", 1234567890123UL);
        await WriteReadAssertAsync("hr:24", 12.5f);
        await WriteReadAssertAsync("hr:26", 1234.5678d);
        await WriteReadAssertAsync("hr:30", (byte)88);
        await WriteReadAssertStringAsync("hr:31", "TEST", 2);
        await WriteReadAssertAsync("coil:1", true);
    }

    [Fact]
    public async Task ArrayReadWriteAsync_AllSupportedTypes()
    {
        AssertSuccess(await ModbusTcp.ConnectAsync());

        await WriteReadArrayAssertAsync("hr:100", new short[] { -1, 2, 3, 4 });
        await WriteReadArrayAssertAsync("hr:110", new ushort[] { 1, 2, 3, 4 });
        await WriteReadArrayAssertAsync("hr:120", new[] { -1, 2, 3, 4 });
        await WriteReadArrayAssertAsync("hr:140", new uint[] { 1, 2, 3, 4 });
        await WriteReadArrayAssertAsync("hr:160", new[] { -1L, 2L, 3L, 4L });
        await WriteReadArrayAssertAsync("hr:190", new[] { 1UL, 2UL, 3UL, 4UL });
        await WriteReadArrayAssertAsync("hr:220", new[] { 1.1f, 2.2f, 3.3f, 4.4f });
        await WriteReadArrayAssertAsync("hr:240", new[] { 1.11d, 2.22d, 3.33d, 4.44d });
        await WriteReadArrayAssertAsync("hr:280", new byte[] { 1, 2, 3, 4 });
        await WriteReadArrayAssertAsync("coil:20", new[] { true, false, true, true });
    }

    [Fact]
    public async Task ConcurrentAsyncReadWrite_UsesSingleConnectionSafely()
    {
        AssertSuccess(await ModbusTcp.ConnectAsync());

        const int taskCount = 16;
        const int loopCount = 20;
        Task[] tasks = Enumerable.Range(0, taskCount)
            .Select(taskIndex => Task.Run(async () =>
            {
                string intPoint = $"hr:{400 + taskIndex * 4}";
                string boolPoint = $"coil:{100 + taskIndex}";

                for (int i = 0; i < loopCount; i++)
                {
                    int expected = taskIndex * 10000 + i;
                    bool expectedBool = (taskIndex + i) % 2 == 0;

                    AssertSuccess(await ModbusTcp.WriteAsync(intPoint, expected));
                    int actual = AssertSuccess(await ModbusTcp.ReadAsync<int>(intPoint));
                    Assert.Equal(expected, actual);

                    AssertSuccess(await ModbusTcp.WriteAsync(boolPoint, expectedBool));
                    bool actualBool = AssertSuccess(await ModbusTcp.ReadAsync<bool>(boolPoint));
                    Assert.Equal(expectedBool, actualBool);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void ConcurrentSyncReadWrite_UsesSingleConnectionSafely()
    {
        AssertSuccess(ModbusTcp.Connect());

        const int taskCount = 16;
        const int loopCount = 20;

        Parallel.For(0, taskCount, taskIndex =>
        {
            string intPoint = $"hr:{600 + taskIndex * 4}";
            string boolPoint = $"coil:{200 + taskIndex}";

            for (int i = 0; i < loopCount; i++)
            {
                int expected = taskIndex * 10000 + i;
                bool expectedBool = (taskIndex + i) % 2 == 0;

                AssertSuccess(ModbusTcp.Write(intPoint, expected));
                int actual = AssertSuccess(ModbusTcp.Read<int>(intPoint));
                Assert.Equal(expected, actual);

                AssertSuccess(ModbusTcp.Write(boolPoint, expectedBool));
                bool actualBool = AssertSuccess(ModbusTcp.Read<bool>(boolPoint));
                Assert.Equal(expectedBool, actualBool);
            }
        });
    }

    [Fact]
    public async Task ConcurrentAsyncArrayReadWrite_UsesSingleConnectionSafely()
    {
        AssertSuccess(await ModbusTcp.ConnectAsync());

        const int taskCount = 8;
        const int loopCount = 10;
        Task[] tasks = Enumerable.Range(0, taskCount)
            .Select(taskIndex => Task.Run(async () =>
            {
                string point = $"hr:{800 + taskIndex * 20}";

                for (int i = 0; i < loopCount; i++)
                {
                    int[] expected =
                    {
                        taskIndex * 1000 + i,
                        taskIndex * 1000 + i + 1,
                        taskIndex * 1000 + i + 2,
                        taskIndex * 1000 + i + 3
                    };

                    AssertSuccess(await ModbusTcp.WriteAsync(point, expected));
                    int[] actual = AssertSuccess(await ModbusTcp.ReadArrayAsync<int>(point, checked((ushort)expected.Length)));
                    AssertArray(expected, actual);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void Configuration_RejectsLockWaitTimeoutNotGreaterThanReceiveTimeout()
    {
        using ModbusTcp modbusTcp = new ModbusTcp("127.0.0.1", 502);

        Assert.Equal(5000, modbusTcp.LockWaitTimeOut);
        modbusTcp.ReceiveTimeOut = 2000;
        modbusTcp.LockWaitTimeOut = 5000;

        Assert.Throws<ArgumentOutOfRangeException>(() => modbusTcp.LockWaitTimeOut = 2000);
        Assert.Throws<ArgumentOutOfRangeException>(() => modbusTcp.ReceiveTimeOut = 5000);
    }

    [Fact]
    public void Write_SavesParsedWriteResponse()
    {
        AssertSuccess(ModbusTcp.Connect());

        AssertSuccess(ModbusTcp.Write("hr:50", (ushort)1234));

        Assert.NotNull(ModbusTcp.LastWriteResponse);
        ModbusWriteResponse response = ModbusTcp.LastWriteResponse;
        Assert.Equal(6, response.FunctionCode);
        Assert.Equal((ushort)50, response.Address);
        Assert.Equal((ushort)1, response.Quantity);
        Assert.Equal(new byte[] { 0x04, 0xD2 }, response.ValueBytes);
    }

    [Fact]
    public void Statistics_CountsBytesRequestsAndElapsedTime()
    {
        AssertSuccess(ModbusTcp.Connect());
        ModbusTcp.ResetStatistics();

        AssertSuccess(ModbusTcp.Write("hr:60", (ushort)321));
        ushort actual = AssertSuccess(ModbusTcp.Read<ushort>("hr:60"));

        ModbusTcpStatistics statistics = ModbusTcp.GetStatistics();
        Assert.Equal((ushort)321, actual);
        Assert.True(statistics.RequestCount >= 2);
        Assert.Equal(0, statistics.FailedRequestCount);
        Assert.True(statistics.BytesSent > 0);
        Assert.True(statistics.BytesReceived > 0);
        Assert.True(statistics.LastElapsed >= TimeSpan.Zero);
    }

    [Fact]
    public async Task WriteAsync_InvalidEchoReturnsInvalidFrame()
    {
        using FakeModbusServer server = await FakeModbusServer.StartAsync(request =>
        {
            byte[] response = CreateWriteSingleRegisterEcho(request);
            response[8]++;
            return response;
        });
        using ModbusTcp modbusTcp = CreateLocalClient(server.Port);

        AssertSuccess(await modbusTcp.ConnectAsync());
        Result result = await modbusTcp.WriteAsync("hr:1", (ushort)10);

        Assert.False(result.IsSuccess);
        Assert.Equal(ModbusTcpErrorCodes.InvalidFrame, result.ErrorCode);
    }

    [Fact]
    public async Task ReadAsync_TimesOutWhenServerDoesNotRespond()
    {
        using FakeModbusServer server = await FakeModbusServer.StartAsync(_ => null);
        using ModbusTcp modbusTcp = CreateLocalClient(server.Port);
        modbusTcp.ReceiveTimeOut = 100;
        modbusTcp.LockWaitTimeOut = 500;

        AssertSuccess(await modbusTcp.ConnectAsync());
        Result<ushort> result = await modbusTcp.ReadAsync<ushort>("hr:0");

        Assert.False(result.IsSuccess);
        Assert.Equal(ModbusTcpErrorCodes.Timeout, result.ErrorCode);
        Assert.False(modbusTcp.IsConnected);
    }

    [Fact]
    public async Task OriginalByteEx_ReturnsRawResponseFrame()
    {
        using FakeModbusServer server = await FakeModbusServer.StartAsync(CreateReadHoldingRegistersResponse);
        using ModbusTcp modbusTcp = CreateLocalClient(server.Port);
        byte[] request = { 0x12, 0x34, 0x00, 0x00, 0x00, 0x06, 0x01, 0x03, 0x00, 0x00, 0x00, 0x01 };

        AssertSuccess(await modbusTcp.ConnectAsync());
        byte[] response = AssertSuccess(modbusTcp.OriginalByteEx(request));

        Assert.Equal(11, response.Length);
        Assert.Equal(request[0], response[0]);
        Assert.Equal(request[1], response[1]);
        Assert.Equal(0x03, response[7]);
        Assert.Equal(0x02, response[8]);
    }

    [Fact]
    public async Task ReadBoolArray_ShortBitPayloadReturnsInvalidFrame()
    {
        using FakeModbusServer server = await FakeModbusServer.StartAsync(request =>
        {
            byte[] response = CreateReadCoilsResponse(request);
            response[8] = 0;
            Array.Resize(ref response, 9);
            response[5] = 0x03;
            return response;
        });
        using ModbusTcp modbusTcp = CreateLocalClient(server.Port);

        AssertSuccess(await modbusTcp.ConnectAsync());
        Result<bool[]> result = await modbusTcp.ReadArrayAsync<bool>("coil:0", 8);

        Assert.False(result.IsSuccess);
        Assert.Equal(ModbusTcpErrorCodes.InvalidFrame, result.ErrorCode);
    }

    public void Dispose()
    {
        ModbusTcp.Dispose();
    }

    private void WriteReadAssert<T>(string point, T expected) where T : struct
    {
        AssertSuccess(ModbusTcp.Write(point, expected));
        T actual = AssertSuccess(ModbusTcp.Read<T>(point));
        AssertValue(expected, actual);
    }

    private async Task WriteReadAssertAsync<T>(string point, T expected) where T : struct
    {
        AssertSuccess(await ModbusTcp.WriteAsync(point, expected));
        T actual = AssertSuccess(await ModbusTcp.ReadAsync<T>(point));
        AssertValue(expected, actual);
    }

    private void WriteReadArrayAssert<T>(string point, T[] expected) where T : struct
    {
        AssertSuccess(ModbusTcp.Write(point, expected));
        T[] actual = AssertSuccess(ModbusTcp.ReadArray<T>(point, checked((ushort)expected.Length)));
        AssertArray(expected, actual);
    }

    private async Task WriteReadArrayAssertAsync<T>(string point, T[] expected) where T : struct
    {
        AssertSuccess(await ModbusTcp.WriteAsync(point, expected));
        T[] actual = AssertSuccess(await ModbusTcp.ReadArrayAsync<T>(point, checked((ushort)expected.Length)));
        AssertArray(expected, actual);
    }

    private void WriteReadAssertString(string point, string expected, ushort registerLength)
    {
        AssertSuccess(ModbusTcp.Write(point, expected));
        string actual = AssertSuccess(ModbusTcp.ReadString(point, registerLength));
        Assert.Equal(expected, actual);
    }

    private async Task WriteReadAssertStringAsync(string point, string expected, ushort registerLength)
    {
        AssertSuccess(await ModbusTcp.WriteAsync(point, expected));
        string actual = AssertSuccess(await ModbusTcp.ReadStringAsync(point, registerLength));
        Assert.Equal(expected, actual);
    }

    private static void AssertSuccess(Result result)
    {
        Assert.True(result.IsSuccess, result.Message);
    }

    private static T AssertSuccess<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.Data);
        return result.Data;
    }

    private static void AssertValue<T>(T expected, T actual)
    {
        if (expected is float expectedFloat && actual is float actualFloat)
        {
            Assert.Equal(expectedFloat, actualFloat, 5);
            return;
        }

        if (expected is double expectedDouble && actual is double actualDouble)
        {
            Assert.Equal(expectedDouble, actualDouble, 8);
            return;
        }

        Assert.Equal(expected, actual);
    }

    private static void AssertArray<T>(T[] expected, T[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            AssertValue(expected[i], actual[i]);
        }
    }

    private static ModbusTcp CreateLocalClient(int port)
    {
        return new ModbusTcp("127.0.0.1", port)
        {
            ConnectTimeOut = 1000,
            ReceiveTimeOut = 200,
            LockWaitTimeOut = 1000
        };
    }

    private static byte[] CreateWriteSingleRegisterEcho(byte[] request)
    {
        byte[] response = new byte[12];
        Buffer.BlockCopy(request, 0, response, 0, response.Length);
        return response;
    }

    private static byte[] CreateReadHoldingRegistersResponse(byte[] request)
    {
        return new byte[]
        {
            request[0], request[1], 0x00, 0x00, 0x00, 0x05, request[6],
            request[7], 0x02, 0x12, 0x34
        };
    }

    private static byte[] CreateReadCoilsResponse(byte[] request)
    {
        return new byte[]
        {
            request[0], request[1], 0x00, 0x00, 0x00, 0x04, request[6],
            request[7], 0x01, 0x00
        };
    }

    private sealed class FakeModbusServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<byte[], byte[]?> _handleRequest;
        private readonly CancellationTokenSource _disposeToken = new CancellationTokenSource();
        private readonly Task _serverTask;

        private FakeModbusServer(TcpListener listener, Func<byte[], byte[]?> handleRequest)
        {
            _listener = listener;
            _handleRequest = handleRequest;
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serverTask = RunAsync();
        }

        public int Port { get; }

        public static Task<FakeModbusServer> StartAsync(Func<byte[], byte[]?> handleRequest)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new FakeModbusServer(listener, handleRequest));
        }

        public void Dispose()
        {
            _disposeToken.Cancel();
            _listener.Stop();
            try
            {
                _serverTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
            }

            _disposeToken.Dispose();
        }

        private async Task RunAsync()
        {
            try
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(_disposeToken.Token);
                using NetworkStream stream = client.GetStream();
                byte[] header = new byte[7];
                await ReadExactAsync(stream, header, 0, header.Length, _disposeToken.Token);
                int length = (header[4] << 8) | header[5];
                byte[] request = new byte[6 + length];
                Buffer.BlockCopy(header, 0, request, 0, header.Length);
                await ReadExactAsync(stream, request, 7, length - 1, _disposeToken.Token);

                byte[]? response = _handleRequest(request);
                if (response != null)
                {
                    await stream.WriteAsync(response, _disposeToken.Token);
                    await stream.FlushAsync(_disposeToken.Token);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), _disposeToken.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }

        private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int received = 0;
            while (received < count)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(offset + received, count - received), cancellationToken);
                if (read == 0)
                {
                    throw new IOException("Client disconnected.");
                }

                received += read;
            }
        }
    }
}
