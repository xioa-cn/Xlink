using System.Threading.Tasks;

namespace XLinkCore;

public interface IReadWriteCore
{
    Result<T> Read<T>(string point) where T : struct;
    Result<T[]> ReadArray<T>(string point, ushort length) where T : struct;
    Result<string> ReadString(string point, ushort length);
    Result<string[]> ReadStringArray(string point, ushort length);
    Result Write<T>(string point, T value);
}

public interface IReadWriteCoreAsync
{
    Task<Result<T>> ReadAsync<T>(string point) where T : struct;
    Task<Result<T[]>> ReadArrayAsync<T>(string point, ushort length) where T : struct;
    Task<Result<string>> ReadStringAsync(string point, ushort length);
    Task<Result<string[]>> ReadStringArrayAsync(string point, ushort length);
    Task<Result> WriteAsync<T>(string point, T value);
}
