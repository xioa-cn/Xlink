namespace XLinkCore.ModbusTcp;

public static class ModbusExtensions
{
    public static Result<byte[]> OriginalByteEx(this ModbusTcp modbusTcp, byte[] bytes)
    {
        if (modbusTcp == null)
        {
            throw new System.ArgumentNullException(nameof(modbusTcp));
        }

        try
        {
            return new Result<byte[]>(modbusTcp.OriginalBytes(bytes))
            {
                IsSuccess = true,
                Message = "Success"
            };
        }
        catch (System.Exception ex)
        {
            return new Result<byte[]>
            {
                IsSuccess = false,
                Message = ex.Message,
                ErrorCode = ModbusTcpErrorCodes.FromException(ex)
            };
        }
    }
}
