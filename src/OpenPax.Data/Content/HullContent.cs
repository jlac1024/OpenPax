using OpenPax.Data.Tables;

namespace OpenPax.Data.Content;

/// <summary>One of the six hulls as a content pack changes it: its numbers and, if given, its class name.</summary>
public sealed record HullContent(int Type, Hull Record, string? Name);
