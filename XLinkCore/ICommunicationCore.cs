using System;
using System.Threading.Tasks;

namespace XLinkCore;

public interface ICommunicationCore : IDisposable
{
    Result Connect();
    Result Disconnect();
}

public interface ICommunicationCoreAsync : IDisposable
{
    Task<Result> ConnectAsync();
    Task<Result> DisconnectAsync();
}