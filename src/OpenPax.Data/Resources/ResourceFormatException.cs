namespace OpenPax.Data.Resources;

/// <summary>A resource container or resource body that does not parse.</summary>
public sealed class ResourceFormatException(string message) : Exception(message);
