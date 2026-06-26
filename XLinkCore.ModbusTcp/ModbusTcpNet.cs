using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace XLinkCore.ModbusTcp
{
    public class ModbusTcpNet : ICommunicationCore, ICommunicationCoreAsync
    {
        private readonly SemaphoreSlim _communicationLock = new SemaphoreSlim(1, 1);
        private ushort _transactionId;

        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int ReceiveTimeOut { get; set; }
        public int ConnectTimeOut { get; set; }
        public Socket Socket { get; private set; }

        public void Dispose()
        {
            Disconnect();
            _communicationLock.Dispose();
        }

        public Result Connect()
        {
            _communicationLock.Wait();
            try
            {
                DisconnectCore();

                Socket = CreateSocket();
                if (ConnectTimeOut > 0)
                {
                    IAsyncResult result = Socket.BeginConnect(IpAddress, Port, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(ConnectTimeOut))
                    {
                        DisconnectCore();
                        return Error("Connect timeout.", -1);
                    }

                    Socket.EndConnect(result);
                }
                else
                {
                    Socket.Connect(IpAddress, Port);
                }

                return Success();
            }
            catch (Exception ex)
            {
                DisconnectCore();
                return Error(ex.Message, -1);
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        public Result Disconnect()
        {
            _communicationLock.Wait();
            try
            {
                DisconnectCore();
                return Success();
            }
            catch (Exception ex)
            {
                Socket = null;
                return Error(ex.Message, -1);
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        public async Task<Result> ConnectAsync()
        {
            await _communicationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                DisconnectCore();

                Socket = CreateSocket();
                Task connectTask = Task.Factory.FromAsync(
                    (callback, state) => Socket.BeginConnect(IpAddress, Port, callback, state),
                    Socket.EndConnect,
                    null);

                if (ConnectTimeOut > 0)
                {
                    Task timeoutTask = Task.Delay(ConnectTimeOut);
                    Task completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
                    if (completedTask != connectTask)
                    {
                        DisconnectCore();
                        return Error("Connect timeout.", -1);
                    }
                }

                await connectTask.ConfigureAwait(false);
                return Success();
            }
            catch (Exception ex)
            {
                DisconnectCore();
                return Error(ex.Message, -1);
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        public async Task<Result> DisconnectAsync()
        {
            await _communicationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                DisconnectCore();
                return Success();
            }
            catch (Exception ex)
            {
                Socket = null;
                return Error(ex.Message, -1);
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

        public void WriteSingleCoil(byte station, ushort address, bool value)
        {
            byte[] pdu = new byte[]
            {
                5,
                HighByte(address),
                LowByte(address),
                value ? (byte)0xFF : (byte)0x00,
                0x00
            };

            Execute(station, pdu);
        }

        public Task WriteSingleCoilAsync(byte station, ushort address, bool value)
        {
            byte[] pdu = new byte[]
            {
                5,
                HighByte(address),
                LowByte(address),
                value ? (byte)0xFF : (byte)0x00,
                0x00
            };

            return ExecuteAsync(station, pdu);
        }

        public void WriteMultipleCoils(byte station, ushort address, bool[] values)
        {
            Execute(station, CreateWriteMultipleCoilsPdu(address, values));
        }

        public Task WriteMultipleCoilsAsync(byte station, ushort address, bool[] values)
        {
            return ExecuteAsync(station, CreateWriteMultipleCoilsPdu(address, values));
        }

        public void WriteSingleRegister(byte station, ushort address, byte[] registerBytes)
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

            Execute(station, pdu);
        }

        public Task WriteSingleRegisterAsync(byte station, ushort address, byte[] registerBytes)
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

            return ExecuteAsync(station, pdu);
        }

        public void WriteMultipleRegisters(byte station, ushort address, byte[] registerBytes)
        {
            Execute(station, CreateWriteMultipleRegistersPdu(address, registerBytes));
        }

        public Task WriteMultipleRegistersAsync(byte station, ushort address, byte[] registerBytes)
        {
            return ExecuteAsync(station, CreateWriteMultipleRegistersPdu(address, registerBytes));
        }

        private byte[] ReadBits(byte station, byte functionCode, ushort address, ushort quantity)
        {
            byte[] response = Execute(station, CreateReadPdu(functionCode, address, quantity));
            byte byteCount = response[1];
            byte[] data = new byte[byteCount];
            Buffer.BlockCopy(response, 2, data, 0, byteCount);
            return data;
        }

        private async Task<byte[]> ReadBitsAsync(byte station, byte functionCode, ushort address, ushort quantity)
        {
            byte[] response = await ExecuteAsync(station, CreateReadPdu(functionCode, address, quantity)).ConfigureAwait(false);
            byte byteCount = response[1];
            byte[] data = new byte[byteCount];
            Buffer.BlockCopy(response, 2, data, 0, byteCount);
            return data;
        }

        private byte[] ReadRegisters(byte station, byte functionCode, ushort address, ushort quantity)
        {
            byte[] response = Execute(station, CreateReadPdu(functionCode, address, quantity));
            byte byteCount = response[1];
            byte[] data = new byte[byteCount];
            Buffer.BlockCopy(response, 2, data, 0, byteCount);
            return data;
        }

        private async Task<byte[]> ReadRegistersAsync(byte station, byte functionCode, ushort address, ushort quantity)
        {
            byte[] response = await ExecuteAsync(station, CreateReadPdu(functionCode, address, quantity)).ConfigureAwait(false);
            byte byteCount = response[1];
            byte[] data = new byte[byteCount];
            Buffer.BlockCopy(response, 2, data, 0, byteCount);
            return data;
        }

        private byte[] Execute(byte station, byte[] pdu)
        {
            _communicationLock.Wait();
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
            await _communicationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await ExecuteCoreAsync(station, pdu).ConfigureAwait(false);
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        private byte[] ExecuteCore(byte station, byte[] pdu)
        {
            if (Socket == null || !Socket.Connected)
            {
                throw new InvalidOperationException("Socket is not connected.");
            }

            ushort transactionId = unchecked(++_transactionId);
            byte[] request = CreateRequest(station, transactionId, pdu);

            SendAll(request);
            byte[] header = ReceiveExact(7);
            byte[] body = ReceiveExact(ValidateHeader(station, transactionId, header) - 1);
            ValidateBody(pdu, body);
            return body;
        }

        private async Task<byte[]> ExecuteCoreAsync(byte station, byte[] pdu)
        {
            if (Socket == null || !Socket.Connected)
            {
                throw new InvalidOperationException("Socket is not connected.");
            }

            ushort transactionId = unchecked(++_transactionId);
            byte[] request = CreateRequest(station, transactionId, pdu);

            await SendAllAsync(request).ConfigureAwait(false);
            byte[] header = await ReceiveExactAsync(7).ConfigureAwait(false);
            byte[] body = await ReceiveExactAsync(ValidateHeader(station, transactionId, header) - 1).ConfigureAwait(false);
            ValidateBody(pdu, body);
            return body;
        }

        private Socket CreateSocket()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            if (ReceiveTimeOut > 0)
            {
                socket.ReceiveTimeout = ReceiveTimeOut;
                socket.SendTimeout = ReceiveTimeOut;
            }

            return socket;
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
                throw new InvalidOperationException("Modbus transaction id mismatch.");
            }

            if (protocolId != 0)
            {
                throw new InvalidOperationException("Invalid Modbus protocol id.");
            }

            if (responseStation != station)
            {
                throw new InvalidOperationException("Modbus station mismatch.");
            }

            if (responseLength <= 1)
            {
                throw new InvalidOperationException("Invalid Modbus response length.");
            }

            return responseLength;
        }

        private static void ValidateBody(byte[] pdu, byte[] body)
        {
            if (body.Length == 0)
            {
                throw new InvalidOperationException("Empty Modbus response.");
            }

            if ((body[0] & 0x80) == 0x80)
            {
                int code = body.Length > 1 ? body[1] : 0;
                throw new InvalidOperationException("Modbus exception code: " + code);
            }

            if (body[0] != pdu[0])
            {
                throw new InvalidOperationException("Modbus function code mismatch.");
            }
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
            }
        }

        private async Task SendAllAsync(byte[] buffer)
        {
            int sent = 0;
            while (sent < buffer.Length)
            {
                int count = await Task<int>.Factory.FromAsync(
                    (callback, state) => Socket.BeginSend(buffer, sent, buffer.Length - sent, SocketFlags.None, callback, state),
                    Socket.EndSend,
                    null).ConfigureAwait(false);

                if (count <= 0)
                {
                    throw new SocketException();
                }

                sent += count;
            }
        }

        private byte[] ReceiveExact(int length)
        {
            byte[] buffer = new byte[length];
            int received = 0;
            while (received < length)
            {
                int count = Socket.Receive(buffer, received, length - received, SocketFlags.None);
                if (count <= 0)
                {
                    throw new SocketException();
                }

                received += count;
            }

            return buffer;
        }

        private async Task<byte[]> ReceiveExactAsync(int length)
        {
            byte[] buffer = new byte[length];
            int received = 0;
            while (received < length)
            {
                int count = await Task<int>.Factory.FromAsync(
                    (callback, state) => Socket.BeginReceive(buffer, received, length - received, SocketFlags.None, callback, state),
                    Socket.EndReceive,
                    null).ConfigureAwait(false);

                if (count <= 0)
                {
                    throw new SocketException();
                }

                received += count;
            }

            return buffer;
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
    }
}
