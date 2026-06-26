using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace XLinkCore.ModbusTcp
{
    public class ModbusTcpNet : ICommunicationCore, ICommunicationCoreAsync
    {
        private readonly SemaphoreSlim _communicationLock = new SemaphoreSlim(1, 1);
        private readonly object _stateLock = new object();
        private readonly object _statisticsLock = new object();
        private const int DefaultLockWaitTimeOut = 5000;
        private const uint KeepAliveTime = 60000;
        private const uint KeepAliveInterval = 5000;
        private ushort _transactionId;
        private string _ipAddress;
        private int _port;
        private int _receiveTimeOut;
        private int _connectTimeOut;
        private int _lockWaitTimeOut = DefaultLockWaitTimeOut;
        private long _bytesSent;
        private long _bytesReceived;
        private long _requestCount;
        private long _failedRequestCount;
        private long _totalElapsedTicks;
        private long _lastElapsedTicks;
        private long _maxElapsedTicks;
        private bool _disposed;
        private ModbusWriteResponse _lastWriteResponse;

        public string IpAddress
        {
            get
            {
                lock (_stateLock)
                {
                    return _ipAddress;
                }
            }
            set
            {
                lock (_stateLock)
                {
                    ThrowIfConnectedLocked("IpAddress");
                    _ipAddress = value;
                }
            }
        }

        public int Port
        {
            get
            {
                lock (_stateLock)
                {
                    return _port;
                }
            }
            set
            {
                lock (_stateLock)
                {
                    ThrowIfConnectedLocked("Port");
                    _port = value;
                }
            }
        }

        public int ReceiveTimeOut
        {
            get
            {
                lock (_stateLock)
                {
                    return _receiveTimeOut;
                }
            }
            set
            {
                lock (_stateLock)
                {
                    ValidateTimeoutConfiguration(value, _lockWaitTimeOut);
                    _receiveTimeOut = value;
                    if (Socket != null)
                    {
                        Socket.ReceiveTimeout = value;
                        Socket.SendTimeout = value;
                    }
                }
            }
        }

        public int ConnectTimeOut
        {
            get
            {
                lock (_stateLock)
                {
                    return _connectTimeOut;
                }
            }
            set
            {
                lock (_stateLock)
                {
                    _connectTimeOut = value;
                }
            }
        }

        public int LockWaitTimeOut
        {
            get
            {
                lock (_stateLock)
                {
                    return _lockWaitTimeOut;
                }
            }
            set
            {
                lock (_stateLock)
                {
                    ValidateTimeoutConfiguration(_receiveTimeOut, value);
                    _lockWaitTimeOut = value;
                }
            }
        }

        public bool IsConnected
        {
            get
            {
                lock (_stateLock)
                {
                    return Socket != null && Socket.Connected;
                }
            }
        }

        public bool IsBusy
        {
            get { return _communicationLock.CurrentCount == 0; }
        }

        public ModbusWriteResponse LastWriteResponse
        {
            get
            {
                lock (_stateLock)
                {
                    return _lastWriteResponse;
                }
            }
        }

        public Socket Socket { get; private set; }

        public ModbusTcpStatistics GetStatistics()
        {
            lock (_statisticsLock)
            {
                return new ModbusTcpStatistics(
                    _bytesSent,
                    _bytesReceived,
                    _requestCount,
                    _failedRequestCount,
                    TimeSpan.FromTicks(_totalElapsedTicks),
                    TimeSpan.FromTicks(_lastElapsedTicks),
                    TimeSpan.FromTicks(_maxElapsedTicks));
            }
        }

        public void ResetStatistics()
        {
            lock (_statisticsLock)
            {
                _bytesSent = 0;
                _bytesReceived = 0;
                _requestCount = 0;
                _failedRequestCount = 0;
                _totalElapsedTicks = 0;
                _lastElapsedTicks = 0;
                _maxElapsedTicks = 0;
            }
        }

        public void Dispose()
        {
            bool shouldDispose;
            lock (_stateLock)
            {
                shouldDispose = !_disposed;
                _disposed = true;
            }

            if (!shouldDispose)
            {
                return;
            }

            bool entered = false;
            try
            {
                entered = TryEnterCommunicationLock(true);
                DisconnectCore();
            }
            catch
            {
            }
            finally
            {
                if (entered)
                {
                    _communicationLock.Release();
                }

                _communicationLock.Dispose();
            }
        }

        public Result Connect()
        {
            if (!TryEnterCommunicationLock())
            {
                return Error("Communication lock wait timeout.", ModbusTcpErrorCodes.CommunicationLockTimeout);
            }

            try
            {
                ThrowIfDisposed();
                DisconnectCore();

                string ipAddress;
                int port;
                int connectTimeOut;
                lock (_stateLock)
                {
                    ipAddress = _ipAddress;
                    port = _port;
                    connectTimeOut = _connectTimeOut;
                }

                Socket = CreateSocket();
                if (connectTimeOut > 0)
                {
                    IAsyncResult result = Socket.BeginConnect(ipAddress, port, null, null);
                    try
                    {
                        if (!result.AsyncWaitHandle.WaitOne(connectTimeOut))
                        {
                            Socket timedOutSocket = Socket;
                            DisconnectCore();
                            CompleteTimedOutConnect(timedOutSocket, result);
                            return Error("Connect timeout.", ModbusTcpErrorCodes.Timeout);
                        }

                        Socket.EndConnect(result);
                    }
                    finally
                    {
                        result.AsyncWaitHandle.Dispose();
                    }
                }
                else
                {
                    Socket.Connect(ipAddress, port);
                }

                return Success();
            }
            catch (Exception ex)
            {
                DisconnectCore();
                return Error(ex);
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        public Result Disconnect()
        {
            if (!TryEnterCommunicationLock())
            {
                return Error("Communication lock wait timeout.", ModbusTcpErrorCodes.CommunicationLockTimeout);
            }

            try
            {
                DisconnectCore();
                return Success();
            }
            catch (Exception ex)
            {
                Socket = null;
                return Error(ex);
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        public async Task<Result> ConnectAsync()
        {
            if (!await TryEnterCommunicationLockAsync().ConfigureAwait(false))
            {
                return Error("Communication lock wait timeout.", ModbusTcpErrorCodes.CommunicationLockTimeout);
            }

            try
            {
                ThrowIfDisposed();
                DisconnectCore();

                string ipAddress;
                int port;
                int connectTimeOut;
                lock (_stateLock)
                {
                    ipAddress = _ipAddress;
                    port = _port;
                    connectTimeOut = _connectTimeOut;
                }

                Socket = CreateSocket();
                Task connectTask = Task.Factory.FromAsync(
                    (callback, state) => Socket.BeginConnect(ipAddress, port, callback, state),
                    Socket.EndConnect,
                    null);

                if (connectTimeOut > 0)
                {
                    Task timeoutTask = Task.Delay(connectTimeOut);
                    Task completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
                    if (completedTask != connectTask)
                    {
                        DisconnectCore();
                        return Error("Connect timeout.", ModbusTcpErrorCodes.Timeout);
                    }
                }

                await connectTask.ConfigureAwait(false);
                return Success();
            }
            catch (Exception ex)
            {
                DisconnectCore();
                return Error(ex);
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        public async Task<Result> DisconnectAsync()
        {
            if (!await TryEnterCommunicationLockAsync().ConfigureAwait(false))
            {
                return Error("Communication lock wait timeout.", ModbusTcpErrorCodes.CommunicationLockTimeout);
            }

            try
            {
                DisconnectCore();
                return Success();
            }
            catch (Exception ex)
            {
                Socket = null;
                return Error(ex);
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        public byte[] ReadCoils(byte station, ushort address, ushort quantity)
        {
            return ReadBits(station, 1, address, quantity);
        }

        public Task<byte[]> ReadCoilsAsync(byte station, ushort address, ushort quantity)
        {
            return ReadBitsAsync(station, 1, address, quantity);
        }

        public byte[] ReadDiscreteInputs(byte station, ushort address, ushort quantity)
        {
            return ReadBits(station, 2, address, quantity);
        }

        public Task<byte[]> ReadDiscreteInputsAsync(byte station, ushort address, ushort quantity)
        {
            return ReadBitsAsync(station, 2, address, quantity);
        }

        public byte[] ReadHoldingRegisters(byte station, ushort address, ushort quantity)
        {
            return ReadRegisters(station, 3, address, quantity);
        }

        public Task<byte[]> ReadHoldingRegistersAsync(byte station, ushort address, ushort quantity)
        {
            return ReadRegistersAsync(station, 3, address, quantity);
        }

        public byte[] ReadInputRegisters(byte station, ushort address, ushort quantity)
        {
            return ReadRegisters(station, 4, address, quantity);
        }

        public Task<byte[]> ReadInputRegistersAsync(byte station, ushort address, ushort quantity)
        {
            return ReadRegistersAsync(station, 4, address, quantity);
        }

        public ModbusWriteResponse WriteSingleCoil(byte station, ushort address, bool value)
        {
            byte[] pdu = new byte[]
            {
                5,
                HighByte(address),
                LowByte(address),
                value ? (byte)0xFF : (byte)0x00,
                0x00
            };

            byte[] response = Execute(station, pdu);
            return SetLastWriteResponse(ParseWriteSingleResponse(pdu, response));
        }

        public Task<ModbusWriteResponse> WriteSingleCoilAsync(byte station, ushort address, bool value)
        {
            byte[] pdu = new byte[]
            {
                5,
                HighByte(address),
                LowByte(address),
                value ? (byte)0xFF : (byte)0x00,
                0x00
            };

            return WriteSingleAsync(station, pdu);
        }

        public ModbusWriteResponse WriteMultipleCoils(byte station, ushort address, bool[] values)
        {
            byte[] pdu = CreateWriteMultipleCoilsPdu(address, values);
            byte[] response = Execute(station, pdu);
            return SetLastWriteResponse(ParseWriteMultipleResponse(pdu, response));
        }

        public Task<ModbusWriteResponse> WriteMultipleCoilsAsync(byte station, ushort address, bool[] values)
        {
            return WriteMultipleAsync(station, CreateWriteMultipleCoilsPdu(address, values));
        }

        public ModbusWriteResponse WriteSingleRegister(byte station, ushort address, byte[] registerBytes)
        {
            if (registerBytes == null || registerBytes.Length != 2)
            {
                throw new ArgumentException("Single register write requires exactly 2 bytes.", "registerBytes");
            }

            byte[] pdu = new byte[]
            {
                6,
                HighByte(address),
                LowByte(address),
                registerBytes[0],
                registerBytes[1]
            };

            byte[] response = Execute(station, pdu);
            return SetLastWriteResponse(ParseWriteSingleResponse(pdu, response));
        }

        public Task<ModbusWriteResponse> WriteSingleRegisterAsync(byte station, ushort address, byte[] registerBytes)
        {
            if (registerBytes == null || registerBytes.Length != 2)
            {
                throw new ArgumentException("Single register write requires exactly 2 bytes.", "registerBytes");
            }

            byte[] pdu = new byte[]
            {
                6,
                HighByte(address),
                LowByte(address),
                registerBytes[0],
                registerBytes[1]
            };

            return WriteSingleAsync(station, pdu);
        }

        public ModbusWriteResponse WriteMultipleRegisters(byte station, ushort address, byte[] registerBytes)
        {
            byte[] pdu = CreateWriteMultipleRegistersPdu(address, registerBytes);
            byte[] response = Execute(station, pdu);
            return SetLastWriteResponse(ParseWriteMultipleResponse(pdu, response));
        }

        public Task<ModbusWriteResponse> WriteMultipleRegistersAsync(byte station, ushort address, byte[] registerBytes)
        {
            return WriteMultipleAsync(station, CreateWriteMultipleRegistersPdu(address, registerBytes));
        }

        internal byte[] OriginalBytes(byte[] request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (request.Length < 8)
            {
                throw new ArgumentException("Modbus TCP request must contain MBAP header and PDU.", "request");
            }

            int requestLength = ToUInt16(request[4], request[5]);
            if (requestLength <= 1 || request.Length != requestLength + 6)
            {
                throw new ArgumentException("Invalid Modbus TCP request length.", "request");
            }

            if (!TryEnterCommunicationLock())
            {
                throw new ModbusTcpException("Communication lock wait timeout.", ModbusTcpErrorCodes.CommunicationLockTimeout);
            }

            try
            {
                return ReadOriginalBytesCore(request);
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        private byte[] ReadBits(byte station, byte functionCode, ushort address, ushort quantity)
        {
            byte[] response = Execute(station, CreateReadPdu(functionCode, address, quantity));
            return ExtractReadData(response);
        }

        private async Task<byte[]> ReadBitsAsync(byte station, byte functionCode, ushort address, ushort quantity)
        {
            byte[] response = await ExecuteAsync(station, CreateReadPdu(functionCode, address, quantity)).ConfigureAwait(false);
            return ExtractReadData(response);
        }

        private byte[] ReadRegisters(byte station, byte functionCode, ushort address, ushort quantity)
        {
            byte[] response = Execute(station, CreateReadPdu(functionCode, address, quantity));
            return ExtractReadData(response);
        }

        private async Task<byte[]> ReadRegistersAsync(byte station, byte functionCode, ushort address, ushort quantity)
        {
            byte[] response = await ExecuteAsync(station, CreateReadPdu(functionCode, address, quantity)).ConfigureAwait(false);
            return ExtractReadData(response);
        }

        private byte[] Execute(byte station, byte[] pdu)
        {
            if (!TryEnterCommunicationLock())
            {
                throw new ModbusTcpException("Communication lock wait timeout.", ModbusTcpErrorCodes.CommunicationLockTimeout);
            }

            try
            {
                return ExecuteCore(station, pdu);
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        private async Task<byte[]> ExecuteAsync(byte station, byte[] pdu)
        {
            if (!await TryEnterCommunicationLockAsync().ConfigureAwait(false))
            {
                throw new ModbusTcpException("Communication lock wait timeout.", ModbusTcpErrorCodes.CommunicationLockTimeout);
            }

            try
            {
                return await ExecuteCoreAsync(station, pdu).ConfigureAwait(false);
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        private async Task<ModbusWriteResponse> WriteSingleAsync(byte station, byte[] pdu)
        {
            byte[] response = await ExecuteAsync(station, pdu).ConfigureAwait(false);
            return SetLastWriteResponse(ParseWriteSingleResponse(pdu, response));
        }

        private async Task<ModbusWriteResponse> WriteMultipleAsync(byte station, byte[] pdu)
        {
            byte[] response = await ExecuteAsync(station, pdu).ConfigureAwait(false);
            return SetLastWriteResponse(ParseWriteMultipleResponse(pdu, response));
        }

        private static byte[] ExtractReadData(byte[] response)
        {
            byte byteCount = response[1];
            return CopyExact(response, 2, byteCount);
        }

        private byte[] ExecuteCore(byte station, byte[] pdu)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool success = false;
            try
            {
                EnsureConnected();

                ushort transactionId = unchecked(++_transactionId);
                byte[] request = CreateRequest(station, transactionId, pdu);

                SendAll(request);
                byte[] header = RentAndReceiveExact(7);
                try
                {
                    int bodyLength = ValidateHeader(station, transactionId, header) - 1;
                    byte[] rentedBody = RentAndReceiveExact(bodyLength);
                    try
                    {
                        ValidateBody(pdu, rentedBody, bodyLength);
                        byte[] body = CopyExact(rentedBody, 0, bodyLength);
                        success = true;
                        return body;
                    }
                    finally
                    {
                        ReturnArray(rentedBody);
                    }
                }
                finally
                {
                    ReturnArray(header);
                }
            }
            catch (SocketException ex)
            {
                throw ConvertSocketException(ex);
            }
            finally
            {
                RecordRequest(stopwatch.ElapsedTicks, success);
            }
        }

        private byte[] ReadOriginalBytesCore(byte[] request)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool success = false;
            try
            {
                EnsureConnected();

                SendAll(request);

                byte[] response = new byte[7];
                ReceiveExact(response, 0, 7);
                ValidateOriginalHeader(request, response);

                int responseLength = ToUInt16(response[4], response[5]);
                int responseFrameLength = 6 + responseLength;
                if (response.Length != responseFrameLength)
                {
                    Array.Resize(ref response, responseFrameLength);
                }

                int bodyLength = responseLength - 1;
                if (bodyLength > 0)
                {
                    ReceiveExact(response, 7, bodyLength);
                }

                success = true;
                return response;
            }
            catch (SocketException ex)
            {
                throw ConvertSocketException(ex);
            }
            finally
            {
                RecordRequest(stopwatch.ElapsedTicks, success);
            }
        }

        private async Task<byte[]> ExecuteCoreAsync(byte station, byte[] pdu)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool success = false;
            try
            {
                EnsureConnected();

                ushort transactionId = unchecked(++_transactionId);
                byte[] request = CreateRequest(station, transactionId, pdu);

                await SendAllAsync(request).ConfigureAwait(false);
                byte[] header = await RentAndReceiveExactAsync(7).ConfigureAwait(false);
                try
                {
                    int bodyLength = ValidateHeader(station, transactionId, header) - 1;
                    byte[] rentedBody = await RentAndReceiveExactAsync(bodyLength).ConfigureAwait(false);
                    try
                    {
                        ValidateBody(pdu, rentedBody, bodyLength);
                        byte[] body = CopyExact(rentedBody, 0, bodyLength);
                        success = true;
                        return body;
                    }
                    finally
                    {
                        ReturnArray(rentedBody);
                    }
                }
                finally
                {
                    ReturnArray(header);
                }
            }
            catch (SocketException ex)
            {
                throw ConvertSocketException(ex);
            }
            finally
            {
                RecordRequest(stopwatch.ElapsedTicks, success);
            }
        }

        private Socket CreateSocket()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EnableKeepAlive(socket);
            if (ReceiveTimeOut > 0)
            {
                socket.ReceiveTimeout = ReceiveTimeOut;
                socket.SendTimeout = ReceiveTimeOut;
            }

            return socket;
        }

        private static void EnableKeepAlive(Socket socket)
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            byte[] inOptionValues = new byte[12];
            BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);
            BitConverter.GetBytes(KeepAliveTime).CopyTo(inOptionValues, 4);
            BitConverter.GetBytes(KeepAliveInterval).CopyTo(inOptionValues, 8);
            socket.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
        }

        private void DisconnectCore()
        {
            if (Socket == null)
            {
                return;
            }

            try
            {
                if (Socket.Connected)
                {
                    Socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch
            {
            }
            finally
            {
                Socket.Close();
                Socket.Dispose();
                Socket = null;
            }
        }

        private static byte[] CreateReadPdu(byte functionCode, ushort address, ushort quantity)
        {
            return new byte[]
            {
                functionCode,
                HighByte(address),
                LowByte(address),
                HighByte(quantity),
                LowByte(quantity)
            };
        }

        private static byte[] CreateWriteMultipleCoilsPdu(ushort address, bool[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            ushort quantity = checked((ushort)values.Length);
            byte byteCount = checked((byte)((quantity + 7) / 8));
            byte[] pdu = new byte[6 + byteCount];
            pdu[0] = 15;
            pdu[1] = HighByte(address);
            pdu[2] = LowByte(address);
            pdu[3] = HighByte(quantity);
            pdu[4] = LowByte(quantity);
            pdu[5] = byteCount;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i])
                {
                    pdu[6 + i / 8] |= (byte)(1 << (i % 8));
                }
            }

            return pdu;
        }

        private static byte[] CreateWriteMultipleRegistersPdu(ushort address, byte[] registerBytes)
        {
            if (registerBytes == null)
            {
                throw new ArgumentNullException("registerBytes");
            }

            ushort quantity = checked((ushort)((registerBytes.Length + 1) / 2));
            byte byteCount = checked((byte)(quantity * 2));
            byte[] pdu = new byte[6 + byteCount];
            pdu[0] = 16;
            pdu[1] = HighByte(address);
            pdu[2] = LowByte(address);
            pdu[3] = HighByte(quantity);
            pdu[4] = LowByte(quantity);
            pdu[5] = byteCount;
            Buffer.BlockCopy(registerBytes, 0, pdu, 6, registerBytes.Length);
            return pdu;
        }

        private static byte[] CreateRequest(byte station, ushort transactionId, byte[] pdu)
        {
            byte[] request = new byte[7 + pdu.Length];
            request[0] = HighByte(transactionId);
            request[1] = LowByte(transactionId);
            request[2] = 0;
            request[3] = 0;
            ushort length = checked((ushort)(pdu.Length + 1));
            request[4] = HighByte(length);
            request[5] = LowByte(length);
            request[6] = station;
            Buffer.BlockCopy(pdu, 0, request, 7, pdu.Length);
            return request;
        }

        private static int ValidateHeader(byte station, ushort transactionId, byte[] header)
        {
            ushort responseTransactionId = ToUInt16(header[0], header[1]);
            ushort protocolId = ToUInt16(header[2], header[3]);
            ushort responseLength = ToUInt16(header[4], header[5]);
            byte responseStation = header[6];

            if (responseTransactionId != transactionId)
            {
                throw new ModbusTcpProtocolException("Modbus transaction id mismatch.");
            }

            if (protocolId != 0)
            {
                throw new ModbusTcpProtocolException("Invalid Modbus protocol id.");
            }

            if (responseStation != station)
            {
                throw new ModbusTcpProtocolException("Modbus station mismatch.");
            }

            if (responseLength <= 1)
            {
                throw new ModbusTcpProtocolException("Invalid Modbus response length.");
            }

            return responseLength;
        }

        private static void ValidateOriginalHeader(byte[] request, byte[] responseHeader)
        {
            ushort requestTransactionId = ToUInt16(request[0], request[1]);
            ushort responseTransactionId = ToUInt16(responseHeader[0], responseHeader[1]);
            if (responseTransactionId != requestTransactionId)
            {
                throw new ModbusTcpProtocolException("Modbus transaction id mismatch.");
            }

            ushort protocolId = ToUInt16(responseHeader[2], responseHeader[3]);
            if (protocolId != 0)
            {
                throw new ModbusTcpProtocolException("Invalid Modbus protocol id.");
            }

            if (responseHeader[6] != request[6])
            {
                throw new ModbusTcpProtocolException("Modbus station mismatch.");
            }

            ushort responseLength = ToUInt16(responseHeader[4], responseHeader[5]);
            if (responseLength <= 1)
            {
                throw new ModbusTcpProtocolException("Invalid Modbus response length.");
            }
        }

        private static void ValidateBody(byte[] pdu, byte[] body, int bodyLength)
        {
            if (bodyLength == 0)
            {
                throw new ModbusTcpProtocolException("Empty Modbus response.");
            }

            if ((body[0] & 0x80) == 0x80)
            {
                int code = bodyLength > 1 ? body[1] : 0;
                throw new ModbusTcpProtocolException("Modbus exception code: " + code, code);
            }

            if (body[0] != pdu[0])
            {
                throw new ModbusTcpProtocolException("Modbus function code mismatch.");
            }
        }

        private ModbusWriteResponse SetLastWriteResponse(ModbusWriteResponse response)
        {
            lock (_stateLock)
            {
                _lastWriteResponse = response;
            }

            return response;
        }

        private static ModbusWriteResponse ParseWriteSingleResponse(byte[] pdu, byte[] body)
        {
            if (body.Length != pdu.Length)
            {
                throw new ModbusTcpProtocolException("Invalid Modbus write response length.");
            }

            for (int i = 0; i < pdu.Length; i++)
            {
                if (body[i] != pdu[i])
                {
                    throw new ModbusTcpProtocolException("Modbus write response echo mismatch.");
                }
            }

            byte[] valueBytes = new byte[2];
            valueBytes[0] = body[3];
            valueBytes[1] = body[4];
            return new ModbusWriteResponse(body[0], ToUInt16(body[1], body[2]), 1, valueBytes);
        }

        private static ModbusWriteResponse ParseWriteMultipleResponse(byte[] pdu, byte[] body)
        {
            if (body.Length != 5)
            {
                throw new ModbusTcpProtocolException("Invalid Modbus write response length.");
            }

            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] != pdu[i])
                {
                    throw new ModbusTcpProtocolException("Modbus write response echo mismatch.");
                }
            }

            return new ModbusWriteResponse(body[0], ToUInt16(body[1], body[2]), ToUInt16(body[3], body[4]), new byte[0]);
        }

        private void SendAll(byte[] buffer)
        {
            int sent = 0;
            while (sent < buffer.Length)
            {
                int count = Socket.Send(buffer, sent, buffer.Length - sent, SocketFlags.None);
                if (count <= 0)
                {
                    throw new SocketException();
                }

                sent += count;
                RecordBytesSent(count);
            }
        }

        private async Task SendAllAsync(byte[] buffer)
        {
            int sent = 0;
            while (sent < buffer.Length)
            {
                Task<int> sendTask = Task<int>.Factory.FromAsync(
                    (callback, state) => Socket.BeginSend(buffer, sent, buffer.Length - sent, SocketFlags.None, callback, state),
                    Socket.EndSend,
                    null);
                int count = await WithReceiveTimeout(sendTask, "Socket send timeout.").ConfigureAwait(false);

                if (count <= 0)
                {
                    throw new SocketException();
                }

                sent += count;
                RecordBytesSent(count);
            }
        }

        private byte[] ReceiveExact(int length)
        {
            byte[] buffer = new byte[length];
            ReceiveExact(buffer, 0, length);
            return buffer;
        }

        private byte[] RentAndReceiveExact(int length)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                ReceiveExact(buffer, 0, length);
                return buffer;
            }
            catch
            {
                ReturnArray(buffer);
                throw;
            }
        }

        private void ReceiveExact(byte[] buffer, int offset, int length)
        {
            int received = 0;
            while (received < length)
            {
                int count = Socket.Receive(buffer, offset + received, length - received, SocketFlags.None);
                if (count <= 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }

                received += count;
                RecordBytesReceived(count);
            }
        }

        private async Task<byte[]> ReceiveExactAsync(int length)
        {
            byte[] buffer = new byte[length];
            await ReceiveExactAsync(buffer, 0, length).ConfigureAwait(false);
            return buffer;
        }

        private async Task<byte[]> RentAndReceiveExactAsync(int length)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                await ReceiveExactAsync(buffer, 0, length).ConfigureAwait(false);
                return buffer;
            }
            catch
            {
                ReturnArray(buffer);
                throw;
            }
        }

        private async Task ReceiveExactAsync(byte[] buffer, int offset, int length)
        {
            int received = 0;
            while (received < length)
            {
                Task<int> receiveTask = Task<int>.Factory.FromAsync(
                    (callback, state) => Socket.BeginReceive(buffer, offset + received, length - received, SocketFlags.None, callback, state),
                    Socket.EndReceive,
                    null);
                int count = await WithReceiveTimeout(receiveTask, "Socket receive timeout.").ConfigureAwait(false);

                if (count <= 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }

                received += count;
                RecordBytesReceived(count);
            }
        }

        private async Task<T> WithReceiveTimeout<T>(Task<T> operation, string timeoutMessage)
        {
            int timeout = GetReceiveTimeout();
            if (timeout <= 0)
            {
                return await operation.ConfigureAwait(false);
            }

            Task timeoutTask = Task.Delay(timeout);
            Task completedTask = await Task.WhenAny(operation, timeoutTask).ConfigureAwait(false);
            if (completedTask == operation)
            {
                return await operation.ConfigureAwait(false);
            }

            DisconnectCore();
            _ = ObserveFaultAsync(operation);
            throw new ModbusTcpTimeoutException(timeoutMessage);
        }

        private static async Task ObserveFaultAsync<T>(Task<T> task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private int GetReceiveTimeout()
        {
            lock (_stateLock)
            {
                return _receiveTimeOut;
            }
        }

        private static void ValidateTimeoutConfiguration(int receiveTimeOut, int lockWaitTimeOut)
        {
            if (receiveTimeOut > 0 && lockWaitTimeOut >= 0 && lockWaitTimeOut <= receiveTimeOut)
            {
                throw new ArgumentOutOfRangeException(
                    "lockWaitTimeOut",
                    "LockWaitTimeOut must be greater than ReceiveTimeOut.");
            }
        }

        private static byte[] CopyExact(byte[] source, int offset, int length)
        {
            byte[] target = new byte[length];
            Buffer.BlockCopy(source, offset, target, 0, length);
            return target;
        }

        private static void ReturnArray(byte[] buffer)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        private static byte HighByte(ushort value)
        {
            return (byte)(value >> 8);
        }

        private static byte LowByte(ushort value)
        {
            return (byte)(value & 0xFF);
        }

        private static ushort ToUInt16(byte high, byte low)
        {
            return (ushort)((high << 8) | low);
        }

        private bool TryEnterCommunicationLock(bool allowDisposed = false)
        {
            int timeout;
            lock (_stateLock)
            {
                if (!allowDisposed)
                {
                    ThrowIfDisposed();
                }

                timeout = _lockWaitTimeOut;
            }

            if (timeout < 0)
            {
                _communicationLock.Wait();
                return true;
            }

            return _communicationLock.Wait(timeout);
        }

        private Task<bool> TryEnterCommunicationLockAsync(bool allowDisposed = false)
        {
            int timeout;
            lock (_stateLock)
            {
                if (!allowDisposed)
                {
                    ThrowIfDisposed();
                }

                timeout = _lockWaitTimeOut;
            }

            return timeout < 0
                ? WaitCommunicationLockWithoutTimeoutAsync()
                : _communicationLock.WaitAsync(timeout);
        }

        private async Task<bool> WaitCommunicationLockWithoutTimeoutAsync()
        {
            await _communicationLock.WaitAsync().ConfigureAwait(false);
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private void ThrowIfConnectedLocked(string propertyName)
        {
            if (Socket != null && Socket.Connected)
            {
                throw new InvalidOperationException(propertyName + " cannot be changed while connected.");
            }
        }

        private void EnsureConnected()
        {
            ThrowIfDisposed();

            if (Socket == null || !Socket.Connected)
            {
                throw new ModbusTcpException("Socket is not connected.", ModbusTcpErrorCodes.NotConnected);
            }
        }

        private ModbusTcpException ConvertSocketException(SocketException exception)
        {
            int errorCode = ModbusTcpErrorCodes.FromSocketError(exception.SocketErrorCode);
            if (errorCode == ModbusTcpErrorCodes.Timeout)
            {
                return new ModbusTcpTimeoutException("Socket timeout.", exception);
            }

            if (errorCode == ModbusTcpErrorCodes.NetworkDisconnected)
            {
                return new ModbusTcpDisconnectedException("Socket disconnected.", exception);
            }

            return new ModbusTcpException(exception.Message, errorCode, exception);
        }

        private static void CompleteTimedOutConnect(Socket socket, IAsyncResult result)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    socket.EndConnect(result);
                }
                catch
                {
                }
            });
        }

        private void RecordBytesSent(int count)
        {
            lock (_statisticsLock)
            {
                _bytesSent += count;
            }
        }

        private void RecordBytesReceived(int count)
        {
            lock (_statisticsLock)
            {
                _bytesReceived += count;
            }
        }

        private void RecordRequest(long elapsedTicks, bool success)
        {
            lock (_statisticsLock)
            {
                _requestCount++;
                if (!success)
                {
                    _failedRequestCount++;
                }

                _lastElapsedTicks = elapsedTicks;
                _totalElapsedTicks += elapsedTicks;
                if (elapsedTicks > _maxElapsedTicks)
                {
                    _maxElapsedTicks = elapsedTicks;
                }
            }
        }

        private static Result Success()
        {
            return new Result
            {
                IsSuccess = true,
                Message = "Success"
            };
        }

        private static Result Error(string message, int errorCode)
        {
            return new Result
            {
                IsSuccess = false,
                Message = message,
                ErrorCode = errorCode
            };
        }

        private static Result Error(Exception exception)
        {
            return Error(exception.Message, ModbusTcpErrorCodes.FromException(exception));
        }
    }
}
