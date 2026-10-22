namespace OpenPax.Sim.Saving;

/// <summary>What the next value in a saved game is (docs/formats/port-saves.md).</summary>
internal enum SaveTag : byte
{
    Null = 0,
    Bool = 1,
    Byte = 2,
    SByte = 3,
    Int16 = 4,
    UInt16 = 5,
    Int32 = 6,
    UInt32 = 7,
    Int64 = 8,
    UInt64 = 9,
    Double = 10,
    Single = 11,
    Char = 12,
    String = 13,
    Enum = 14,

    /// <summary>An object already written, by its number.</summary>
    Reference = 20,

    /// <summary>A new object: its type, then its fields by name.</summary>
    Object = 21,

    /// <summary>A value type written in place: its type, then its fields.</summary>
    Struct = 22,
    Array = 23,

    /// <summary>A list or set: its type, then its items.</summary>
    Collection = 24,
    Dictionary = 25,

    /// <summary>An object of the game data, by where it sits in the tables.</summary>
    Table = 26,
}
