using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Scion.UI;

/// <summary>Writes elements and text into a render tree; it hands out the sequence numbers so callers never count.</summary>
internal sealed class MarkdownTree(RenderTreeBuilder builder)
{
    private int _sequence;

    public void Open(string element, string? cssClass = null)
    {
        builder.OpenElement(_sequence++, element);
        if (cssClass is not null)
        {
            Attribute("class", cssClass);
        }
    }

    public void Attribute(string name, string value) => builder.AddAttribute(_sequence++, name, value);

    public void Handler<TArgs>(string name, EventCallback<TArgs> callback) => builder.AddAttribute(_sequence++, name, callback);

    public void Close() => builder.CloseElement();

    public void Element(string element, string? cssClass = null)
    {
        Open(element, cssClass);
        Close();
    }

    public void Text(string text) => builder.AddContent(_sequence++, text);
}
