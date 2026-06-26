using System.Threading.Tasks;
using System;
using System.Buffers;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace XLinkCore.SiemensS7
{
    public class SiemensS7 : DeviceTcpCore
    {
        private const uint KeepAliveTime = 60000;
        private const uint KeepAliveInterval = 5000;
        private const int TpktHeaderLength = 4;
        private const int S7ProtocolOverhead = 64;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private Socket? _socket;
        private ushort _pduRef;
        private int _pduSize = 240;
        private bool _disposed;

        public SiemensPlcs PlcType { get; set; } = SiemensPlcs.S1200;
        public int Rack { get; set; }
        public int Slot { get; set; } = 1;
        public Encoding StringEncoding { get; set; } = Encoding.ASCII;
        public int LockWaitTimeOut { get; set; } = 5000;
        public override bool IsConnected => _socket != null && _socket.Connected;
        protected override Socket? Socket => _socket;

        public SiemensS7()
        {
            Port = 102;
        }

        public SiemensS7(string ip, SiemensPlcs plcType = SiemensPlcs.S1200, int rack = 0, int? slot = null,
            int port = 102)
        {
            IpAddress = ip;
            Port = port;
            PlcType = plcType;
            Rack = rack;
            Slot = slot ?? (plcType == SiemensPlcs.S300 || plcType == SiemensPlcs.S400 ? 2 : 1);
        }

        public override Result Connect()
        {
            if (!Enter()) return Error("Communication lock wait timeout.", -1003);
            try
            {
                ThrowIfDisposed();
                DisconnectCore();
                _socket = CreateSocket();
                ConnectSocket(_socket);
                Handshake();
                return Ok();
            }
            catch (Exception ex)
            {
                DisconnectCore();
                return Error(ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public override Result Disconnect()
        {
            if (!Enter()) return Error("Communication lock wait timeout.", -1003);
            try
            {
                DisconnectCore();
                return Ok();
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public override Result<T> Read<T>(string point)
        {
            try
            {
                Type t = typeof(T);
                S7Address a = ParseAddress(point, t);
                byte[] data = ReadArea(a, ReadLength(t, 1), t == typeof(bool));
                return Ok((T)ToValue(data, 0, t, a.Bit));
            }
            catch (Exception ex)
            {
                return Error<T>(ex);
            }
        }

        public override Result<T[]> ReadArray<T>(string point, ushort length)
        {
            try
            {
                Type t = typeof(T);
                S7Address a = ParseAddress(point, typeof(T[]));
                byte[] data = ReadArea(a, ReadLength(t, length), t == typeof(bool));
                T[] values = new T[length];
                if (t == typeof(bool))
                {
                    for (int i = 0; i < length; i++) values[i] = (T)(object)GetBit(data, a.Bit + i);
                }
                else
                {
                    int size = SizeOf(t);
                    for (int i = 0; i < length; i++) values[i] = (T)ToValue(data, i * size, t, 0);
                }

                return Ok(values);
            }
            catch (Exception ex)
            {
                return Error<T[]>(ex);
            }
        }

        public override Result<string> ReadString(string point, ushort length)
        {
            try
            {
                byte[] data = ReadArea(ParseAddress(point, typeof(string)), length, false);
                return Ok(StringEncoding.GetString(data).TrimEnd('\0'));
            }
            catch (Exception ex)
            {
                return Error<string>(ex);
            }
        }

        public Result<byte[]> ReadBytes(string point, ushort length)
        {
            try
            {
                return Ok(ReadArea(ParseAddress(point, typeof(byte[])), length, false));
            }
            catch (Exception ex)
            {
                return Error<byte[]>(ex);
            }
        }

        public Result<string> ReadSiemensString(string point, ushort maxLength)
        {
            try
            {
                byte[] data = ReadArea(ParseAddress(point, typeof(string)), checked(maxLength + 2), false);
                return Ok(DecodeSiemensString(data, maxLength));
            }
            catch (Exception ex)
            {
                return Error<string>(ex);
            }
        }

        public override Result Write<T>(string point, T value)
        {
            try
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                Type t = typeof(T);
                S7Address a = ParseAddress(point, t);
                using RentedBuffer data = FromValue(value, t, a.Bit);
                WriteArea(a, data.Buffer, data.Length, t == typeof(bool));
                return Ok();
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        public Result WriteBytes(string point, byte[] value)
        {
            try
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                WriteArea(ParseAddress(point, typeof(byte[])), value, value.Length, false);
                return Ok();
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        public Result WriteSiemensString(string point, string value, byte maxLength)
        {
            try
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                using RentedBuffer data = EncodeSiemensString(value, maxLength);
                WriteArea(ParseAddress(point, typeof(string)), data.Buffer, data.Length, false);
                return Ok();
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        public override async Task<Result> ConnectAsync()
        {
            if (!await EnterAsync().ConfigureAwait(false)) return Error("Communication lock wait timeout.", -1003);
            try
            {
                ThrowIfDisposed();
                DisconnectCore();
                _socket = CreateSocket();
                Task task = Task.Factory.FromAsync((cb, st) => _socket.BeginConnect(IpAddress, Port, cb, st),
                    _socket.EndConnect, null);
                await WithTimeout(task, ConnectTimeOut, "Connect timeout.").ConfigureAwait(false);
                await HandshakeAsync().ConfigureAwait(false);
                return Ok();
            }
            catch (Exception ex)
            {
                DisconnectCore();
                return Error(ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public override async Task<Result> DisconnectAsync()
        {
            if (!await EnterAsync().ConfigureAwait(false)) return Error("Communication lock wait timeout.", -1003);
            try
            {
                DisconnectCore();
                return Ok();
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public override async Task<Result<T>> ReadAsync<T>(string point)
        {
            try
            {
                Type t = typeof(T);
                S7Address a = ParseAddress(point, t);
                byte[] data = await ReadAreaAsync(a, ReadLength(t, 1), t == typeof(bool)).ConfigureAwait(false);
                return Ok((T)ToValue(data, 0, t, a.Bit));
            }
            catch (Exception ex)
            {
                return Error<T>(ex);
            }
        }

        public override async Task<Result<T[]>> ReadArrayAsync<T>(string point, ushort length)
        {
            try
            {
                Type t = typeof(T);
                S7Address a = ParseAddress(point, typeof(T[]));
                byte[] data = await ReadAreaAsync(a, ReadLength(t, length), t == typeof(bool)).ConfigureAwait(false);
                T[] values = new T[length];
                if (t == typeof(bool))
                    for (int i = 0; i < length; i++)
                        values[i] = (T)(object)GetBit(data, a.Bit + i);
                else
                {
                    int size = SizeOf(t);
                    for (int i = 0; i < length; i++) values[i] = (T)ToValue(data, i * size, t, 0);
                }

                return Ok(values);
            }
            catch (Exception ex)
            {
                return Error<T[]>(ex);
            }
        }

        public override async Task<Result<string>> ReadStringAsync(string point, ushort length)
        {
            try
            {
                byte[] data = await ReadAreaAsync(ParseAddress(point, typeof(string)), length, false)
                    .ConfigureAwait(false);
                return Ok(StringEncoding.GetString(data).TrimEnd('\0'));
            }
            catch (Exception ex)
            {
                return Error<string>(ex);
            }
        }

        public async Task<Result<byte[]>> ReadBytesAsync(string point, ushort length)
        {
            try
            {
                return Ok(await ReadAreaAsync(ParseAddress(point, typeof(byte[])), length, false).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                return Error<byte[]>(ex);
            }
        }

        public async Task<Result<string>> ReadSiemensStringAsync(string point, ushort maxLength)
        {
            try
            {
                byte[] data = await ReadAreaAsync(ParseAddress(point, typeof(string)), checked(maxLength + 2), false)
                    .ConfigureAwait(false);
                return Ok(DecodeSiemensString(data, maxLength));
            }
            catch (Exception ex)
            {
                return Error<string>(ex);
            }
        }

        public override async Task<Result> WriteAsync<T>(string point, T value)
        {
            try
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                Type t = typeof(T);
                S7Address a = ParseAddress(point, t);
                using RentedBuffer data = FromValue(value, t, a.Bit);
                await WriteAreaAsync(a, data.Buffer, data.Length, t == typeof(bool)).ConfigureAwait(false);
                return Ok();
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        public async Task<Result> WriteBytesAsync(string point, byte[] value)
        {
            try
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                await WriteAreaAsync(ParseAddress(point, typeof(byte[])), value, value.Length, false).ConfigureAwait(false);
                return Ok();
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        public async Task<Result> WriteSiemensStringAsync(string point, string value, byte maxLength)
        {
            try
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                using RentedBuffer data = EncodeSiemensString(value, maxLength);
                await WriteAreaAsync(ParseAddress(point, typeof(string)), data.Buffer, data.Length, false)
                    .ConfigureAwait(false);
                return Ok();
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            bool entered = false;
            try
            {
                entered = Enter(true);
                DisconnectCore();
            }
            catch
            {
            }
            finally
            {
                if (entered) _lock.Release();
                _lock.Dispose();
            }
        }

        private byte[] ReadArea(S7Address address, int length, bool bit)
        {
            return Locked(() =>
            {
                EnsureConnected();
                using RentedBuffer request = CreateRead(address, length, bit);
                SendAll(request.Buffer, request.Length);
                using RentedBuffer response = ReceiveFrame();
                return ParseRead(response.Buffer, response.Length, length, bit);
            });
        }

        private Task<byte[]> ReadAreaAsync(S7Address address, int length, bool bit)
        {
            return LockedAsync(async () =>
            {
                EnsureConnected();
                using RentedBuffer request = CreateRead(address, length, bit);
                await SendAllAsync(request.Buffer, request.Length).ConfigureAwait(false);
                using RentedBuffer response = await ReceiveFrameAsync().ConfigureAwait(false);
                return ParseRead(response.Buffer, response.Length, length, bit);
            });
        }

        private void WriteArea(S7Address address, byte[] data, int dataLength, bool bit)
        {
            Locked(() =>
            {
                EnsureConnected();
                using RentedBuffer request = CreateWrite(address, data, dataLength, bit);
                SendAll(request.Buffer, request.Length);
                using RentedBuffer response = ReceiveFrame();
                ValidateWrite(response.Buffer, response.Length);
                return true;
            });
        }

        private Task WriteAreaAsync(S7Address address, byte[] data, int dataLength, bool bit)
        {
            return LockedAsync(async () =>
            {
                EnsureConnected();
                using RentedBuffer request = CreateWrite(address, data, dataLength, bit);
                await SendAllAsync(request.Buffer, request.Length).ConfigureAwait(false);
                using RentedBuffer response = await ReceiveFrameAsync().ConfigureAwait(false);
                ValidateWrite(response.Buffer, response.Length);
                return true;
            });
        }

        private T Locked<T>(Func<T> action)
        {
            if (!Enter()) throw new SiemensS7Exception("Communication lock wait timeout.", SiemensS7ErrorCodes.CommunicationLockTimeout);
            try
            {
                return action();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<T> LockedAsync<T>(Func<Task<T>> action)
        {
            if (!await EnterAsync().ConfigureAwait(false))
                throw new SiemensS7Exception("Communication lock wait timeout.", SiemensS7ErrorCodes.CommunicationLockTimeout);
            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        private void Handshake()
        {
            using RentedBuffer connectionRequest = CreateConnectionRequest();
            SendAll(connectionRequest.Buffer, connectionRequest.Length);
            using RentedBuffer connectionResponse = ReceiveFrame();
            ValidateConnection(connectionResponse.Buffer, connectionResponse.Length);

            using RentedBuffer setupRequest = CreateSetup();
            SendAll(setupRequest.Buffer, setupRequest.Length);
            using RentedBuffer setupResponse = ReceiveFrame();
            ValidateSetup(setupResponse.Buffer, setupResponse.Length);
        }

        private async Task HandshakeAsync()
        {
            using RentedBuffer connectionRequest = CreateConnectionRequest();
            await SendAllAsync(connectionRequest.Buffer, connectionRequest.Length).ConfigureAwait(false);
            using RentedBuffer connectionResponse = await ReceiveFrameAsync().ConfigureAwait(false);
            ValidateConnection(connectionResponse.Buffer, connectionResponse.Length);

            using RentedBuffer setupRequest = CreateSetup();
            await SendAllAsync(setupRequest.Buffer, setupRequest.Length).ConfigureAwait(false);
            using RentedBuffer setupResponse = await ReceiveFrameAsync().ConfigureAwait(false);
            ValidateSetup(setupResponse.Buffer, setupResponse.Length);
        }

        private void ConnectSocket(Socket socket)
        {
            if (ConnectTimeOut <= 0)
            {
                socket.Connect(IpAddress, Port);
                return;
            }

            IAsyncResult result = socket.BeginConnect(IpAddress, Port, null, null);
            try
            {
                if (!WaitSocketOperation(result, ConnectTimeOut))
                {
                    DisconnectCore();
                    CompleteTimedOutConnect(socket, result);
                    throw new SiemensS7TimeoutException("Connect timeout.");
                }

                socket.EndConnect(result);
            }
            finally
            {
                result.AsyncWaitHandle.Dispose();
            }
        }

        private static bool WaitSocketOperation(IAsyncResult result, int timeout)
        {
            return timeout <= 0 || result.AsyncWaitHandle.WaitOne(timeout);
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

        private RentedBuffer CreateConnectionRequest()
        {
            ushort localTsap = 0x0100;
            ushort remoteTsap = (ushort)(0x0100 + Rack * 0x20 + Slot);
            byte[] buffer = Rent(22);
            buffer[0] = 0x03; buffer[1] = 0x00; buffer[2] = 0x00; buffer[3] = 0x16;
            buffer[4] = 0x11; buffer[5] = 0xE0; buffer[6] = 0x00; buffer[7] = 0x00;
            buffer[8] = 0x00; buffer[9] = 0x01; buffer[10] = 0x00; buffer[11] = 0xC1;
            buffer[12] = 0x02; buffer[13] = Hi(localTsap); buffer[14] = Lo(localTsap); buffer[15] = 0xC2;
            buffer[16] = 0x02; buffer[17] = Hi(remoteTsap); buffer[18] = Lo(remoteTsap); buffer[19] = 0xC0;
            buffer[20] = 0x01; buffer[21] = 0x0A;
            return new RentedBuffer(buffer, 22);
        }

        private RentedBuffer CreateSetup()
        {
            ushort p = NextRef();
            byte[] buffer = Rent(25);
            buffer[0] = 0x03; buffer[1] = 0x00; buffer[2] = 0x00; buffer[3] = 0x19;
            buffer[4] = 0x02; buffer[5] = 0xF0; buffer[6] = 0x80; buffer[7] = 0x32;
            buffer[8] = 0x01; buffer[9] = 0x00; buffer[10] = 0x00; buffer[11] = Hi(p);
            buffer[12] = Lo(p); buffer[13] = 0x00; buffer[14] = 0x08; buffer[15] = 0x00;
            buffer[16] = 0x00; buffer[17] = 0xF0; buffer[18] = 0x00; buffer[19] = 0x00;
            buffer[20] = 0x01; buffer[21] = 0x00; buffer[22] = 0x01; buffer[23] = 0x03;
            buffer[24] = 0xC0;
            return new RentedBuffer(buffer, 25);
        }

        private RentedBuffer CreateRead(S7Address a, int length, bool bit)
        {
            ushort p = NextRef();
            int bitAddress = a.ByteOffset * 8 + (bit ? a.Bit : 0);
            ushort len = checked((ushort)length);
            byte[] buffer = Rent(31);
            buffer[0] = 0x03; buffer[1] = 0x00; buffer[2] = 0x00; buffer[3] = 0x1F;
            buffer[4] = 0x02; buffer[5] = 0xF0; buffer[6] = 0x80; buffer[7] = 0x32;
            buffer[8] = 0x01; buffer[9] = 0x00; buffer[10] = 0x00; buffer[11] = Hi(p);
            buffer[12] = Lo(p); buffer[13] = 0x00; buffer[14] = 0x0E; buffer[15] = 0x00;
            buffer[16] = 0x00; buffer[17] = 0x04; buffer[18] = 0x01; buffer[19] = 0x12;
            buffer[20] = 0x0A; buffer[21] = 0x10; buffer[22] = bit ? (byte)0x01 : (byte)0x02;
            buffer[23] = Hi(len); buffer[24] = Lo(len); buffer[25] = Hi(a.Db); buffer[26] = Lo(a.Db);
            buffer[27] = a.Area; buffer[28] = (byte)((bitAddress >> 16) & 0xFF);
            buffer[29] = (byte)((bitAddress >> 8) & 0xFF); buffer[30] = (byte)(bitAddress & 0xFF);
            return new RentedBuffer(buffer, 31);
        }

        private RentedBuffer CreateWrite(S7Address a, byte[] data, int dataLength, bool bit)
        {
            ushort p = NextRef();
            int bitAddress = a.ByteOffset * 8 + (bit ? a.Bit : 0);
            ushort bits = (ushort)(bit ? 1 : dataLength * 8);
            int pad = dataLength % 2;
            ushort dataLen = (ushort)(4 + dataLength + pad);
            ushort packetLen = (ushort)(35 + dataLength + pad);
            byte[] req = Rent(packetLen);
            req[0] = 0x03; req[1] = 0x00; req[2] = Hi(packetLen); req[3] = Lo(packetLen);
            req[4] = 0x02; req[5] = 0xF0; req[6] = 0x80; req[7] = 0x32;
            req[8] = 0x01; req[9] = 0x00; req[10] = 0x00; req[11] = Hi(p);
            req[12] = Lo(p); req[13] = 0x00; req[14] = 0x0E; req[15] = Hi(dataLen);
            req[16] = Lo(dataLen); req[17] = 0x05; req[18] = 0x01; req[19] = 0x12;
            req[20] = 0x0A; req[21] = 0x10; req[22] = bit ? (byte)0x01 : (byte)0x02;
            req[23] = 0x00; req[24] = bit ? (byte)0x01 : (byte)dataLength; req[25] = Hi(a.Db);
            req[26] = Lo(a.Db); req[27] = a.Area; req[28] = (byte)((bitAddress >> 16) & 0xFF);
            req[29] = (byte)((bitAddress >> 8) & 0xFF); req[30] = (byte)(bitAddress & 0xFF);
            req[31] = 0x00; req[32] = bit ? (byte)0x03 : (byte)0x04; req[33] = Hi(bits); req[34] = Lo(bits);
            Buffer.BlockCopy(data, 0, req, 35, dataLength);
            if (pad > 0) req[packetLen - 1] = 0;
            return new RentedBuffer(req, packetLen);
        }

        private byte[] ParseRead(byte[] response, int responseLength, int requested, bool bit)
        {
            ValidateS7(response, responseLength);
            if (responseLength < 25) throw new SiemensS7ProtocolException("Invalid S7 read response length.");
            if (response[21] != 0xFF)
                throw new SiemensS7ProtocolException("S7 read failed, return code: " +
                                                     response[21].ToString("X2", CultureInfo.InvariantCulture),
                    response[21]);
            int bitLength = U16(response[23], response[24]);
            int byteLength = bit ? (bitLength + 7) / 8 : bitLength / 8;
            if (responseLength < 25 + byteLength || byteLength < requested)
                throw new SiemensS7ProtocolException("S7 read response data is shorter than requested.");
            byte[] data = new byte[requested];
            Buffer.BlockCopy(response, 25, data, 0, requested);
            return data;
        }

        private static void ValidateWrite(byte[] response, int responseLength)
        {
            ValidateS7(response, responseLength);
            if (responseLength < 22) throw new SiemensS7ProtocolException("Invalid S7 write response length.");
            if (response[21] != 0xFF)
                throw new SiemensS7ProtocolException("S7 write failed, return code: " +
                                                     response[21].ToString("X2", CultureInfo.InvariantCulture),
                    response[21]);
        }

        private static void ValidateConnection(byte[] response, int responseLength)
        {
            if (responseLength < 7 || response[5] != 0xD0)
                throw new SiemensS7ProtocolException("Invalid S7 COTP connection response.");
        }

        private void ValidateSetup(byte[] response, int responseLength)
        {
            ValidateS7(response, responseLength);
            if (responseLength >= 27) _pduSize = U16(response[25], response[26]);
        }

        private static void ValidateS7(byte[] response, int responseLength)
        {
            if (responseLength < 19 || response[7] != 0x32)
                throw new SiemensS7ProtocolException("Invalid S7 response.");
            if (response[17] != 0 || response[18] != 0)
                throw new SiemensS7ProtocolException("S7 response error: " +
                                                     response[17].ToString("X2", CultureInfo.InvariantCulture) + "/" +
                                                     response[18].ToString("X2", CultureInfo.InvariantCulture));
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
                TrySetSocketOption(socket, SocketOptionLevel.Tcp, (SocketOptionName)4,
                    unchecked((int)(KeepAliveTime / 1000)));
                TrySetSocketOption(socket, SocketOptionLevel.Tcp, (SocketOptionName)5,
                    unchecked((int)(KeepAliveInterval / 1000)));
                return;
            }

            byte[] inOptionValues = new byte[12];
            BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);
            BitConverter.GetBytes(KeepAliveTime).CopyTo(inOptionValues, 4);
            BitConverter.GetBytes(KeepAliveInterval).CopyTo(inOptionValues, 8);
            socket.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
        }

        private static void TrySetSocketOption(Socket socket, SocketOptionLevel level, SocketOptionName name, int value)
        {
            try
            {
                socket.SetSocketOption(level, name, value);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void SendAll(byte[] buffer, int length)
        {
            int sent = 0;
            while (sent < length)
            {
                int count = _socket!.Send(buffer, sent, length - sent, SocketFlags.None);
                if (count <= 0) throw new SocketException((int)SocketError.ConnectionReset);
                sent += count;
            }
        }

        private async Task SendAllAsync(byte[] buffer, int length)
        {
            int sent = 0;
            while (sent < length)
            {
                Task<int> task = Task<int>.Factory.FromAsync(
                    (cb, st) => _socket!.BeginSend(buffer, sent, length - sent, SocketFlags.None, cb, st),
                    _socket!.EndSend, null);
                int count = await WithTimeout(task, ReceiveTimeOut, "S7 socket send timeout.").ConfigureAwait(false);
                if (count <= 0) throw new SocketException((int)SocketError.ConnectionReset);
                sent += count;
            }
        }

        private RentedBuffer ReceiveFrame()
        {
            byte[] header = Rent(TpktHeaderLength);
            ReceiveExact(header, 0, TpktHeaderLength);
            int length = U16(header[2], header[3]);
            try
            {
                ValidateFrameLength(length);
                byte[] frame = Rent(length);
                Buffer.BlockCopy(header, 0, frame, 0, TpktHeaderLength);
                ReceiveExact(frame, TpktHeaderLength, length - TpktHeaderLength);
                return new RentedBuffer(frame, length);
            }
            finally
            {
                ReturnArray(header);
            }
        }

        private async Task<RentedBuffer> ReceiveFrameAsync()
        {
            byte[] header = Rent(TpktHeaderLength);
            await ReceiveExactAsync(header, 0, TpktHeaderLength).ConfigureAwait(false);
            int length = U16(header[2], header[3]);
            try
            {
                ValidateFrameLength(length);
                byte[] frame = Rent(length);
                Buffer.BlockCopy(header, 0, frame, 0, TpktHeaderLength);
                await ReceiveExactAsync(frame, TpktHeaderLength, length - TpktHeaderLength).ConfigureAwait(false);
                return new RentedBuffer(frame, length);
            }
            finally
            {
                ReturnArray(header);
            }
        }

        private void ReceiveExact(byte[] buffer, int offset, int length)
        {
            int received = 0;
            while (received < length)
            {
                int count = _socket!.Receive(buffer, offset + received, length - received, SocketFlags.None);
                if (count <= 0) throw new SocketException((int)SocketError.ConnectionReset);
                received += count;
            }
        }

        private async Task ReceiveExactAsync(byte[] buffer, int offset, int length)
        {
            int received = 0;
            while (received < length)
            {
                Task<int> task = Task<int>.Factory.FromAsync(
                    (cb, st) => _socket!.BeginReceive(buffer, offset + received, length - received, SocketFlags.None,
                        cb, st), _socket!.EndReceive, null);
                int count = await WithTimeout(task, ReceiveTimeOut, "S7 socket receive timeout.").ConfigureAwait(false);
                if (count <= 0) throw new SocketException((int)SocketError.ConnectionReset);
                received += count;
            }
        }

        private async Task WithTimeout(Task task, int timeout, string message)
        {
            if (timeout <= 0)
            {
                await task.ConfigureAwait(false);
                return;
            }

            Task completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed == task)
            {
                await task.ConfigureAwait(false);
                return;
            }

            DisconnectCore();
            _ = Observe(task);
            throw new SiemensS7TimeoutException(message);
        }

        private async Task<T> WithTimeout<T>(Task<T> task, int timeout, string message)
        {
            await WithTimeout((Task)task, timeout, message).ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }

        private void ValidateFrameLength(int length)
        {
            int maxFrameLength = Math.Max(_pduSize + S7ProtocolOverhead, 512);
            if (length < TpktHeaderLength || length > maxFrameLength)
                throw new SiemensS7ProtocolException("Invalid S7 frame length.");
        }

        private static SiemensS7Exception ConvertSocketException(SocketException exception)
        {
            int errorCode = SiemensS7ErrorCodes.FromSocketError(exception.SocketErrorCode);
            if (errorCode == SiemensS7ErrorCodes.Timeout)
                return new SiemensS7TimeoutException("Socket timeout.", exception);
            if (errorCode == SiemensS7ErrorCodes.NetworkDisconnected)
                return new SiemensS7DisconnectedException("Socket disconnected.", exception);
            return new SiemensS7Exception(exception.Message, errorCode, exception);
        }

        private void DisconnectCore()
        {
            Socket? socket = _socket;
            _socket = null;
            if (socket == null) return;
            try
            {
                if (socket.Connected) socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }
            finally
            {
                socket.Close();
                socket.Dispose();
            }
        }

        private S7Address ParseAddress(string point, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(point)) throw new ArgumentException("Point cannot be empty.", nameof(point));
            string text = point.Trim().ToUpperInvariant();
            Type elementType = targetType.IsArray ? targetType.GetElementType()! : targetType;
            if (text.StartsWith("DB", StringComparison.Ordinal))
            {
                int dot = text.IndexOf('.');
                if (dot <= 2) throw new FormatException("Invalid DB address: " + point);
                ushort db = ParseUShort(text.Substring(2, dot - 2));
                string rest = text.Substring(dot + 1);
                if (rest.StartsWith("DBX", StringComparison.Ordinal))
                {
                    ParseByteBit(rest.Substring(3), out int byteOffset, out int bit);
                    return new S7Address(0x84, db, byteOffset, bit);
                }

                int offsetStart = rest.StartsWith("DBB", StringComparison.Ordinal) ||
                                  rest.StartsWith("DBW", StringComparison.Ordinal) ||
                                  rest.StartsWith("DBD", StringComparison.Ordinal)
                    ? 3
                    : 0;
                string dbAddressText = rest.Substring(offsetStart);
                if (elementType == typeof(bool) || dbAddressText.IndexOf('.') >= 0)
                {
                    ParseByteBit(dbAddressText, out int byteOffset, out int bit);
                    return new S7Address(0x84, db, byteOffset, bit);
                }

                return new S7Address(0x84, db, ParseInt(dbAddressText), 0);
            }

            if (!TryParseArea(text, out byte area, out string addressText))
                throw new FormatException("Unsupported S7 address: " + point);

            addressText = TrimAddressDataType(addressText);
            if (elementType == typeof(bool) || addressText.IndexOf('.') >= 0)
            {
                ParseByteBit(addressText, out int byteOffset, out int bit);
                return new S7Address(area, 0, byteOffset, bit);
            }

            return new S7Address(area, 0, ParseInt(addressText), 0);
        }

        private static bool TryParseArea(string text, out byte area, out string addressText)
        {
            if (text.StartsWith("SM", StringComparison.Ordinal))
                return Area(0x05, text, 2, out area, out addressText);
            if (text.StartsWith("AI", StringComparison.Ordinal))
                return Area(0x06, text, 2, out area, out addressText);
            if (text.StartsWith("AQ", StringComparison.Ordinal))
                return Area(0x07, text, 2, out area, out addressText);
            if (text.StartsWith("PI", StringComparison.Ordinal) || text.StartsWith("PQ", StringComparison.Ordinal))
                return Area(0x80, text, 2, out area, out addressText);

            switch (text[0])
            {
                case 'P':
                    return Area(0x80, text, 1, out area, out addressText);
                case 'I':
                case 'E':
                    return Area(0x81, text, 1, out area, out addressText);
                case 'Q':
                case 'A':
                    return Area(0x82, text, 1, out area, out addressText);
                case 'M':
                    return Area(0x83, text, 1, out area, out addressText);
                case 'V':
                    return Area(0x87, text, 1, out area, out addressText);
                case 'C':
                    return Area(0x1C, text, 1, out area, out addressText);
                case 'T':
                    return Area(0x1D, text, 1, out area, out addressText);
                default:
                    area = 0;
                    addressText = string.Empty;
                    return false;
            }
        }

        private static bool Area(byte value, string text, int prefixLength, out byte area, out string addressText)
        {
            area = value;
            addressText = text.Substring(prefixLength);
            return addressText.Length > 0;
        }

        private static string TrimAddressDataType(string addressText)
        {
            if (addressText.StartsWith("B", StringComparison.Ordinal) ||
                addressText.StartsWith("W", StringComparison.Ordinal) ||
                addressText.StartsWith("D", StringComparison.Ordinal) ||
                addressText.StartsWith("X", StringComparison.Ordinal))
                return addressText.Substring(1);
            return addressText;
        }

        private RentedBuffer FromValue(object value, Type type, int bit)
        {
            if (type.IsArray) return FromArrayValue((Array)value, type.GetElementType()!);
            if (type == typeof(bool))
            {
                byte[] boolBytes = Rent(1);
                boolBytes[0] = (byte)((bool)value ? 1 << bit : 0);
                return new RentedBuffer(boolBytes, 1);
            }

            if (type == typeof(byte))
            {
                byte[] byteBytes = Rent(1);
                byteBytes[0] = (byte)value;
                return new RentedBuffer(byteBytes, 1);
            }

            byte[] bytes;
            if (type == typeof(short)) bytes = BitConverter.GetBytes((short)value);
            else if (type == typeof(ushort)) bytes = BitConverter.GetBytes((ushort)value);
            else if (type == typeof(int)) bytes = BitConverter.GetBytes((int)value);
            else if (type == typeof(uint)) bytes = BitConverter.GetBytes((uint)value);
            else if (type == typeof(long)) bytes = BitConverter.GetBytes((long)value);
            else if (type == typeof(ulong)) bytes = BitConverter.GetBytes((ulong)value);
            else if (type == typeof(float)) bytes = BitConverter.GetBytes((float)value);
            else if (type == typeof(double)) bytes = BitConverter.GetBytes((double)value);
            else if (type == typeof(string)) bytes = StringEncoding.GetBytes((string)value);
            else throw new NotSupportedException("Unsupported write type: " + type.FullName);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            byte[] buffer = Rent(bytes.Length);
            Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);
            return new RentedBuffer(buffer, bytes.Length);
        }

        private RentedBuffer FromArrayValue(Array values, Type elementType)
        {
            if (elementType == typeof(bool))
                throw new NotSupportedException("Bool array writes are not supported; write individual bits.");
            if (elementType == typeof(string))
                throw new NotSupportedException("String array writes are not supported.");

            int elementSize = SizeOf(elementType);
            byte[] bytes = Rent(checked(elementSize * values.Length));
            for (int i = 0; i < values.Length; i++)
            {
                object? value = values.GetValue(i);
                if (value == null) throw new ArgumentNullException(nameof(values));
                using RentedBuffer itemBytes = FromValue(value, elementType, 0);
                Buffer.BlockCopy(itemBytes.Buffer, 0, bytes, i * elementSize, elementSize);
            }

            return new RentedBuffer(bytes, checked(elementSize * values.Length));
        }

        private static object ToValue(byte[] bytes, int offset, Type type, int bit)
        {
            if (type == typeof(bool)) return GetBit(bytes, bit);
            int size = SizeOf(type);
            byte[] valueBytes = Rent(size);
            try
            {
                Buffer.BlockCopy(bytes, offset, valueBytes, 0, size);
                if (BitConverter.IsLittleEndian) Array.Reverse(valueBytes, 0, size);
                if (type == typeof(byte)) return valueBytes[0];
                if (type == typeof(short)) return BitConverter.ToInt16(valueBytes, 0);
                if (type == typeof(ushort)) return BitConverter.ToUInt16(valueBytes, 0);
                if (type == typeof(int)) return BitConverter.ToInt32(valueBytes, 0);
                if (type == typeof(uint)) return BitConverter.ToUInt32(valueBytes, 0);
                if (type == typeof(long)) return BitConverter.ToInt64(valueBytes, 0);
                if (type == typeof(ulong)) return BitConverter.ToUInt64(valueBytes, 0);
                if (type == typeof(float)) return BitConverter.ToSingle(valueBytes, 0);
                if (type == typeof(double)) return BitConverter.ToDouble(valueBytes, 0);
                throw new NotSupportedException("Unsupported read type: " + type.FullName);
            }
            finally
            {
                ReturnArray(valueBytes);
            }
        }

        private string DecodeSiemensString(byte[] bytes, ushort maxLength)
        {
            if (bytes.Length < 2) throw new SiemensS7ProtocolException("Invalid Siemens STRING response length.");
            int declaredMaxLength = bytes[0];
            int currentLength = bytes[1];
            int allowedLength = Math.Min(declaredMaxLength, maxLength);
            if (currentLength > allowedLength || currentLength > bytes.Length - 2)
                throw new SiemensS7ProtocolException("Invalid Siemens STRING current length.");
            return StringEncoding.GetString(bytes, 2, currentLength);
        }

        private RentedBuffer EncodeSiemensString(string value, byte maxLength)
        {
            byte[] textBytes = StringEncoding.GetBytes(value);
            if (textBytes.Length > maxLength)
                throw new ArgumentOutOfRangeException(nameof(value), "Siemens STRING value exceeds max length.");

            byte[] bytes = Rent(maxLength + 2);
            bytes[0] = maxLength;
            bytes[1] = (byte)textBytes.Length;
            Buffer.BlockCopy(textBytes, 0, bytes, 2, textBytes.Length);
            Array.Clear(bytes, 2 + textBytes.Length, maxLength - textBytes.Length);
            return new RentedBuffer(bytes, maxLength + 2);
        }

        private ushort NextRef() => unchecked(++_pduRef);

        private void EnsureConnected()
        {
            ThrowIfDisposed();
            if (_socket == null || !_socket.Connected)
                throw new SiemensS7Exception("Socket is not connected.", SiemensS7ErrorCodes.NotConnected);
        }

        private bool Enter(bool allowDisposed = false)
        {
            if (!allowDisposed) ThrowIfDisposed();
            if (LockWaitTimeOut < 0)
            {
                _lock.Wait();
                return true;
            }

            return _lock.Wait(LockWaitTimeOut);
        }

        private Task<bool> EnterAsync()
        {
            ThrowIfDisposed();
            return LockWaitTimeOut < 0 ? EnterNoTimeoutAsync() : _lock.WaitAsync(LockWaitTimeOut);
        }

        private async Task<bool> EnterNoTimeoutAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
        }

        private static async Task Observe(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private static bool GetBit(byte[] bytes, int bitIndex)
        {
            int byteIndex = bitIndex / 8;
            int bit = bitIndex % 8;
            if (byteIndex < 0 || byteIndex >= bytes.Length)
                throw new SiemensS7ProtocolException("S7 bit response data is shorter than requested.");
            return (bytes[byteIndex] & (1 << bit)) != 0;
        }

        private static int ReadLength(Type type, int count) =>
            type == typeof(bool) ? (count + 7) / 8 : checked(SizeOf(type) * count);

        private static int SizeOf(Type type)
        {
            if (type == typeof(bool) || type == typeof(byte) || type == typeof(string)) return 1;
            if (type == typeof(short) || type == typeof(ushort)) return 2;
            if (type == typeof(int) || type == typeof(uint) || type == typeof(float)) return 4;
            if (type == typeof(long) || type == typeof(ulong) || type == typeof(double)) return 8;
            throw new NotSupportedException("Unsupported type: " + type.FullName);
        }

        private static void ParseByteBit(string text, out int byteOffset, out int bit)
        {
            int dot = text.IndexOf('.');
            if (dot < 0)
            {
                byteOffset = ParseInt(text);
                bit = 0;
                return;
            }

            byteOffset = ParseInt(text.Substring(0, dot));
            bit = ParseInt(text.Substring(dot + 1));
            if (bit < 0 || bit > 7) throw new FormatException("S7 bit index must be between 0 and 7.");
        }

        private static int ParseInt(string text)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 0)
                throw new FormatException("Invalid S7 address offset: " + text);
            return value;
        }

        private static ushort ParseUShort(string text)
        {
            if (!ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort value))
                throw new FormatException("Invalid S7 DB number: " + text);
            return value;
        }

        private static byte Hi(ushort value) => (byte)(value >> 8);
        private static byte Lo(ushort value) => (byte)(value & 0xFF);
        private static ushort U16(byte high, byte low) => (ushort)((high << 8) | low);
        private static byte[] Rent(int minimumLength) => ArrayPool<byte>.Shared.Rent(minimumLength);
        private static void ReturnArray(byte[] buffer) => ArrayPool<byte>.Shared.Return(buffer);
        private static Result Ok() => new Result { IsSuccess = true, Message = "Success" };
        private static Result<T> Ok<T>(T data) => new Result<T>(data) { IsSuccess = true, Message = "Success" };
        private static Result Error(Exception exception) => Error(exception.Message, SiemensS7ErrorCodes.FromException(exception));

        private static Result Error(string message, int errorCode) => new Result
            { IsSuccess = false, Message = message, ErrorCode = errorCode };

        private static Result<T> Error<T>(Exception exception) => new Result<T>
            { IsSuccess = false, Message = exception.Message, ErrorCode = SiemensS7ErrorCodes.FromException(exception) };

        private readonly struct S7Address
        {
            public S7Address(byte area, ushort db, int byteOffset, int bit)
            {
                Area = area;
                Db = db;
                ByteOffset = byteOffset;
                Bit = bit;
            }

            public byte Area { get; }
            public ushort Db { get; }
            public int ByteOffset { get; }
            public int Bit { get; }
        }

        private readonly struct RentedBuffer : IDisposable
        {
            public RentedBuffer(byte[] buffer, int length)
            {
                Buffer = buffer;
                Length = length;
            }

            public byte[] Buffer { get; }
            public int Length { get; }

            public void Dispose()
            {
                ReturnArray(Buffer);
            }
        }
    }
}
