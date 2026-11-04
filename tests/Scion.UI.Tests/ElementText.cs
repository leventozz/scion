using AngleSharp.Dom;

namespace Scion.UI.Tests;

internal static class ElementText
{
    /// <summary>The text nodes that are direct children of the element: what a reader sees, without what is drawn in child elements.</summary>
    public static string Own(this IElement element) =>
        string.Concat(element.ChildNodes.OfType<IText>().Select(text => text.Data));
}
