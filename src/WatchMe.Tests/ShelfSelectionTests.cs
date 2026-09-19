using WatchMe.Shelf;

namespace WatchMe.Tests;

public class ShelfSelectionTests
{
    private static List<string> Items() => ["a", "b", "c", "d", "e"];

    [Fact]
    public void PlainClick_SelectsOnlyThatItem()
    {
        var selection = new ShelfSelection<string>();
        var items = Items();
        selection.ApplyClick("a", ctrl: false, shift: false, items);
        selection.ApplyClick("c", ctrl: false, shift: false, items);

        Assert.Equal(["c"], selection.Selected);
        Assert.True(selection.IsSelected("c"));
        Assert.False(selection.IsSelected("a"));
    }

    [Fact]
    public void CtrlClick_TogglesWithoutClearing()
    {
        var selection = new ShelfSelection<string>();
        var items = Items();
        selection.ApplyClick("a", ctrl: false, shift: false, items);
        selection.ApplyClick("c", ctrl: true, shift: false, items);
        selection.ApplyClick("d", ctrl: true, shift: false, items);

        Assert.Equal(["a", "c", "d"], selection.Selected.Order().ToArray());

        selection.ApplyClick("c", ctrl: true, shift: false, items);
        Assert.Equal(["a", "d"], selection.Selected.Order().ToArray());
    }

    [Fact]
    public void ShiftClick_SelectsRangeFromAnchor()
    {
        var selection = new ShelfSelection<string>();
        var items = Items();
        selection.ApplyClick("a", ctrl: false, shift: false, items);
        selection.ApplyClick("d", ctrl: false, shift: true, items);

        Assert.Equal(["a", "b", "c", "d"], selection.Selected.Order().ToArray());
    }

    [Fact]
    public void ShiftClick_BackwardsRange_AlsoWorks()
    {
        var selection = new ShelfSelection<string>();
        var items = Items();
        selection.ApplyClick("d", ctrl: false, shift: false, items);
        selection.ApplyClick("b", ctrl: false, shift: true, items);

        Assert.Equal(["b", "c", "d"], selection.Selected.Order().ToArray());
    }

    [Fact]
    public void SelectMany_And_Clear()
    {
        var selection = new ShelfSelection<string>();
        selection.SelectMany(["a", "b"]);
        Assert.Equal(2, selection.Selected.Count);

        selection.Clear();
        Assert.Empty(selection.Selected);
    }
}
