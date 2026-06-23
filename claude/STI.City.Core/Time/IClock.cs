namespace STI.City.Core.Time;

/// <summary>
/// Abstraction over the system clock so the retrieval timestamp persisted with a
/// cached geocoding record is deterministic and testable.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Default <see cref="IClock"/> backed by <see cref="DateTimeOffset.UtcNow"/>.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
