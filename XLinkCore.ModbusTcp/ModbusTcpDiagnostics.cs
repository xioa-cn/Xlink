using System;
using System.Net.Sockets;

namespace XLinkCore.ModbusTcp
{
    public static class ModbusTcpErrorCodes
    {
        public const int Unknown = -1;
        public const int Timeout = -1001;
        public const int NetworkDisconnected = -1002;
        public const int CommunicationLockTimeout = -1003;
        public const int NotConnected = -1004;
        public const int ObjectDisposed = -1005;
        public const int InvalidFrame = -2001;
        public const int ModbusExceptionBase = -3000;

        public static int FromException(Exception exception)
        {
            if (exception is ModbusTcpException modbusTcpException)
            {
                return modbusTcpException.ErrorCode;
            }

            if (exception is ObjectDisposedException)
            {
                return ObjectDisposed;
            }

            if (exception is TimeoutException)
            {
                return Timeout;
            }

            if (exception is SocketException socketException)
            {
                return FromSocketError(socketException.SocketErrorCode);
            }

            return Unknown;
        }

        public static int FromModbusExceptionCode(int exceptionCode)
        {
            return ModbusExceptionBase - exceptionCode;
        }

        public static int FromSocketError(SocketError socketError)
        {
            switch (socketError)
            {
                case SocketError.TimedOut:
                case SocketError.WouldBlock:
                case SocketError.TryAgain:
                    return Timeout;
                case SocketError.ConnectionAborted:
                case SocketError.ConnectionReset:
                case SocketError.Disconnecting:
                case SocketError.HostDown:
                case SocketError.HostNotFound:
                case SocketError.HostUnreachable:
                case SocketError.NetworkDown:
                case SocketError.NetworkReset:
                case SocketError.NetworkUnreachable:
                case SocketError.NotConnected:
                case SocketError.Shutdown:
                    return NetworkDisconnected;
                default:
                    return Unknown;
            }
        }
    }

    public class ModbusTcpException : Exception
    {
        public ModbusTcpException(string message, int errorCode)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public ModbusTcpException(string message, int errorCode, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        public int ErrorCode { get; }
    }

    public sealed class ModbusTcpTimeoutException : ModbusTcpException
    {
        public ModbusTcpTimeoutException(string message)
            : base(message, ModbusTcpErrorCodes.Timeout)
        {
        }

        public ModbusTcpTimeoutException(string message, Exception innerException)
            : base(message, ModbusTcpErrorCodes.Timeout, innerException)
        {
        }
    }

    public sealed class ModbusTcpDisconnectedException : ModbusTcpException
    {
        public ModbusTcpDisconnectedException(string message)
            : base(message, ModbusTcpErrorCodes.NetworkDisconnected)
        {
        }

        public ModbusTcpDisconnectedException(string message, Exception innerException)
            : base(message, ModbusTcpErrorCodes.NetworkDisconnected, innerException)
        {
        }
    }

    public sealed class ModbusTcpProtocolException : ModbusTcpException
    {
        public ModbusTcpProtocolException(string message)
            : base(message, ModbusTcpErrorCodes.InvalidFrame)
        {
        }

        public ModbusTcpProtocolException(string message, int modbusExceptionCode)
            : base(message, ModbusTcpErrorCodes.FromModbusExceptionCode(modbusExceptionCode))
        {
            ModbusExceptionCode = modbusExceptionCode;
        }

        public int? ModbusExceptionCode { get; }
    }

    public sealed class ModbusTcpStatistics
    {
        internal ModbusTcpStatistics(
            long bytesSent,
            long bytesReceived,
            long requestCount,
            long failedRequestCount,
            TimeSpan totalElapsed,
            TimeSpan lastElapsed,
            TimeSpan maxElapsed)
        {
            BytesSent = bytesSent;
            BytesReceived = bytesReceived;
            RequestCount = requestCount;
            FailedRequestCount = failedRequestCount;
            TotalElapsed = totalElapsed;
            LastElapsed = lastElapsed;
            MaxElapsed = maxElapsed;
        }

        public long BytesSent { get; }
        public long BytesReceived { get; }
        public long RequestCount { get; }
        public long FailedRequestCount { get; }
        public TimeSpan TotalElapsed { get; }
        public TimeSpan LastElapsed { get; }
        public TimeSpan MaxElapsed { get; }

        public TimeSpan AverageElapsed
        {
            get
            {
                if (RequestCount == 0)
                {
                    return TimeSpan.Zero;
                }

                return TimeSpan.FromTicks(TotalElapsed.Ticks / RequestCount);
            }
        }
    }

    public sealed class ModbusWriteResponse
    {
        internal ModbusWriteResponse(byte functionCode, ushort address, ushort quantity, byte[] valueBytes)
        {
            FunctionCode = functionCode;
            Address = address;
            Quantity = quantity;
            ValueBytes = valueBytes;
        }

        public byte FunctionCode { get; }
        public ushort Address { get; }
        public ushort Quantity { get; }
        public byte[] ValueBytes { get; }
    }
}
