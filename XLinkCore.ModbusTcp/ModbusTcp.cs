using System;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace XLinkCore.ModbusTcp
{
    public class ModbusTcp : DeviceTcpCore
    {
        private readonly ModbusTcpNet _modbusTcpNet;

        public DataFormat DataFormat { get; set; } = DataFormat.ABCD;
        public int Station { get; set; } = 1;
        public Encoding StringEncoding { get; set; } = Encoding.ASCII;
        public bool AddressStartWithZero { get; set; } = true;
        public bool ReferenceAddressStartWithZero { get; set; } = true;
        public bool ReverseStringBytes { get; set; }
        public bool SwapStringBytes { get; set; }

        public override int ReceiveTimeOut
        {
            get { return base.ReceiveTimeOut; }
            set
            {
                base.ReceiveTimeOut = value;
                _modbusTcpNet.ReceiveTimeOut = value;
            }
        }

        public override int ConnectTimeOut
        {
            get { return base.ConnectTimeOut; }
            set
            {
                base.ConnectTimeOut = value;
                _modbusTcpNet.ConnectTimeOut = value;
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
                AddressInfo address = ParseAddress(point, typeof(T));
                object value = ReadValue(typeof(T), address, 1);
                return Success((T)value);
            }
            catch (Exception ex)
            {
                return Error<T>(ex.Message, -1);
            }
        }

        public override Result<T[]> ReadArray<T>(string point, ushort length) where T : struct
        {
            try
            {
                Type arrayType = typeof(T[]);
                AddressInfo address = ParseAddress(point, arrayType);
                object value = ReadValue(arrayType, address, length);
                return Success((T[])value);
            }
            catch (Exception ex)
            {
                return Error<T[]>(ex.Message, -1);
            }
        }

        public override Result<string> ReadString(string point, ushort length)
        {
            try
            {
                AddressInfo address = ParseAddress(point, typeof(string));
                object value = ReadValue(typeof(string), address, length);
                return Success((string)value);
            }
            catch (Exception ex)
            {
                return Error<string>(ex.Message, -1);
            }
        }

        public override Result<string[]> ReadStringArray(string point, ushort length)
        {
            try
            {
                AddressInfo address = ParseAddress(point, typeof(string[]));
                object value = ReadValue(typeof(string[]), address, length);
                return Success((string[])value);
            }
            catch (Exception ex)
            {
                return Error<string[]>(ex.Message, -1);
            }
        }

        public override Result Write<T>(string point, T value)
        {
            try
            {
                AddressInfo address = ParseAddress(point, typeof(T));
                WriteValue(address, value, typeof(T));
                return Success();
            }
            catch (Exception ex)
            {
                return Error(ex.Message, -1);
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
                AddressInfo address = ParseAddress(point, typeof(T));
                object value = await ReadValueAsync(typeof(T), address, 1).ConfigureAwait(false);
                return Success((T)value);
            }
            catch (Exception ex)
            {
                return Error<T>(ex.Message, -1);
            }
        }

        public override async Task<Result<T[]>> ReadArrayAsync<T>(string point, ushort length) where T : struct
        {
            try
            {
                Type arrayType = typeof(T[]);
                AddressInfo address = ParseAddress(point, arrayType);
                object value = await ReadValueAsync(arrayType, address, length).ConfigureAwait(false);
                return Success((T[])value);
            }
            catch (Exception ex)
            {
                return Error<T[]>(ex.Message, -1);
            }
        }

        public override async Task<Result<string>> ReadStringAsync(string point, ushort length)
        {
            try
            {
                AddressInfo address = ParseAddress(point, typeof(string));
                object value = await ReadValueAsync(typeof(string), address, length).ConfigureAwait(false);
                return Success((string)value);
            }
            catch (Exception ex)
            {
                return Error<string>(ex.Message, -1);
            }
        }

        public override async Task<Result<string[]>> ReadStringArrayAsync(string point, ushort length)
        {
            try
            {
                AddressInfo address = ParseAddress(point, typeof(string[]));
                object value = await ReadValueAsync(typeof(string[]), address, length).ConfigureAwait(false);
                return Success((string[])value);
            }
            catch (Exception ex)
            {
                return Error<string[]>(ex.Message, -1);
            }
        }

        public override async Task<Result> WriteAsync<T>(string point, T value)
        {
            try
            {
                AddressInfo address = ParseAddress(point, typeof(T));
                await WriteValueAsync(address, value, typeof(T)).ConfigureAwait(false);
                return Success();
            }
            catch (Exception ex)
            {
                return Error(ex.Message, -1);
            }
        }

        public override void Dispose()
        {
            _modbusTcpNet.Dispose();
        }

        private Result WriteArray<T>(string point, T[] value)
        {
            return Write<T[]>(point, value);
        }

        private object ReadValue(Type targetType, AddressInfo address, ushort length)
        {
            Type elementType = GetElementType(targetType);
            bool isArray = targetType.IsArray;

            if (elementType == typeof(bool))
            {
                int count = isArray ? length : 1;
                byte[] bitBytes = ReadBits(address, checked((ushort)count));
                bool[] values = BitsToBooleans(bitBytes, count);
                return isArray ? (object)values : values[0];
            }

            if (elementType == typeof(string))
            {
                ushort registerCount = length == 0 ? (ushort)1 : length;
                byte[] bytes = ReadRegisterBytes(address, registerCount);
                if (isArray)
                {
                    string[] values = new string[registerCount];
                    for (int i = 0; i < registerCount; i++)
                    {
                        byte[] itemBytes = new byte[2];
                        Buffer.BlockCopy(bytes, i * 2, itemBytes, 0, 2);
                        values[i] = DecodeString(itemBytes);
                    }

                    return values;
                }

                return DecodeString(bytes);
            }

            int elementSize = SizeOf(elementType);
            int elementCount = isArray ? length : 1;
            int byteCount = checked(elementSize * elementCount);
            ushort registers = checked((ushort)((byteCount + 1) / 2));
            byte[] registerBytes = ReadRegisterBytes(address, registers);

            if (registerBytes.Length != byteCount)
            {
                byte[] trimmed = new byte[byteCount];
                Buffer.BlockCopy(registerBytes, 0, trimmed, 0, byteCount);
                registerBytes = trimmed;
            }

            if (isArray)
            {
                Array array = Array.CreateInstance(elementType, elementCount);
                for (int i = 0; i < elementCount; i++)
                {
                    byte[] itemBytes = new byte[elementSize];
                    Buffer.BlockCopy(registerBytes, i * elementSize, itemBytes, 0, elementSize);
                    array.SetValue(BytesToValue(itemBytes, elementType), i);
                }

                return array;
            }

            return BytesToValue(registerBytes, elementType);
        }

        private async Task<object> ReadValueAsync(Type targetType, AddressInfo address, ushort length)
        {
            Type elementType = GetElementType(targetType);
            bool isArray = targetType.IsArray;

            if (elementType == typeof(bool))
            {
                int count = isArray ? length : 1;
                byte[] bitBytes = await ReadBitsAsync(address, checked((ushort)count)).ConfigureAwait(false);
                bool[] values = BitsToBooleans(bitBytes, count);
                return isArray ? (object)values : values[0];
            }

            if (elementType == typeof(string))
            {
                ushort registerCount = length == 0 ? (ushort)1 : length;
                byte[] bytes = await ReadRegisterBytesAsync(address, registerCount).ConfigureAwait(false);
                if (isArray)
                {
                    string[] values = new string[registerCount];
                    for (int i = 0; i < registerCount; i++)
                    {
                        byte[] itemBytes = new byte[2];
                        Buffer.BlockCopy(bytes, i * 2, itemBytes, 0, 2);
                        values[i] = DecodeString(itemBytes);
                    }

                    return values;
                }

                return DecodeString(bytes);
            }

            int elementSize = SizeOf(elementType);
            int elementCount = isArray ? length : 1;
            int byteCount = checked(elementSize * elementCount);
            ushort registers = checked((ushort)((byteCount + 1) / 2));
            byte[] registerBytes = await ReadRegisterBytesAsync(address, registers).ConfigureAwait(false);

            if (registerBytes.Length != byteCount)
            {
                byte[] trimmed = new byte[byteCount];
                Buffer.BlockCopy(registerBytes, 0, trimmed, 0, byteCount);
                registerBytes = trimmed;
            }

            if (isArray)
            {
                Array array = Array.CreateInstance(elementType, elementCount);
                for (int i = 0; i < elementCount; i++)
                {
                    byte[] itemBytes = new byte[elementSize];
                    Buffer.BlockCopy(registerBytes, i * elementSize, itemBytes, 0, elementSize);
                    array.SetValue(BytesToValue(itemBytes, elementType), i);
                }

                return array;
            }

            return BytesToValue(registerBytes, elementType);
        }

        private void WriteValue<T>(AddressInfo address, T value, Type targetType)
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
                    _modbusTcpNet.WriteMultipleCoils(GetStation(), address.Address, values);
                }
                else
                {
                    _modbusTcpNet.WriteSingleCoil(GetStation(), address.Address, (bool)(object)value);
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
                    bytes = EncodeString(text);
                }
                else
                {
                    bytes = EncodeString((string)(object)value);
                }
            }
            else if (targetType.IsArray)
            {
                Array array = (Array)(object)value;
                int elementSize = SizeOf(elementType);
                bytes = new byte[array.Length * elementSize];
                for (int i = 0; i < array.Length; i++)
                {
                    byte[] itemBytes = ValueToBytes(array.GetValue(i), elementType);
                    Buffer.BlockCopy(itemBytes, 0, bytes, i * elementSize, elementSize);
                }
            }
            else
            {
                bytes = ValueToBytes(value, elementType);
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
                _modbusTcpNet.WriteSingleRegister(GetStation(), address.Address, bytes);
            }
            else
            {
                _modbusTcpNet.WriteMultipleRegisters(GetStation(), address.Address, bytes);
            }
        }

        private async Task WriteValueAsync<T>(AddressInfo address, T value, Type targetType)
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
                    await _modbusTcpNet.WriteMultipleCoilsAsync(GetStation(), address.Address, values).ConfigureAwait(false);
                }
                else
                {
                    await _modbusTcpNet.WriteSingleCoilAsync(GetStation(), address.Address, (bool)(object)value).ConfigureAwait(false);
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
                    bytes = EncodeString(text);
                }
                else
                {
                    bytes = EncodeString((string)(object)value);
                }
            }
            else if (targetType.IsArray)
            {
                Array array = (Array)(object)value;
                int elementSize = SizeOf(elementType);
                bytes = new byte[array.Length * elementSize];
                for (int i = 0; i < array.Length; i++)
                {
                    byte[] itemBytes = ValueToBytes(array.GetValue(i), elementType);
                    Buffer.BlockCopy(itemBytes, 0, bytes, i * elementSize, elementSize);
                }
            }
            else
            {
                bytes = ValueToBytes(value, elementType);
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
                await _modbusTcpNet.WriteSingleRegisterAsync(GetStation(), address.Address, bytes).ConfigureAwait(false);
            }
            else
            {
                await _modbusTcpNet.WriteMultipleRegistersAsync(GetStation(), address.Address, bytes).ConfigureAwait(false);
            }
        }

        private byte[] ReadBits(AddressInfo address, ushort count)
        {
            switch (address.Area)
            {
                case RegisterArea.DiscreteInput:
                    return _modbusTcpNet.ReadDiscreteInputs(GetStation(), address.Address, count);
                case RegisterArea.Coil:
                    return _modbusTcpNet.ReadCoils(GetStation(), address.Address, count);
                default:
                    throw new NotSupportedException("Boolean reads only support coil or discrete input addresses.");
            }
        }

        private Task<byte[]> ReadBitsAsync(AddressInfo address, ushort count)
        {
            switch (address.Area)
            {
                case RegisterArea.DiscreteInput:
                    return _modbusTcpNet.ReadDiscreteInputsAsync(GetStation(), address.Address, count);
                case RegisterArea.Coil:
                    return _modbusTcpNet.ReadCoilsAsync(GetStation(), address.Address, count);
                default:
                    throw new NotSupportedException("Boolean reads only support coil or discrete input addresses.");
            }
        }

        private byte[] ReadRegisterBytes(AddressInfo address, ushort registers)
        {
            switch (address.Area)
            {
                case RegisterArea.InputRegister:
                    return _modbusTcpNet.ReadInputRegisters(GetStation(), address.Address, registers);
                case RegisterArea.HoldingRegister:
                    return _modbusTcpNet.ReadHoldingRegisters(GetStation(), address.Address, registers);
                default:
                    throw new NotSupportedException("Register reads only support holding register or input register addresses.");
            }
        }

        private Task<byte[]> ReadRegisterBytesAsync(AddressInfo address, ushort registers)
        {
            switch (address.Area)
            {
                case RegisterArea.InputRegister:
                    return _modbusTcpNet.ReadInputRegistersAsync(GetStation(), address.Address, registers);
                case RegisterArea.HoldingRegister:
                    return _modbusTcpNet.ReadHoldingRegistersAsync(GetStation(), address.Address, registers);
                default:
                    throw new NotSupportedException("Register reads only support holding register or input register addresses.");
            }
        }

        private string DecodeString(byte[] bytes)
        {
            byte[] stringBytes = ApplyStringFormat(bytes);
            return StringEncoding.GetString(stringBytes).TrimEnd('\0');
        }

        private byte[] EncodeString(string value)
        {
            byte[] bytes = StringEncoding.GetBytes(value);
            if (bytes.Length % 2 != 0)
            {
                byte[] padded = new byte[bytes.Length + 1];
                Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);
                bytes = padded;
            }

            return ApplyStringFormat(bytes);
        }

        private byte[] ApplyStringFormat(byte[] bytes)
        {
            byte[] result = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);

            if (SwapStringBytes)
            {
                SwapEveryTwoBytes(result);
            }

            if (ReverseStringBytes)
            {
                Array.Reverse(result);
            }

            return result;
        }

        private static bool[] BitsToBooleans(byte[] bytes, int count)
        {
            bool[] values = new bool[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
            }

            return values;
        }

        private object BytesToValue(byte[] bytes, Type type)
        {
            byte[] valueBytes = ApplyDataFormat(bytes);

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

        private byte[] ValueToBytes(object value, Type type)
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

            return ApplyDataFormat(bytes);
        }

        private byte[] ApplyDataFormat(byte[] bytes)
        {
            byte[] result = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(result);
            }

            if (result.Length <= 2)
            {
                if (DataFormat == DataFormat.BADC || DataFormat == DataFormat.DCBA)
                {
                    SwapEveryTwoBytes(result);
                }

                return result;
            }

            switch (DataFormat)
            {
                case DataFormat.ABCD:
                    break;
                case DataFormat.BADC:
                    SwapEveryTwoBytes(result);
                    break;
                case DataFormat.CDAB:
                    SwapWords(result);
                    break;
                case DataFormat.DCBA:
                    Array.Reverse(result);
                    break;
                default:
                    throw new NotSupportedException("Unsupported data format: " + DataFormat);
            }

            return result;
        }

        private static void SwapEveryTwoBytes(byte[] bytes)
        {
            for (int i = 0; i + 1 < bytes.Length; i += 2)
            {
                byte temp = bytes[i];
                bytes[i] = bytes[i + 1];
                bytes[i + 1] = temp;
            }
        }

        private static void SwapWords(byte[] bytes)
        {
            if (bytes.Length % 2 != 0)
            {
                return;
            }

            byte[] copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            int wordCount = bytes.Length / 2;
            for (int i = 0; i < wordCount; i++)
            {
                int source = i * 2;
                int target = ((i + wordCount / 2) % wordCount) * 2;
                bytes[target] = copy[source];
                bytes[target + 1] = copy[source + 1];
            }
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

        private AddressInfo ParseAddress(string point, Type targetType)
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
                ushort address = NormalizeAddress(ParseUInt16Address(text.Substring(separatorIndex + 1)));
                return new AddressInfo(ParseArea(prefix, targetType), address);
            }

            int reference;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out reference))
            {
                if (reference >= 40001 && reference <= 49999)
                {
                    return new AddressInfo(RegisterArea.HoldingRegister, ParseReferenceAddress(reference, 40000));
                }

                if (reference >= 30001 && reference <= 39999)
                {
                    return new AddressInfo(RegisterArea.InputRegister, ParseReferenceAddress(reference, 30000));
                }

                if (reference >= 10001 && reference <= 19999)
                {
                    return new AddressInfo(RegisterArea.DiscreteInput, ParseReferenceAddress(reference, 10000));
                }

                if (reference >= 1 && reference <= 9999)
                {
                    Type elementType = GetElementType(targetType);
                    RegisterArea area = elementType == typeof(bool)
                        ? RegisterArea.Coil
                        : RegisterArea.HoldingRegister;
                    return new AddressInfo(area, NormalizeAddress(checked((ushort)reference)));
                }

                if (reference >= 0 && reference <= ushort.MaxValue)
                {
                    Type elementType = GetElementType(targetType);
                    RegisterArea area = elementType == typeof(bool)
                        ? RegisterArea.Coil
                        : RegisterArea.HoldingRegister;
                    return new AddressInfo(area, NormalizeAddress((ushort)reference));
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

        private ushort NormalizeAddress(ushort address)
        {
            if (AddressStartWithZero || address == 0)
            {
                return address;
            }

            return checked((ushort)(address - 1));
        }

        private ushort ParseReferenceAddress(int reference, int baseAddress)
        {
            int address = reference - baseAddress;
            if (ReferenceAddressStartWithZero)
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
            if (Station < byte.MinValue || Station > byte.MaxValue)
            {
                throw new InvalidOperationException("Station must be between 0 and 255.");
            }

            return (byte)Station;
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

        private static Result<T> Error<T>(string message, int errorCode)
        {
            return new Result<T>
            {
                IsSuccess = false,
                Message = message,
                ErrorCode = errorCode
            };
        }

        private enum RegisterArea
        {
            Coil,
            DiscreteInput,
            HoldingRegister,
            InputRegister
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
