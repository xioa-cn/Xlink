using System.Net.Sockets;
using System.Threading.Tasks;

namespace XLinkCore;

public abstract class DeviceTcpCore : IDeviceCoreNet
{
    public abstract bool IsConnected { get; }

    public virtual int ReceiveTimeOut { get; set; }
    public virtual int ConnectTimeOut { get; set; }
    public virtual string IpAddress { get; set; } = string.Empty;
    public virtual int Port { get; set; }

    protected virtual Socket? Socket { get; }

    public abstract Result Connect();

    public abstract Result Disconnect();

    public abstract Result<T> Read<T>(string point) where T : struct;

    public abstract Result<T[]> ReadArray<T>(string point, ushort length) where T : struct;

    public abstract Result<string> ReadString(string point, ushort length);

    public abstract Result Write<T>(string point, T value);

    public abstract Task<Result> ConnectAsync();

    public abstract Task<Result> DisconnectAsync();

    public abstract Task<Result<T>> ReadAsync<T>(string point) where T : struct;

    public abstract Task<Result<T[]>> ReadArrayAsync<T>(string point, ushort length) where T : struct;

    public abstract Task<Result<string>> ReadStringAsync(string point, ushort length);

    public abstract Task<Result> WriteAsync<T>(string point, T value);

    public abstract void Dispose();
}
