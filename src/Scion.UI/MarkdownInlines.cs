using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Scion.UI;

/// <summary>Writes Markdig inlines as Blazor elements. Raw HTML never gets here: the pipeline turns it into literal text.</summary>
internal sealed class MarkdownInlines(MarkdownTree tree, IHandleEvent receiver, IExternalLinks links)
{
    public void Write(ContainerInline? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            WriteInline(inline);
        }
    }

    private void WriteInline(Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                tree.Text(literal.Content.ToString());
                break;
            case HtmlEntityInline entity:
                tree.Text(entity.Transcoded.ToString());
                break;
            case CodeInline code:
                tree.Open("code");
                tree.Text(code.Content.ToString());
                tree.Close();
                break;
            case LineBreakInline lineBreak:
                if (lineBreak.IsHard)
                {
                    tree.Element("br");
                }
                else
                {
                    tree.Text("\n");
                }

                break;
            case EmphasisInline emphasis:
                tree.Open(emphasis.DelimiterCount >= 2 ? "strong" : "em");
                Write(emphasis);
                tree.Close();
                break;
            case AutolinkInline autolink:
                WriteLink(autolink.Url, () => tree.Text(autolink.Url));
                break;
            case LinkInline { IsImage: true } image:
                Write(image); // an image is never loaded; its alt text stands in
                break;
            case LinkInline link:
                WriteLink(link.Url, () => Write(link));
                break;
            case ContainerInline container:
                Write(container);
                break;
        }
    }

    // A link has no href, so the web view can never navigate by itself; only http(s) asks the host to open it.
    private void WriteLink(string? url, Action writeLabel)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            tree.Open("span", "markdown-link-inert");
            writeLabel();
            tree.Close();
            return;
        }

        tree.Open("a", "markdown-link");
        tree.Attribute("role", "link");
        tree.Attribute("tabindex", "0");
        tree.Handler("onclick", EventCallback.Factory.Create<MouseEventArgs>(receiver, () => links.OpenAsync(uri)));
        tree.Handler("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(
            receiver, args => args.Key == "Enter" ? links.OpenAsync(uri) : Task.CompletedTask));
        writeLabel();
        tree.Close();
    }
}
