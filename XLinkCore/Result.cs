namespace XLinkCore;

public class Result
{
    public string Message { get; set; } = "Success";
    public bool IsSuccess { get; set; }
    public int ErrorCode { get; set; }

    public Result()
    {
    }

    public Result(string message)
    {
        Message = message;
    }
}

public class Result<T> : Result
{
    public T? Data { get; set; }

    public Result()
    {
    }

    public Result(T? data)
    {
        Data = data;
    }
}

public static class ResultHelper
{
    public static Result CreateSuccessResult(string message)
    {
        return new Result(message);
    }

    public static Result CreateErrorResult(string message, int errorCode)
    {
        return new Result(message)
        {
            IsSuccess = false,
            ErrorCode = errorCode
        };
    }

    public static Result<T> CreateSuccessResult<T>(T data, string message)
    {
        return new Result<T>(data);
    }

    public static Result<T> CreateErrorResult<T>(string message, int errorCode)
    {
        return new Result<T>()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            Message = message
        };
    }
}