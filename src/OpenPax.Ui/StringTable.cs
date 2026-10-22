using OpenPax.Data.Resources;
using OpenPax.Data.Text;
using OpenPax.Data.Views;

namespace OpenPax.Ui;

/// <summary>The game's STR# string lists (Data/StrData), parsed on first use.</summary>
public sealed class StringTable
{
    private readonly ResourceFile _file;
    private readonly Dictionary<int, IReadOnlyList<string>> _lists = [];
    private readonly Dictionary<(int List, int Index), string> _overrides = [];

    public StringTable(ResourceFile file)
    {
        _file = file;
    }

    /// <summary>The string, or an empty one when the list or index does not exist.</summary>
    public string Get(TextRef text)
    {
        if (_overrides.TryGetValue((text.List, text.Index), out var given))
        {
            return given;
        }

        if (!_lists.TryGetValue(text.List, out var list))
        {
            list = _file.TryGet(ResourceType.StringList, text.List, out var body) ? StringList.Parse(body.Span) : [];
            _lists[text.List] = list;
        }

        return text.Index >= 0 && text.Index < list.Count ? list[text.Index] : "";
    }

    /// <summary>A string a mod's content gives (docs/modding/content.md), read in place of the game's, or where the game has none.</summary>
    public void Override(int list, int index, string text) => _overrides[(list, index)] = text;
}
