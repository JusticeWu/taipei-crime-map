namespace TaipeiCrimeMap.Domain.Events;

public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
        
    }
}