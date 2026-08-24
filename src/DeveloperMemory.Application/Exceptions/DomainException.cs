namespace DeveloperMemory.Application.Exceptions;

public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string message, string errorCode = "domain_error")
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
