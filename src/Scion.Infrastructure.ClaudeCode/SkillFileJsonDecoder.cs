using System.Globalization;
using System.Text;

namespace Scion.Infrastructure.ClaudeCode;

/// <summary>
/// Reads the <c>skillFile</c> string of <c>{"skillFile":"…"}</c> while its JSON text is still arriving in pieces of any
/// size, and gives back the text that is new with each piece. A piece may end inside an escape (<c>\n</c>, <c>\"</c>,
/// <c>\uXXXX</c>); a high surrogate is held back until its pair has arrived. The text is only as good as the JSON: it
/// is for display, and the verified value is the one in the final result.
/// </summary>
internal sealed class SkillFileJsonDecoder
{
    private const string PropertyName = "skillFile";

    private enum Part
    {
        BeforeKey,
        Key,
        AfterKey,
        BeforeValue,
        Value,
        Done,
    }

    private enum Escape
    {
        None,
        Backslash,
        Unicode,
    }

    private readonly StringBuilder _key = new();
    private readonly StringBuilder _hexDigits = new();
    private Part _part = Part.BeforeKey;
    private Escape _escape = Escape.None;
    private char? _highSurrogate;
    private bool _isSkillFile;

    /// <summary>Takes the next piece of the JSON text and returns the part of the skill file it completes.</summary>
    public string Append(string piece)
    {
        var text = new StringBuilder();
        foreach (var character in piece)
        {
            Step(character, text);
        }

        return text.ToString();
    }

    private void Step(char character, StringBuilder text)
    {
        switch (_part)
        {
            case Part.BeforeKey when character == '"':
                _part = Part.Key;
                break;
            case Part.Key:
                if (ReadStringCharacter(character, _key))
                {
                    _isSkillFile = _key.ToString() == PropertyName;
                    _part = Part.AfterKey;
                }

                break;
            case Part.AfterKey when character == ':':
                _part = Part.BeforeValue;
                break;
            case Part.BeforeValue when character == '"':
                _part = Part.Value;
                break;
            case Part.BeforeValue when !char.IsWhiteSpace(character):
                _part = Part.Done;
                break;
            case Part.Value:
                if (ReadStringCharacter(character, _isSkillFile ? text : null))
                {
                    _part = Part.Done;
                }

                break;
        }
    }

    /// <returns>True when the character closed the string.</returns>
    private bool ReadStringCharacter(char character, StringBuilder? sink)
    {
        switch (_escape)
        {
            case Escape.Backslash:
                _escape = Escape.None;
                if (character == 'u')
                {
                    _escape = Escape.Unicode;
                }
                else
                {
                    Emit(Unescaped(character), sink);
                }

                return false;
            case Escape.Unicode:
                _hexDigits.Append(character);
                if (_hexDigits.Length == 4)
                {
                    _escape = Escape.None;
                    Emit(CodeUnitOf(_hexDigits.ToString()), sink);
                    _hexDigits.Clear();
                }

                return false;
            default:
                if (character == '"')
                {
                    EndString(sink);

                    return true;
                }

                if (character == '\\')
                {
                    _escape = Escape.Backslash;
                }
                else
                {
                    Emit(character, sink);
                }

                return false;
        }
    }

    private static char Unescaped(char character) => character switch
    {
        'n' => '\n',
        't' => '\t',
        'r' => '\r',
        'b' => '\b',
        'f' => '\f',
        _ => character,
    };

    private static char CodeUnitOf(string hexDigits) =>
        int.TryParse(hexDigits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code) ? (char)code : '�';

    private void Emit(char character, StringBuilder? sink)
    {
        if (_highSurrogate is { } high)
        {
            _highSurrogate = null;
            sink?.Append(char.IsLowSurrogate(character) ? high : '�');
        }

        if (char.IsHighSurrogate(character))
        {
            _highSurrogate = character;

            return;
        }

        sink?.Append(character);
    }

    private void EndString(StringBuilder? sink)
    {
        if (_highSurrogate is not null)
        {
            _highSurrogate = null;
            sink?.Append('�');
        }
    }
}
