using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace Scion.UI;

/// <summary>Writes Markdig blocks as Blazor elements; a block it does not know is shown as its source text.</summary>
internal sealed class MarkdownBlocks(MarkdownTree tree, MarkdownInlines inlines, string source)
{
    public void Write(ContainerBlock container, bool tight = false)
    {
        foreach (var block in container)
        {
            WriteBlock(block, tight);
        }
    }

    private void WriteBlock(Block block, bool tight)
    {
        switch (block)
        {
            case HeadingBlock heading:
                tree.Open($"h{Math.Clamp(heading.Level, 1, 6)}");
                inlines.Write(heading.Inline);
                tree.Close();
                break;
            case ParagraphBlock paragraph when tight:
                inlines.Write(paragraph.Inline);
                break;
            case ParagraphBlock paragraph:
                tree.Open("p");
                inlines.Write(paragraph.Inline);
                tree.Close();
                break;
            case ListBlock list:
                WriteList(list);
                break;
            case QuoteBlock quote:
                tree.Open("blockquote");
                Write(quote);
                tree.Close();
                break;
            case ThematicBreakBlock:
                tree.Element("hr");
                break;
            case CodeBlock code:
                tree.Open("pre", "code-block");
                tree.Open("code");
                tree.Text(code.Lines.ToString());
                tree.Close();
                tree.Close();
                break;
            case Table table:
                WriteTable(table);
                break;
            default:
                WriteSource(block);
                break;
        }
    }

    private void WriteList(ListBlock list)
    {
        tree.Open(list.IsOrdered ? "ol" : "ul");
        if (list.IsOrdered && list.OrderedStart is { Length: > 0 } start && start != "1")
        {
            tree.Attribute("start", start);
        }

        foreach (var item in list.OfType<ListItemBlock>())
        {
            tree.Open("li");
            Write(item, tight: !list.IsLoose);
            tree.Close();
        }

        tree.Close();
    }

    private void WriteTable(Table table)
    {
        var rows = table.OfType<TableRow>().ToList();

        tree.Open("div", "markdown-table-scroll");
        tree.Open("table", "markdown-table");
        WriteRows("thead", "th", rows.Where(row => row.IsHeader));
        WriteRows("tbody", "td", rows.Where(row => !row.IsHeader));
        tree.Close();
        tree.Close();
    }

    private void WriteRows(string section, string cellElement, IEnumerable<TableRow> rows)
    {
        var rowList = rows.ToList();
        if (rowList.Count == 0)
        {
            return;
        }

        tree.Open(section);
        foreach (var row in rowList)
        {
            tree.Open("tr");
            foreach (var cell in row.OfType<TableCell>())
            {
                tree.Open(cellElement);
                Write(cell, tight: true);
                tree.Close();
            }

            tree.Close();
        }

        tree.Close();
    }

    private void WriteSource(Block block)
    {
        tree.Open("p");
        tree.Text(source.Substring(block.Span.Start, block.Span.Length));
        tree.Close();
    }
}
