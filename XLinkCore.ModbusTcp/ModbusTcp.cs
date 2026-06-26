using System;
using System.Buffers;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XLinkCore.ModbusTcp
{
    public class ModbusTcp : DeviceTcpCore
    {
        private readonly ModbusTcpNet _modbusTcpNet;
        private readonly object _configurationLock = new object();
        private int _disposed;
        private DataFormat _dataFormat = DataFormat.ABCD;
        private int _station = 1;
        private Encoding _stringEncoding = Encoding.ASCII;
        private bool _addressStartWithZero = true;
        private bool _referenceAddressStartWithZero = true;
        private bool _reverseStringBytes;
        private bool _swapStringBytes;

        public DataFormat DataFormat
        {
            get
            {
                lock (_configurationLock)
                {
                    return _dataFormat;
                }
            }
            set
            {
                lock (_configurationLock)
                {
                    ThrowIfConnectedForConfiguration("DataFormat");
                    _dataFormat = value;
                }
            }
        }

        public int Station
        {
            get
            {
                lock (_configurationLock)
                {
                    return _station;
                }
            }
            set
            {
                lock (_configurationLock)
                {
                    ThrowIfConnectedForConfiguration("Station");
                    _station = value;
                }
            }
        }

        public Encoding StringEncoding
        {
            get
            {
                lock (_configurationLock)
                {
                    return _stringEncoding;
                }
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                lock (_configurationLock)
                {
                    ThrowIfConnectedForConfiguration("StringEncoding");
                    _stringEncoding = value;
                }
            }
        }

        public bool AddressStartWithZero
        {
            get
            {
                lock (_configurationLock)
                {
                    return _addressStartWithZero;
                }
            }
            set
            {
                lock (_configurationLock)
                {
                    ThrowIfConnectedForConfiguration("AddressStartWithZero");
                    _addressStartWithZero = value;
                }
            }
        }

        public bool ReferenceAddressStartWithZero
        {
            get
            {
                lock (_configurationLock)
                {
                    return _referenceAddressStartWithZero;
                }
            }
            set
            {
                lock (_configurationLock)
                {
                    ThrowIfConnectedForConfiguration("ReferenceAddressStartWithZero");
                    _referenceAddressStartWithZero = value;
                }
            }
        }

        public bool ReverseStringBytes
        {
            get
            {
                lock (_configurationLock)
                {
                    return _reverseStringBytes;
                }
            }
            set
            {
                lock (_configurationLock)
                {
                    ThrowIfConnectedForConfiguration("ReverseStringBytes");
                    _reverseStringBytes = value;
                }
            }
        }

        public bool SwapStringBytes
        {
            get
            {
                lock (_configurationLock)
                {
                    return _swapStringBytes;
                }
            }
            set
            {
                lock (_configurationLock)
                {
                    ThrowIfConnectedForConfiguration("SwapStringBytes");
                    _swapStringBytes = value;
                }
            }
        }

        public override bool IsConnected
        {
            get { return _modbusTcpNet.IsConnected; }
        }

        public bool IsBusy
        {
            get { return _modbusTcpNet.IsBusy; }
        }

        public ModbusWriteResponse LastWriteResponse
        {
            get { return _modbusTcpNet.LastWriteResponse; }
        }

        public int LockWaitTimeOut
        {
            get { return _modbusTcpNet.LockWaitTimeOut; }
            set { _modbusTcpNet.LockWaitTimeOut = value; }
        }

        public override int ReceiveTimeOut
        {
            get { return base.ReceiveTimeOut; }
            set
            {
                lock (_configurationLock)
                {
                    base.ReceiveTimeOut = value;
                    _modbusTcpNet.ReceiveTimeOut = value;
                }
            }
        }

        public override int ConnectTimeOut
        {
            get { return base.ConnectTimeOut; }
            set
            {
                lock (_configurationLock)
                {
                    base.ConnectTimeOut = value;
                    _modbusTcpNet.ConnectTimeOut = value;
                }
            }
        }

        protected override Socket Socket
        {
            get { return _modbusTcpNet.Socket; }
        }

        public ModbusTcp(string ip, int port)
        {
            base.IpAddress = ip;
            base.Port = port;

            _modbusTcpNet = new ModbusTcpNet
            {
                IpAddress = ip,
                Port = port
            };
        }

        public override Result Connect()
        {
            return _modbusTcpNet.Connect();
        }

        public override Result Disconnect()
        {
            return _modbusTcpNet.Disconnect();
        }

        public override Task<Result> ConnectAsync()
        {
            return _modbusTcpNet.ConnectAsync();
        }

        public override Task<Result> DisconnectAsync()
        {
            return _modbusTcpNet.DisconnectAsync();
        }

        public override Result<T> Read<T>(string point) where T : struct
        {
            try
            {
                ConfigurationSnapshot configuration = GetConfigurationSnapshot();
                AddressInfo address = ParseAddress(point, typeof(T), configuration);
                object value = ReadValue(typeof(T), address, 1, configuration);
                return Success((T)value);
            }
            catch (Exception ex)
            {
                return Error<T>(ex);
            }
        }

        public override Result<T[]> ReadArray<T>(string point, ushort length) where T : struct
        {
            try
            {
                Type arrayType = typeof(T[]);
                ConfigurationSnapshot configuration = GetConfigurationSnapshot();
                AddressInfo address = ParseAddress(point, arrayType, configuration);
                object value = ReadValue(arrayType, address, length, configuration);
                return Success((T[])value);
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
                ConfigurationSnapshot configuration = GetConfigurationSnapshot();
                AddressInfo address = ParseAddress(point, typeof(string), configuration);
                object value = ReadValue(typeof(string), address, length, configuration);
                return Success((string)value);
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
                ConfigurationSnapshot configuration = GetConfigurationSnapshot();
                AddressInfo address = ParseAddress(point, typeof(T), configuration);
                WriteValue(address, value, typeof(T), configuration);
                return Success();
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        public Result Write(string point, bool[] value)
        {
            return WriteArray(point, value);
        }

        public Result Write(string point, byte[] value)
        {
            return WriteArray(point, value);
        }

        public Result Write(string point, short[] value)
        {
            return WriteArray(point, value);
        }

        public Result Write(string point, ushort[] value)
        {
            return WriteArray(point, value);
        }

        public Result Write(string point, int[] value)
        {
            return WriteArray(point, value);
        }

        public Result Write(string point, uint[] value)
        {
            return WriteArray(point, value);
        }

        public Result Write(string point, long[] value)
        {
            return WriteArray(point, value);
        }

        public Result Write(string point, ulong[] value)
        {
            return WriteArray(point, value);
        }

        public Result Write(string point, float[] value)
        {
            return WriteArray(point, value);
        }

        public Result Write(string point, double[] value)
        {
            return WriteArray(point, value);
        }

        public Result Write(string point, string[] value)
        {
            return WriteArray(point, value);
        }

        public override async Task<Result<T>> ReadAsync<T>(string point) where T : struct
        {
            try
            {
                ConfigurationSnapshot configuration = GetConfigurationSnapshot();
                AddressInfo address = ParseAddress(point, typeof(T), configuration);
                object value = await ReadValueAsync(typeof(T), address, 1, configuration).ConfigureAwait(false);
                return Success((T)value);
            }
            catch (Exception ex)
            {
                return Error<T>(ex);
            }
        }

        public override async Task<Result<T[]>> ReadArrayAsync<T>(string point, ushort length) where T : struct
        {
            try
            {
                Type arrayType = typeof(T[]);
                ConfigurationSnapshot configuration = GetConfigurationSnapshot();
                AddressInfo address = ParseAddress(point, arrayType, configuration);
                object value = await ReadValueAsync(arrayType, address, length, configuration).ConfigureAwait(false);
                return Success((T[])value);
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
                ConfigurationSnapshot configuration = GetConfigurationSnapshot();
                AddressInfo address = ParseAddress(point, typeof(string), configuration);
                object value = await ReadValueAsync(typeof(string), address, length, configuration).ConfigureAwait(false);
                return Success((string)value);
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
                ConfigurationSnapshot configuration = GetConfigurationSnapshot();
                AddressInfo address = ParseAddress(point, typeof(T), configuration);
                await WriteValueAsync(address, value, typeof(T), configuration).ConfigureAwait(false);
                return Success();
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        public override void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _modbusTcpNet.Dispose();
        }

        internal byte[] OriginalBytes(byte[] bytes)
        {
            return _modbusTcpNet.OriginalBytes(bytes);
        }

        public ModbusTcpStatistics GetStatistics()
        {
            return _modbusTcpNet.GetStatistics();
        }

        public void ResetStatistics()
        {
            _modbusTcpNet.ResetStatistics();
        }

        private ConfigurationSnapshot GetConfigurationSnapshot()
        {
            lock (_configurationLock)
            {
                return new ConfigurationSnapshot(
                    _dataFormat,
                    _station,
                    _stringEncoding,
                    _addressStartWithZero,
                    _referenceAddressStartWithZero,
                    _reverseStringBytes,
                    _swapStringBytes);
            }
        }

        private void ThrowIfConnectedForConfiguration(string propertyName)
        {
            if (_modbusTcpNet.IsConnected)
            {
                throw new InvalidOperationException(propertyName + " cannot be changed while connected.");
            }
        }

        private Result WriteArray<T>(string point, T[] value)
        {
            return Write<T[]>(point, value);
        }

        private object ReadValue(Type targetType, AddressInfo address, ushort length, ConfigurationSnapshot configuration)
        {
            Type elementType = GetElementType(targetType);
            bool isArray = targetType.IsArray;

            if (elementType == typeof(bool))
            {
                int count = isArray ? length : 1;
                byte[] bitBytes = ReadBits(address, checked((ushort)count), configuration);
                bool[] values = BitsToBooleans(bitBytes, count);
                return isArray ? (object)values : values[0];
            }

            if (elementType == typeof(string))
            {
                ushort registerCount = length == 0 ? (ushort)1 : length;
                byte[] bytes = ReadRegisterBytes(address, registerCount, configuration);
                if (isArray)
                {
                    string[] values = new string[registerCount];
                    for (int i = 0; i < registerCount; i++)
                    {
                        values[i] = DecodeString(bytes, i * 2, 2, configuration);
                    }

                    return values;
                }

                return DecodeString(bytes, configuration);
            }

            int elementSize = SizeOf(elementType);
            int elementCount = isArray ? length : 1;
            int byteCount = checked(elementSize * elementCount);
            ushort registers = checked((ushort)((byteCount + 1) / 2));
            byte[] registerBytes = ReadRegisterBytes(address, registers, configuration);

            if (isArray)
            {
                Array array = Array.CreateInstance(elementType, elementCount);
                for (int i = 0; i < elementCount; i++)
                {
                    array.SetValue(BytesToValue(registerBytes, i * elementSize, elementSize, elementType, configuration), i);
                }

                return array;
            }

            return BytesToValue(registerBytes, 0, elementSize, elementType, configuration);
        }

        private async Task<object> ReadValueAsync(Type targetType, AddressInfo address, ushort length, ConfigurationSnapshot configuration)
        {
            Type elementType = GetElementType(targetType);
            bool isArray = targetType.IsArray;

            if (elementType == typeof(bool))
            {
                int count = isArray ? length : 1;
                byte[] bitBytes = await ReadBitsAsync(address, checked((ushort)count), configuration).ConfigureAwait(false);
                bool[] values = BitsToBooleans(bitBytes, count);
                return isArray ? (object)values : values[0];
            }

            if (elementType == typeof(string))
            {
                ushort registerCount = length == 0 ? (ushort)1 : length;
                byte[] bytes = await ReadRegisterBytesAsync(address, registerCount, configuration).ConfigureAwait(false);
                if (isArray)
                {
                    string[] values = new string[registerCount];
                    for (int i = 0; i < registerCount; i++)
                    {
                        values[i] = DecodeString(bytes, i * 2, 2, configuration);
                    }

                    return values;
                }

                return DecodeString(bytes, configuration);
            }

            int elementSize = SizeOf(elementType);
            int elementCount = isArray ? length : 1;
            int byteCount = checked(elementSize * elementCount);
            ushort registers = checked((ushort)((byteCount + 1) / 2));
            byte[] registerBytes = await ReadRegisterBytesAsync(address, registers, configuration).ConfigureAwait(false);

            if (isArray)
            {
                Array array = Array.CreateInstance(elementType, elementCount);
                for (int i = 0; i < elementCount; i++)
                {
                    array.SetValue(BytesToValue(registerBytes, i * elementSize, elementSize, elementType, configuration), i);
                }

                return array;
            }

            return BytesToValue(registerBytes, 0, elementSize, elementType, configuration);
        }

        private void WriteValue<T>(AddressInfo address, T value, Type targetType, ConfigurationSnapshot configuration)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            Type elementType = GetElementType(targetType);

            if (elementType == typeof(bool))
            {
                if (address.Area != RegisterArea.Coil)
                {
                    throw new NotSupportedException("Boolean writes only support coil addresses.");
                }

                if (targetType.IsArray)
                {
                    bool[] values = (bool[])(object)value;
                    _modbusTcpNet.WriteMultipleCoils(GetStation(configuration), address.Address, values);
                }
                else
                {
                    _modbusTcpNet.WriteSingleCoil(GetStation(configuration), address.Address, (bool)(object)value);
                }

                return;
            }

            byte[] bytes;
            if (elementType == typeof(string))
            {
                if (targetType.IsArray)
                {
                    string[] values = (string[])(object)value;
                    string text = string.Concat(values);
                    bytes = EncodeString(text, configuration);
                }
                else
                {
                    bytes = EncodeString((string)(object)value, configuration);
                }
            }
            else if (targetType.IsArray)
            {
                Array array = (Array)(object)value;
                int elementSize = SizeOf(elementType);
                bytes = new byte[array.Length * elementSize];
                for (int i = 0; i < array.Length; i++)
                {
                    WriteValueBytes(array.GetValue(i), elementType, configuration, bytes, i * elementSize);
                }
            }
            else
            {
                bytes = ValueToBytes(value, elementType, configuration);
            }

            if (bytes.Length % 2 != 0)
            {
                byte[] padded = new byte[bytes.Length + 1];
                Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);
                bytes = padded;
            }

            if (address.Area != RegisterArea.HoldingRegister)
            {
                throw new NotSupportedException("Register writes only support holding register addresses.");
            }

            if (bytes.Length == 2)
            {
                _modbusTcpNet.WriteSingleRegister(GetStation(configuration), address.Address, bytes);
            }
            else
            {
                _modbusTcpNet.WriteMultipleRegisters(GetStation(configuration), address.Address, bytes);
            }
        }

        private async Task WriteValueAsync<T>(AddressInfo address, T value, Type targetType, ConfigurationSnapshot configuration)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            Type elementType = GetElementType(targetType);

            if (elementType == typeof(bool))
            {
                if (address.Area != RegisterArea.Coil)
                {
                    throw new NotSupportedException("Boolean writes only support coil addresses.");
                }

                if (targetType.IsArray)
                {
                    bool[] values = (bool[])(object)value;
                    await _modbusTcpNet.WriteMultipleCoilsAsync(GetStation(configuration), address.Address, values).ConfigureAwait(false);
                }
                else
                {
                    await _modbusTcpNet.WriteSingleCoilAsync(GetStation(configuration), address.Address, (bool)(object)value).ConfigureAwait(false);
                }

                return;
            }

            byte[] bytes;
            if (elementType == typeof(string))
            {
                if (targetType.IsArray)
                {
                    string[] values = (string[])(object)value;
                    string text = string.Concat(values);
                bytes = EncodeString(text, configuration);
                }
                else
                {
                    bytes = EncodeString((string)(object)value, configuration);
                }
            }
            else if (targetType.IsArray)
            {
                Array array = (Array)(object)value;
                int elementSize = SizeOf(elementType);
                bytes = new byte[array.Length * elementSize];
                for (int i = 0; i < array.Length; i++)
                {
                    WriteValueBytes(array.GetValue(i), elementType, configuration, bytes, i * elementSize);
                }
            }
            else
            {
                bytes = ValueToBytes(value, elementType, configuration);
            }

            if (bytes.Length % 2 != 0)
            {
                byte[] padded = new byte[bytes.Length + 1];
                Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);
                bytes = padded;
            }

            if (address.Area != RegisterArea.HoldingRegister)
            {
                throw new NotSupportedException("Register writes only support holding register addresses.");
            }

            if (bytes.Length == 2)
            {
                await _modbusTcpNet.WriteSingleRegisterAsync(GetStation(configuration), address.Address, bytes).ConfigureAwait(false);
            }
            else
            {
                await _modbusTcpNet.WriteMultipleRegistersAsync(GetStation(configuration), address.Address, bytes).ConfigureAwait(false);
            }
        }

        private byte[] ReadBits(AddressInfo address, ushort count, ConfigurationSnapshot configuration)
        {
            switch (address.Area)
            {
                case RegisterArea.DiscreteInput:
                    return _modbusTcpNet.ReadDiscreteInputs(GetStation(configuration), address.Address, count);
                case RegisterArea.Coil:
                    return _modbusTcpNet.ReadCoils(GetStation(configuration), address.Address, count);
                default:
                    throw new NotSupportedException("Boolean reads only support coil or discrete input addresses.");
            }
        }

        private Task<byte[]> ReadBitsAsync(AddressInfo address, ushort count, ConfigurationSnapshot configuration)
        {
            switch (address.Area)
            {
                case RegisterArea.DiscreteInput:
                    return _modbusTcpNet.ReadDiscreteInputsAsync(GetStation(configuration), address.Address, count);
                case RegisterArea.Coil:
                    return _modbusTcpNet.ReadCoilsAsync(GetStation(configuration), address.Address, count);
                default:
                    throw new NotSupportedException("Boolean reads only support coil or discrete input addresses.");
            }
        }

        private byte[] ReadRegisterBytes(AddressInfo address, ushort registers, ConfigurationSnapshot configuration)
        {
            switch (address.Area)
            {
                case RegisterArea.InputRegister:
                    return _modbusTcpNet.ReadInputRegisters(GetStation(configuration), address.Address, registers);
                case RegisterArea.HoldingRegister:
                    return _modbusTcpNet.ReadHoldingRegisters(GetStation(configuration), address.Address, registers);
                default:
                    throw new NotSupportedException("Register reads only support holding register or input register addresses.");
            }
        }

        private Task<byte[]> ReadRegisterBytesAsync(AddressInfo address, ushort registers, ConfigurationSnapshot configuration)
        {
            switch (address.Area)
            {
                case RegisterArea.InputRegister:
                    return _modbusTcpNet.ReadInputRegistersAsync(GetStation(configuration), address.Address, registers);
                case RegisterArea.HoldingRegister:
                    return _modbusTcpNet.ReadHoldingRegistersAsync(GetStation(configuration), address.Address, registers);
                default:
                    throw new NotSupportedException("Register reads only support holding register or input register addresses.");
            }
        }

        private string DecodeString(byte[] bytes, ConfigurationSnapshot configuration)
        {
            return DecodeString(bytes, 0, bytes.Length, configuration);
        }

        private string DecodeString(byte[] bytes, int offset, int count, ConfigurationSnapshot configuration)
        {
            byte[] stringBytes = ApplyStringFormat(bytes, offset, count, configuration);
            try
            {
                return configuration.StringEncoding.GetString(stringBytes, 0, count).TrimEnd('\0');
            }
            finally
            {
                ReturnArray(stringBytes);
            }
        }

        private byte[] EncodeString(string value, ConfigurationSnapshot configuration)
        {
            byte[] bytes = configuration.StringEncoding.GetBytes(value);
            int byteCount = bytes.Length;
            if (bytes.Length % 2 != 0)
            {
                byte[] padded = new byte[bytes.Length + 1];
                Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);
                bytes = padded;
                byteCount = bytes.Length;
            }

            byte[] formatted = ApplyStringFormat(bytes, 0, byteCount, configuration);
            try
            {
                return CopyExact(formatted, 0, byteCount);
            }
            finally
            {
                ReturnArray(formatted);
            }
        }

        private byte[] ApplyStringFormat(byte[] bytes, int offset, int count, ConfigurationSnapshot configuration)
        {
            byte[] result = RentArray(count);
            Buffer.BlockCopy(bytes, offset, result, 0, count);

            if (configuration.SwapStringBytes)
            {
                SwapEveryTwoBytes(result, count);
            }

            if (configuration.ReverseStringBytes)
            {
                Array.Reverse(result, 0, count);
            }

            return result;
        }

        private static bool[] BitsToBooleans(byte[] bytes, int count)
        {
            int requiredBytes = (count + 7) / 8;
            if (bytes == null || bytes.Length < requiredBytes)
            {
                throw new ModbusTcpProtocolException("Modbus bit response data is shorter than requested count.");
            }

            bool[] values = new bool[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
            }

            return values;
        }

        private object BytesToValue(byte[] bytes, int offset, int count, Type type, ConfigurationSnapshot configuration)
        {
            byte[] valueBytes = ApplyDataFormat(bytes, offset, count, configuration.DataFormat);
            try
            {
                if (type == typeof(byte))
                {
                    return valueBytes[0];
                }

                if (type == typeof(short))
                {
                    return BitConverter.ToInt16(valueBytes, 0);
                }

                if (type == typeof(ushort))
                {
                    return BitConverter.ToUInt16(valueBytes, 0);
                }

                if (type == typeof(int))
                {
                    return BitConverter.ToInt32(valueBytes, 0);
                }

                if (type == typeof(uint))
                {
                    return BitConverter.ToUInt32(valueBytes, 0);
                }

                if (type == typeof(long))
                {
                    return BitConverter.ToInt64(valueBytes, 0);
                }

                if (type == typeof(ulong))
                {
                    return BitConverter.ToUInt64(valueBytes, 0);
                }

                if (type == typeof(float))
                {
                    return BitConverter.ToSingle(valueBytes, 0);
                }

                if (type == typeof(double))
                {
                    return BitConverter.ToDouble(valueBytes, 0);
                }

                throw new NotSupportedException("Unsupported read type: " + type.FullName);
            }
            finally
            {
                ReturnArray(valueBytes);
            }
        }

        private byte[] ValueToBytes(object value, Type type, ConfigurationSnapshot configuration)
        {
            byte[] rented = RentValueBytes(value, type, configuration);
            try
            {
                return CopyExact(rented, 0, SizeOf(type));
            }
            finally
            {
                ReturnArray(rented);
            }
        }

        private byte[] RentValueBytes(object value, Type type, ConfigurationSnapshot configuration)
        {
            byte[] bytes;

            if (type == typeof(byte))
            {
                bytes = new byte[] { (byte)value };
            }
            else if (type == typeof(short))
            {
                bytes = BitConverter.GetBytes((short)value);
            }
            else if (type == typeof(ushort))
            {
                bytes = BitConverter.GetBytes((ushort)value);
            }
            else if (type == typeof(int))
            {
                bytes = BitConverter.GetBytes((int)value);
            }
            else if (type == typeof(uint))
            {
                bytes = BitConverter.GetBytes((uint)value);
            }
            else if (type == typeof(long))
            {
                bytes = BitConverter.GetBytes((long)value);
            }
            else if (type == typeof(ulong))
            {
                bytes = BitConverter.GetBytes((ulong)value);
            }
            else if (type == typeof(float))
            {
                bytes = BitConverter.GetBytes((float)value);
            }
            else if (type == typeof(double))
            {
                bytes = BitConverter.GetBytes((double)value);
            }
            else
            {
                throw new NotSupportedException("Unsupported write type: " + type.FullName);
            }

            return ApplyDataFormat(bytes, 0, bytes.Length, configuration.DataFormat);
        }

        private void WriteValueBytes(object value, Type type, ConfigurationSnapshot configuration, byte[] target, int targetOffset)
        {
            byte[] valueBytes = RentValueBytes(value, type, configuration);
            try
            {
                Buffer.BlockCopy(valueBytes, 0, target, targetOffset, SizeOf(type));
            }
            finally
            {
                ReturnArray(valueBytes);
            }
        }

        private byte[] ApplyDataFormat(byte[] bytes, int offset, int count, DataFormat dataFormat)
        {
            byte[] result = RentArray(count);
            Buffer.BlockCopy(bytes, offset, result, 0, count);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(result, 0, count);
            }

            if (count <= 2)
            {
                if (dataFormat == DataFormat.BADC || dataFormat == DataFormat.DCBA)
                {
                    SwapEveryTwoBytes(result, count);
                }

                return result;
            }

            switch (dataFormat)
            {
                case DataFormat.ABCD:
                    break;
                case DataFormat.BADC:
                    SwapEveryTwoBytes(result, count);
                    break;
                case DataFormat.CDAB:
                    SwapWords(result, count);
                    break;
                case DataFormat.DCBA:
                    Array.Reverse(result, 0, count);
                    break;
                default:
                    throw new NotSupportedException("Unsupported data format: " + dataFormat);
            }

            return result;
        }

        private static void SwapEveryTwoBytes(byte[] bytes)
        {
            SwapEveryTwoBytes(bytes, bytes.Length);
        }

        private static void SwapEveryTwoBytes(byte[] bytes, int count)
        {
            for (int i = 0; i + 1 < count; i += 2)
            {
                byte temp = bytes[i];
                bytes[i] = bytes[i + 1];
                bytes[i + 1] = temp;
            }
        }

        private static void SwapWords(byte[] bytes)
        {
            SwapWords(bytes, bytes.Length);
        }

        private static void SwapWords(byte[] bytes, int count)
        {
            if (count % 2 != 0)
            {
                return;
            }

            byte[] copy = RentArray(count);
            Buffer.BlockCopy(bytes, 0, copy, 0, count);
            try
            {
                int wordCount = count / 2;
                for (int i = 0; i < wordCount; i++)
                {
                    int source = i * 2;
                    int target = ((i + wordCount / 2) % wordCount) * 2;
                    bytes[target] = copy[source];
                    bytes[target + 1] = copy[source + 1];
                }
            }
            finally
            {
                ReturnArray(copy);
            }
        }

        private static byte[] RentArray(int minimumLength)
        {
            return ArrayPool<byte>.Shared.Rent(minimumLength);
        }

        private static byte[] CopyExact(byte[] source, int offset, int count)
        {
            byte[] target = new byte[count];
            Buffer.BlockCopy(source, offset, target, 0, count);
            return target;
        }

        private static void ReturnArray(byte[] bytes)
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }

        private static Type GetElementType(Type type)
        {
            return type.IsArray ? type.GetElementType() : type;
        }

        private static int SizeOf(Type type)
        {
            if (type == typeof(byte))
            {
                return 1;
            }

            if (type == typeof(short) || type == typeof(ushort))
            {
                return 2;
            }

            if (type == typeof(int) || type == typeof(uint) || type == typeof(float))
            {
                return 4;
            }

            if (type == typeof(long) || type == typeof(ulong) || type == typeof(double))
            {
                return 8;
            }

            throw new NotSupportedException("Unsupported type: " + type.FullName);
        }

        private AddressInfo ParseAddress(string point, Type targetType, ConfigurationSnapshot configuration)
        {
            if (string.IsNullOrWhiteSpace(point))
            {
                throw new ArgumentException("Point cannot be empty.", "point");
            }

            string text = point.Trim();
            int separatorIndex = text.IndexOf(':');
            if (separatorIndex >= 0)
            {
                string prefix = text.Substring(0, separatorIndex).Trim().ToLowerInvariant();
                ushort address = NormalizeAddress(ParseUInt16Address(text.Substring(separatorIndex + 1)), configuration);
                return new AddressInfo(ParseArea(prefix, targetType), address);
            }

            int reference;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out reference))
            {
                if (reference >= 40001 && reference <= 49999)
                {
                    return new AddressInfo(RegisterArea.HoldingRegister, ParseReferenceAddress(reference, 40000, configuration));
                }

                if (reference >= 30001 && reference <= 39999)
                {
                    return new AddressInfo(RegisterArea.InputRegister, ParseReferenceAddress(reference, 30000, configuration));
                }

                if (reference >= 10001 && reference <= 19999)
                {
                    return new AddressInfo(RegisterArea.DiscreteInput, ParseReferenceAddress(reference, 10000, configuration));
                }

                if (reference >= 1 && reference <= 9999)
                {
                    Type elementType = GetElementType(targetType);
                    RegisterArea area = elementType == typeof(bool)
                        ? RegisterArea.Coil
                        : RegisterArea.HoldingRegister;
                    return new AddressInfo(area, NormalizeAddress(checked((ushort)reference), configuration));
                }

                if (reference >= 0 && reference <= ushort.MaxValue)
                {
                    Type elementType = GetElementType(targetType);
                    RegisterArea area = elementType == typeof(bool)
                        ? RegisterArea.Coil
                        : RegisterArea.HoldingRegister;
                    return new AddressInfo(area, NormalizeAddress((ushort)reference, configuration));
                }
            }

            throw new FormatException("Unsupported point format: " + point);
        }

        private static ushort ParseUInt16Address(string addressText)
        {
            ushort address;
            if (!ushort.TryParse(addressText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out address))
            {
                throw new FormatException("Invalid Modbus address: " + addressText);
            }

            return address;
        }

        private ushort NormalizeAddress(ushort address, ConfigurationSnapshot configuration)
        {
            if (configuration.AddressStartWithZero || address == 0)
            {
                return address;
            }

            return checked((ushort)(address - 1));
        }

        private ushort ParseReferenceAddress(int reference, int baseAddress, ConfigurationSnapshot configuration)
        {
            int address = reference - baseAddress;
            if (configuration.ReferenceAddressStartWithZero)
            {
                address--;
            }

            if (address < 0 || address > ushort.MaxValue)
            {
                throw new FormatException("Invalid Modbus reference address: " + reference);
            }

            return (ushort)address;
        }

        private static RegisterArea ParseArea(string prefix, Type targetType)
        {
            switch (prefix)
            {
                case "0":
                case "0x":
                case "coil":
                case "coils":
                case "m":
                    return RegisterArea.Coil;
                case "1":
                case "1x":
                case "di":
                case "discrete":
                case "discreteinput":
                    return RegisterArea.DiscreteInput;
                case "3":
                case "3x":
                case "ir":
                case "input":
                case "inputregister":
                    return RegisterArea.InputRegister;
                case "4":
                case "4x":
                case "hr":
                case "holding":
                case "holdingregister":
                case "d":
                    return RegisterArea.HoldingRegister;
                default:
                    Type elementType = GetElementType(targetType);
                    if (elementType == typeof(bool))
                    {
                        return RegisterArea.Coil;
                    }

                    return RegisterArea.HoldingRegister;
            }
        }

        private byte GetStation()
        {
            return GetStation(GetConfigurationSnapshot());
        }

        private static byte GetStation(ConfigurationSnapshot configuration)
        {
            int station = configuration.Station;
            if (station < byte.MinValue || station > byte.MaxValue)
            {
                throw new InvalidOperationException("Station must be between 0 and 255.");
            }

            return (byte)station;
        }

        private static Result Success()
        {
            return new Result
            {
                IsSuccess = true,
                Message = "Success"
            };
        }

        private static Result<T> Success<T>(T data)
        {
            return new Result<T>(data)
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

        private static Result<T> Error<T>(string message, int errorCode)
        {
            return new Result<T>
            {
                IsSuccess = false,
                Message = message,
                ErrorCode = errorCode
            };
        }

        private static Result<T> Error<T>(Exception exception)
        {
            return Error<T>(exception.Message, ModbusTcpErrorCodes.FromException(exception));
        }

        private enum RegisterArea
        {
            Coil,
            DiscreteInput,
            HoldingRegister,
            InputRegister
        }

        private struct ConfigurationSnapshot
        {
            public ConfigurationSnapshot(
                DataFormat dataFormat,
                int station,
                Encoding stringEncoding,
                bool addressStartWithZero,
                bool referenceAddressStartWithZero,
                bool reverseStringBytes,
                bool swapStringBytes)
            {
                DataFormat = dataFormat;
                Station = station;
                StringEncoding = stringEncoding;
                AddressStartWithZero = addressStartWithZero;
                ReferenceAddressStartWithZero = referenceAddressStartWithZero;
                ReverseStringBytes = reverseStringBytes;
                SwapStringBytes = swapStringBytes;
            }

            public DataFormat DataFormat { get; }
            public int Station { get; }
            public Encoding StringEncoding { get; }
            public bool AddressStartWithZero { get; }
            public bool ReferenceAddressStartWithZero { get; }
            public bool ReverseStringBytes { get; }
            public bool SwapStringBytes { get; }
        }

        private struct AddressInfo
        {
            public AddressInfo(RegisterArea area, ushort address)
            {
                Area = area;
                Address = address;
            }

            public RegisterArea Area { get; }
            public ushort Address { get; }
        }
    }
}
