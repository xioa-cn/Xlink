namespace XLink.Test;

public class SiemensS7Test
{
    private const string LocalServerIp = "127.0.0.1";
    private const int LocalServerPort = 1020;

    [Fact]
    public void Constructor_SetsDefaultPortAndSlot()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = new XLinkCore.SiemensS7.SiemensS7(LocalServerIp);

        Assert.Equal(102, s7.Port);
        Assert.Equal(1, s7.Slot);
        Assert.False(s7.IsConnected);
    }

    [Fact]
    public void Constructor_AllowsCustomLocalServerPort()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 =
            new XLinkCore.SiemensS7.SiemensS7(LocalServerIp, port: LocalServerPort);

        Assert.Equal(LocalServerPort, s7.Port);
    }

    [Fact]
    public void Connect_UnreachableEndpointReturnsErrorResult()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = new XLinkCore.SiemensS7.SiemensS7("127.0.0.1", port: 1)
        {
            ConnectTimeOut = 100,
            ReceiveTimeOut = 100,
            LockWaitTimeOut = 500
        };

        XLinkCore.Result result = s7.Connect();

        Assert.False(result.IsSuccess);
        Assert.Contains(result.ErrorCode, new[]
        {
            XLinkCore.SiemensS7.SiemensS7ErrorCodes.Timeout,
            XLinkCore.SiemensS7.SiemensS7ErrorCodes.NetworkDisconnected
        });
    }

    [Fact]
    public void Read_InvalidAddressReturnsErrorResult()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = new XLinkCore.SiemensS7.SiemensS7(LocalServerIp);

        XLinkCore.Result<int> result = s7.Read<int>("bad:0");

        Assert.False(result.IsSuccess);
        Assert.Equal(XLinkCore.SiemensS7.SiemensS7ErrorCodes.Unknown, result.ErrorCode);
        Assert.Contains("Unsupported S7 address", result.Message);
    }

    [Fact]
    public void Read_DbAddressFormatsAreRecognized()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 =
            CreateLocalServerClient();

        AssertDisconnected(s7.Read<bool>("DB1.DBX0.0"));
        AssertDisconnected(s7.Read<bool>("DB1.0.1"));
        AssertDisconnected(s7.Read<byte>("DB1.0"));
        AssertDisconnected(s7.Read<byte>("DB1.DBB20"));
        AssertDisconnected(s7.Read<short>("DB1.DBW0"));
        AssertDisconnected(s7.Read<int>("DB1.DBD4"));
    }

    [Fact]
    public void ReadBytes_DbShortAddressFormatIsRecognized()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertDisconnected(s7.ReadBytes("DB1.0", 4));
    }

    [Fact]
    public async Task ReadBytesAsync_DbShortAddressFormatIsRecognized()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertDisconnected(await s7.ReadBytesAsync("DB1.0", 4));
    }

    [Fact]
    public void WriteBytes_DbShortAddressFormatIsRecognized()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        XLinkCore.Result result = s7.WriteBytes("DB1.0", new byte[] { 1, 2, 3, 4 });

        Assert.False(result.IsSuccess);
        Assert.Equal(XLinkCore.SiemensS7.SiemensS7ErrorCodes.NotConnected, result.ErrorCode);
        Assert.Contains("Socket is not connected", result.Message);
    }

    [Fact]
    public async Task WriteBytesAsync_DbShortAddressFormatIsRecognized()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        XLinkCore.Result result = await s7.WriteBytesAsync("DB1.0", new byte[] { 1, 2, 3, 4 });

        Assert.False(result.IsSuccess);
        Assert.Equal(XLinkCore.SiemensS7.SiemensS7ErrorCodes.NotConnected, result.ErrorCode);
        Assert.Contains("Socket is not connected", result.Message);
    }

    [Fact]
    public void WriteSiemensString_RejectsValueLongerThanMaxLength()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        XLinkCore.Result result = s7.WriteSiemensString("DB1.200", "ABCDE", 4);

        Assert.False(result.IsSuccess);
        Assert.Equal(-1, result.ErrorCode);
        Assert.Contains("exceeds max length", result.Message);
    }

    [Fact]
    public async Task WriteSiemensStringAsync_RejectsValueLongerThanMaxLength()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        XLinkCore.Result result = await s7.WriteSiemensStringAsync("DB1.200", "ABCDE", 4);

        Assert.False(result.IsSuccess);
        Assert.Equal(XLinkCore.SiemensS7.SiemensS7ErrorCodes.Unknown, result.ErrorCode);
        Assert.Contains("exceeds max length", result.Message);
    }

    [Fact]
    public void ReadSiemensString_DbShortAddressFormatIsRecognized()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertDisconnected(s7.ReadSiemensString("DB1.200", 20));
    }

    [Fact]
    public async Task ReadSiemensStringAsync_DbShortAddressFormatIsRecognized()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertDisconnected(await s7.ReadSiemensStringAsync("DB1.200", 20));
    }

    [Theory]
    [InlineData("I0.0")]
    [InlineData("IB0")]
    [InlineData("IW0")]
    [InlineData("ID0")]
    [InlineData("Q0.0")]
    [InlineData("QB0")]
    [InlineData("QW0")]
    [InlineData("QD0")]
    [InlineData("M0.0")]
    [InlineData("MB0")]
    [InlineData("MW0")]
    [InlineData("MD0")]
    [InlineData("V0.0")]
    [InlineData("VB0")]
    [InlineData("VW0")]
    [InlineData("VD0")]
    [InlineData("SM0.0")]
    [InlineData("SMB0")]
    [InlineData("SMW0")]
    [InlineData("SMD0")]
    [InlineData("P0.0")]
    [InlineData("PB0")]
    [InlineData("PW0")]
    [InlineData("PD0")]
    [InlineData("T0")]
    [InlineData("C0")]
    [InlineData("AI0")]
    [InlineData("AIW0")]
    [InlineData("AQ0")]
    [InlineData("AQW0")]
    public void Read_RegisterAreaAddressFormatsAreRecognized(string point)
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertDisconnected(s7.Read<ushort>(point));
    }

    [Fact]
    public void LocalServer_WriteThenReadDbValues_ReturnsWrittenValues()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertSuccess(s7.Connect());

        WriteReadAssert(s7, "DB1.DBW0", (short)12345);
        WriteReadAssert(s7, "DB1.DBD4", 123456789);
        WriteReadAssert(s7, "DB1.DBD8", 12.345f);
        WriteReadAssert(s7, "DB1.DBX20.0", true);
        WriteReadAssert(s7, "DB1.DBX20.0", false);
    }

    [Fact]
    public async Task LocalServer_WriteThenReadDbValuesAsync_ReturnsWrittenValues()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertSuccess(await s7.ConnectAsync());

        await WriteReadAssertAsync(s7, "DB1.DBW0", (short)-2345);
        await WriteReadAssertAsync(s7, "DB1.DBD4", -123456789);
        await WriteReadAssertAsync(s7, "DB1.DBD8", -45.625f);
        await WriteReadAssertAsync(s7, "DB1.DBX20.0", true);
        await WriteReadAssertAsync(s7, "DB1.DBX20.0", false);
    }

    [Fact]
    public void LocalServer_WriteThenReadDbArray_ReturnsWrittenValues()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertSuccess(s7.Connect());

        WriteReadArrayAssert(s7, "DB1.DBW30", new short[] { 101, -202, 303, -404 });
        WriteReadArrayAssert(s7, "DB1.DBD50", new[] { 1000001, -1000002, 1000003 });
        WriteReadArrayAssert(s7, "DB1.DBD70", new[] { 1.25f, -2.5f, 3.75f });
    }

    [Fact]
    public async Task LocalServer_WriteThenReadDbArrayAsync_ReturnsWrittenValues()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertSuccess(await s7.ConnectAsync());

        await WriteReadArrayAssertAsync(s7, "DB1.DBW90", new short[] { -11, 22, -33, 44 });
        await WriteReadArrayAssertAsync(s7, "DB1.DBD110", new[] { -2000001, 2000002, -2000003 });
        await WriteReadArrayAssertAsync(s7, "DB1.DBD130", new[] { -1.125f, 2.25f, -3.5f });
    }

    [Fact]
    public void LocalServer_WriteThenReadDbBytes_ReturnsWrittenValues()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertSuccess(s7.Connect());

        byte[] expected = { 0x10, 0x20, 0x30, 0x40, 0x50 };
        AssertSuccess(s7.WriteBytes("DB1.160", expected));
        byte[] actual = AssertSuccess(s7.ReadBytes("DB1.160", checked((ushort)expected.Length)));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task LocalServer_WriteThenReadDbBytesAsync_ReturnsWrittenValues()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertSuccess(await s7.ConnectAsync());

        byte[] expected = { 0xAA, 0xBB, 0xCC, 0xDD };
        AssertSuccess(await s7.WriteBytesAsync("DB1.170", expected));
        byte[] actual = AssertSuccess(await s7.ReadBytesAsync("DB1.170", checked((ushort)expected.Length)));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LocalServer_WriteThenReadSiemensString_ReturnsWrittenValue()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertSuccess(s7.Connect());

        AssertSuccess(s7.WriteSiemensString("DB1.200", "HELLO-S7", 20));
        string actual = AssertSuccess(s7.ReadSiemensString("DB1.200", 20));

        Assert.Equal("HELLO-S7", actual);
    }

    [Fact]
    public async Task LocalServer_WriteThenReadSiemensStringAsync_ReturnsWrittenValue()
    {
        using XLinkCore.SiemensS7.SiemensS7 s7 = CreateLocalServerClient();

        AssertSuccess(await s7.ConnectAsync());

        AssertSuccess(await s7.WriteSiemensStringAsync("DB1.230", "ASYNC-S7", 20));
        string actual = AssertSuccess(await s7.ReadSiemensStringAsync("DB1.230", 20));

        Assert.Equal("ASYNC-S7", actual);
    }

    private static XLinkCore.SiemensS7.SiemensS7 CreateLocalServerClient()
    {
        return new XLinkCore.SiemensS7.SiemensS7(LocalServerIp, port: LocalServerPort)
        {
            ConnectTimeOut = 1000,
            ReceiveTimeOut = 1000,
            LockWaitTimeOut = 3000
        };
    }

    private static void WriteReadAssert<T>(XLinkCore.SiemensS7.SiemensS7 s7, string point, T expected)
        where T : struct
    {
        AssertSuccess(s7.Write(point, expected));
        T actual = AssertSuccess(s7.Read<T>(point));
        AssertValue(expected, actual);
    }

    private static async Task WriteReadAssertAsync<T>(XLinkCore.SiemensS7.SiemensS7 s7, string point, T expected)
        where T : struct
    {
        AssertSuccess(await s7.WriteAsync(point, expected));
        T actual = AssertSuccess(await s7.ReadAsync<T>(point));
        AssertValue(expected, actual);
    }

    private static void WriteReadArrayAssert<T>(XLinkCore.SiemensS7.SiemensS7 s7, string point, T[] expected)
        where T : struct
    {
        AssertSuccess(s7.Write(point, expected));
        T[] actual = AssertSuccess(s7.ReadArray<T>(point, checked((ushort)expected.Length)));
        AssertArray(expected, actual);
    }

    private static async Task WriteReadArrayAssertAsync<T>(XLinkCore.SiemensS7.SiemensS7 s7, string point, T[] expected)
        where T : struct
    {
        AssertSuccess(await s7.WriteAsync(point, expected));
        T[] actual = AssertSuccess(await s7.ReadArrayAsync<T>(point, checked((ushort)expected.Length)));
        AssertArray(expected, actual);
    }

    private static void AssertSuccess(XLinkCore.Result result)
    {
        Assert.True(result.IsSuccess, result.Message);
    }

    private static T AssertSuccess<T>(XLinkCore.Result<T> result)
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

    private static void AssertDisconnected<T>(XLinkCore.Result<T> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(XLinkCore.SiemensS7.SiemensS7ErrorCodes.NotConnected, result.ErrorCode);
        Assert.Contains("Socket is not connected", result.Message);
    }
}
