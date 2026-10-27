using System;
using System.Collections.Generic;

namespace Game.UI;

/// <summary>Notebook progress passed between scene scripts during a playthrough.</summary>
public sealed class NotebookState
{
    private readonly SortedSet<int> pages = [];
    public bool Available { get; set; }
    public IReadOnlyCollection<int> Pages => pages;
    public void Unlock(int number)
    {
        if (number < 1 || number > 8) throw new ArgumentOutOfRangeException(nameof(number));
        pages.Add(number);
    }
}
