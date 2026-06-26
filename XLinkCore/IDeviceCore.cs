namespace XLinkCore;

public interface IDeviceCore : ICommunicationCore, IReadWriteCore
{
}

public interface IDeviceCoreAsync : ICommunicationCoreAsync, IReadWriteCoreAsync
{
}

public interface IDeviceCoreNet : IDeviceCore, IDeviceCoreAsync
{
}