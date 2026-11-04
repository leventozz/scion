using Scion.Application.Adapting;

namespace Scion.Application.Tests.Adapting;

public class TextDiffTests
{
    private static string Describe(DiffRow row) => $"{Describe(row.Left)} | {Describe(row.Right)}";

    private static string Describe(DiffLine? line) => line is null ? "-" : $"{line.Number}:{line.Kind}:{line.Text}";

    [Fact]
    public void Identical_texts_are_the_same_on_both_sides_with_their_numbers()
    {
        var rows = TextDiff.SideBySide("a\nb\n", "a\nb\n");

        Assert.Equal(["1:Same:a | 1:Same:a", "2:Same:b | 2:Same:b"], rows.Select(Describe));
    }

    [Fact]
    public void A_final_line_break_does_not_make_an_extra_line()
    {
        Assert.Single(TextDiff.SideBySide("a\n", "a\n"));
        Assert.Single(TextDiff.SideBySide("a", "a"));
    }

    [Fact]
    public void A_changed_line_is_removed_on_the_left_and_added_on_the_right_in_the_same_row()
    {
        var rows = TextDiff.SideBySide("a\nold\nc\n", "a\nnew\nc\n");

        Assert.Equal(
            ["1:Same:a | 1:Same:a", "2:Removed:old | 2:Added:new", "3:Same:c | 3:Same:c"],
            rows.Select(Describe));
    }

    [Fact]
    public void An_added_line_has_nothing_on_the_left()
    {
        var rows = TextDiff.SideBySide("a\nc\n", "a\nb\nc\n");

        Assert.Equal(
            ["1:Same:a | 1:Same:a", "- | 2:Added:b", "2:Same:c | 3:Same:c"],
            rows.Select(Describe));
    }

    [Fact]
    public void A_removed_line_has_nothing_on_the_right()
    {
        var rows = TextDiff.SideBySide("a\nb\nc\n", "a\nc\n");

        Assert.Equal(
            ["1:Same:a | 1:Same:a", "2:Removed:b | -", "3:Same:c | 2:Same:c"],
            rows.Select(Describe));
    }

    [Fact]
    public void A_changed_block_with_more_lines_on_the_right_pairs_what_it_can_and_adds_the_rest()
    {
        var rows = TextDiff.SideBySide("a\nx\ny\nz\n", "a\nx2\ny2\nq\nr\nz\n");

        Assert.Equal(
            [
                "1:Same:a | 1:Same:a",
                "2:Removed:x | 2:Added:x2",
                "3:Removed:y | 3:Added:y2",
                "- | 4:Added:q",
                "- | 5:Added:r",
                "4:Same:z | 6:Same:z",
            ],
            rows.Select(Describe));
    }

    [Fact]
    public void A_changed_block_with_more_lines_on_the_left_pairs_what_it_can_and_removes_the_rest()
    {
        var rows = TextDiff.SideBySide("a\nx\ny\nz\nb\n", "a\nx2\nb\n");

        Assert.Equal(
            [
                "1:Same:a | 1:Same:a",
                "2:Removed:x | 2:Added:x2",
                "3:Removed:y | -",
                "4:Removed:z | -",
                "5:Same:b | 3:Same:b",
            ],
            rows.Select(Describe));
    }

    [Fact]
    public void Everything_on_the_right_is_added_when_the_original_is_empty()
    {
        var rows = TextDiff.SideBySide("", "a\nb\n");

        Assert.Equal(["- | 1:Added:a", "- | 2:Added:b"], rows.Select(Describe));
    }

    [Fact]
    public void Everything_on_the_left_is_removed_when_the_adapted_text_is_empty()
    {
        var rows = TextDiff.SideBySide("a\nb\n", "");

        Assert.Equal(["1:Removed:a | -", "2:Removed:b | -"], rows.Select(Describe));
    }

    [Fact]
    public void Finds_the_shortest_edit_and_not_a_line_by_line_mismatch()
    {
        var rows = TextDiff.SideBySide("a\nb\nc\nd\ne\n", "b\nc\nd\ne\nf\n");

        Assert.Equal(
            ["1:Removed:a | -", "2:Same:b | 1:Same:b", "3:Same:c | 2:Same:c", "4:Same:d | 3:Same:d", "5:Same:e | 4:Same:e", "- | 5:Added:f"],
            rows.Select(Describe));
    }

    [Fact]
    public void Two_completely_different_large_texts_still_give_a_result_quickly()
    {
        var original = string.Join('\n', Enumerable.Range(0, 5000).Select(index => $"old {index}"));
        var adapted = string.Join('\n', Enumerable.Range(0, 5000).Select(index => $"new {index}"));

        var rows = TextDiff.SideBySide(original, adapted);

        Assert.Equal(5000, rows.Count);
        Assert.All(rows, row => Assert.Equal(DiffKind.Removed, row.Left!.Kind));
        Assert.All(rows, row => Assert.Equal(DiffKind.Added, row.Right!.Kind));
    }
}
