using System;
using System.Net.Sockets;

namespace XLinkCore.SiemensS7
{
    public static class SiemensS7ErrorCodes
    {
        public const int Unknown = -1;
        public const int Timeout = -1001;
        public const int NetworkDisconnected = -1002;
        public const int CommunicationLockTimeout = -1003;
        public const int NotConnected = -1004;
        public const int ObjectDisposed = -1005;
        public const int InvalidFrame = -2001;
        public const int ProtocolError = -2002;
        public const int S7ReturnCodeBase = -3000;

        public static int FromException(Exception exception)
        {
            if (exception is SiemensS7Exception s7Exception) return s7Exception.ErrorCode;
            if (exception is ObjectDisposedException) return ObjectDisposed;
            if (exception is TimeoutException) return Timeout;
            if (exception is SocketException socketException) return FromSocketError(socketException.SocketErrorCode);
            return Unknown;
        }

        public static int FromS7ReturnCode(byte returnCode)
        {
            return S7ReturnCodeBase - returnCode;
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
                case SocketError.ConnectionRefused:
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

    public class SiemensS7Exception : Exception
    {
        public SiemensS7Exception(string message, int errorCode)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public SiemensS7Exception(string message, int errorCode, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        public int ErrorCode { get; }
    }

    public sealed class SiemensS7TimeoutException : SiemensS7Exception
    {
        public SiemensS7TimeoutException(string message)
            : base(message, SiemensS7ErrorCodes.Timeout)
        {
        }

        public SiemensS7TimeoutException(string message, Exception innerException)
            : base(message, SiemensS7ErrorCodes.Timeout, innerException)
        {
        }
    }

    public sealed class SiemensS7DisconnectedException : SiemensS7Exception
    {
        public SiemensS7DisconnectedException(string message)
            : base(message, SiemensS7ErrorCodes.NetworkDisconnected)
        {
        }

        public SiemensS7DisconnectedException(string message, Exception innerException)
            : base(message, SiemensS7ErrorCodes.NetworkDisconnected, innerException)
        {
        }
    }

    public sealed class SiemensS7ProtocolException : SiemensS7Exception
    {
        public SiemensS7ProtocolException(string message)
            : base(message, SiemensS7ErrorCodes.InvalidFrame)
        {
        }

        public SiemensS7ProtocolException(string message, byte returnCode)
            : base(message, SiemensS7ErrorCodes.FromS7ReturnCode(returnCode))
        {
            ReturnCode = returnCode;
        }

        public byte? ReturnCode { get; }
    }
}
