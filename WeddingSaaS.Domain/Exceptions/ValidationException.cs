namespace WeddingSaaS.Domain.Exceptions;

public sealed class ValidationException : DomainException
{
    public ValidationException(string propertyName, string message) : base(message) => PropertyName = propertyName;
    public string PropertyName { get; }
}
