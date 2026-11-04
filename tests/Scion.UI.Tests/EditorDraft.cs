using Bunit;
using Microsoft.AspNetCore.Components;

namespace Scion.UI.Tests;

internal static class EditorDraft
{
    /// <summary>
    /// The draft the app holds: the text it gives the editor. The text area itself is not written back while the user types
    /// (see <see cref="FieldValue"/>), so its value attribute is only what it was last given from outside.
    /// </summary>
    public static string In(IRenderedComponent<IComponent> component) =>
        component.FindComponent<TextEditor>().Instance.Text;
}
