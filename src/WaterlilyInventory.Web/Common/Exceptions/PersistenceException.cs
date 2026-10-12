namespace WaterlilyInventory.Web.Common.Exceptions;

/// <summary>
/// Wraps unexpected database/persistence failures for the Service layer.
/// Defined outside EF-specific code so Services can catch a known exception type
/// without depending on EF Core internals. The original EF/SQL exception is
/// preserved as <see cref="Exception.InnerException"/> for diagnostic logging.
/// </summary>
public class PersistenceException : Exception
{
    /// <summary>
    /// Creates a new PersistenceException with a safe message and the original cause.
    /// </summary>
    /// <param name="message">A safe, non-leaking description of the failure.</param>
    /// <param name="innerException">The original EF Core / SQL exception for diagnostic logging.</param>
    public PersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new PersistenceException with a safe message.
    /// </summary>
    /// <param name="message">A safe, non-leaking description of the failure.</param>
    public PersistenceException(string message)
        : base(message)
    {
    }
}
