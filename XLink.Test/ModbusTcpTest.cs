using XLinkCore;
using XLinkCore.ModbusTcp;

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
        WriteReadStringArrayAssert("hr:290", new[] { "A1", "B2", "C3", "D4" });
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
        await WriteReadStringArrayAssertAsync("hr:290", new[] { "A1", "B2", "C3", "D4" });
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

    private void WriteReadStringArrayAssert(string point, string[] expected)
    {
        AssertSuccess(ModbusTcp.Write(point, expected));
        string[] actual = AssertSuccess(ModbusTcp.ReadStringArray(point, checked((ushort)expected.Length)));
        Assert.Equal(expected, actual);
    }

    private async Task WriteReadStringArrayAssertAsync(string point, string[] expected)
    {
        AssertSuccess(await ModbusTcp.WriteAsync(point, expected));
        string[] actual = AssertSuccess(await ModbusTcp.ReadStringArrayAsync(point, checked((ushort)expected.Length)));
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
}
