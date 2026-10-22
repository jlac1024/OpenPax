namespace OpenPax.Data.Resources;

/// <summary>
/// A four character resource type such as <c>PPIC</c> or <c>STR#</c>. The
/// original compares types as 32-bit integers built from MSVC multi-char
/// constants (<c>'RACE'</c> is 0x52414345), so on disk the characters
/// appear reversed.
/// </summary>
public readonly record struct ResourceType(uint Value)
{
    public static ResourceType Palette { get; } = FromName("CPLT");
    public static ResourceType Picture { get; } = FromName("PPIC");
    public static ResourceType StringList { get; } = FromName("STR#");
    public static ResourceType Race { get; } = FromName("RACE");
    public static ResourceType Hull { get; } = FromName("HULL");
    public static ResourceType Ship { get; } = FromName("SHIP");
    public static ResourceType Tech { get; } = FromName("TECH");
    public static ResourceType View { get; } = FromName("VIEW");

    public static ResourceType FromName(string name)
    {
        if (name.Length != 4 || name.Any(c => c > 0x7F))
        {
            throw new ArgumentException(string.Format(Strings.BadTypeName, name), nameof(name));
        }

        return new ResourceType((uint)(name[0] << 24 | name[1] << 16 | name[2] << 8 | name[3]));
    }

    public override string ToString()
    {
        Span<char> chars = [(char)(Value >> 24 & 0xFF), (char)(Value >> 16 & 0xFF), (char)(Value >> 8 & 0xFF), (char)(Value & 0xFF)];
        return new string(chars);
    }
}
