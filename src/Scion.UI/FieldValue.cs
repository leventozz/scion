namespace Scion.UI;

/// <summary>
/// The value a text field is rendered with, kept from fighting the user's typing. The app hands the typed text on to its
/// own state and gets it back on the next render; writing that back into the field would overwrite keys typed in the
/// meantime. So the rendered value only changes when the text was changed from outside (a reload, a text taken from
/// elsewhere, a correction the app made), never because of what was typed.
/// </summary>
internal sealed class FieldValue(string initial)
{
    private string _lastTyped = initial;

    /// <summary>What goes into the field. Constant while the user types.</summary>
    public string Rendered { get; private set; } = initial;

    /// <summary>
    /// Changes when the field has to be made again to take <see cref="Rendered"/>: the browser is only told a value when it
    /// differs from the one rendered before, so going back to that very value needs a new field.
    /// </summary>
    public int Generation { get; private set; }

    /// <summary>The user typed this text.</summary>
    public void Typed(string text) => _lastTyped = text;

    /// <summary>The app's text for the field, as it stands now. Called on every render: the same text again changes nothing.</summary>
    public void Offered(string text)
    {
        if (text == _lastTyped)
        {
            return;
        }

        _lastTyped = text;
        if (text == Rendered)
        {
            Generation++;
        }

        Rendered = text;
    }
}
