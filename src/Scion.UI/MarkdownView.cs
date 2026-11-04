using Markdig;
using Markdig.Syntax;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Scion.UI;

/// <summary>
/// Renders Markdown as Blazor elements, never as markup: untrusted content cannot become live HTML.
/// CommonMark plus pipe tables; raw HTML stays literal text.
/// </summary>
public sealed class MarkdownView : ComponentBase
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .DisableHtml()
        .Build();

    private MarkdownDocument _document = new();

    [Parameter, EditorRequired]
    public string Text { get; set; } = "";

    [Inject]
    private IExternalLinks Links { get; set; } = default!;

    protected override void OnParametersSet() => _document = Markdown.Parse(Text, Pipeline);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var tree = new MarkdownTree(builder);

        tree.Open("div", "markdown");
        new MarkdownBlocks(tree, new MarkdownInlines(tree, this, Links), Text).Write(_document);
        tree.Close();
    }
}
