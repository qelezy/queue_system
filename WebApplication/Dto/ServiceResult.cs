namespace WebApplication.Dto
{
    public class ServiceResult
    {
        public bool Succeeded { get; set; }
        public string? Message { get; set; }
        public IEnumerable<string>? Errors { get; set; }

        public static ServiceResult Success(string? message = null) => new ServiceResult { Succeeded = true, Message = message };
        public static ServiceResult Fail(IEnumerable<string> errors, string? message = null) => new ServiceResult { Succeeded = false, Errors = errors, Message = message };
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T? Data { get; set; }

        public static ServiceResult<T> Success(T data, string? message = null) => new ServiceResult<T> { Succeeded = true, Data = data, Message = message };
        public static new ServiceResult<T> Fail(IEnumerable<string> errors, string? message = null) => new ServiceResult<T> { Succeeded = false, Errors = errors, Message = message };
    
    }
}
