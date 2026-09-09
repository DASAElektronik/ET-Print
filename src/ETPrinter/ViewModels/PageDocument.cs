using System.Collections.ObjectModel;

namespace ETPrinter.ViewModels;

/// <summary>Nicht-generische Sicht auf ein Seitendokument (Seitenzahl/aktuelle Seite),
/// damit das Haupt-ViewModel modusunabhaengig auf das aktive Dokument zugreifen kann.</summary>
public interface IPageDocument
{
    int PageCount { get; }
    int CurrentIndex { get; }
}

/// <summary>
/// Mehrseitiges Dokument aus Etiketten- oder Modul-ViewModels (ABSCHLUSSPLAN AP9,
/// Schnitt "PageDocument"). Haelt ALLE Seiten (<see cref="Pages"/>) und spiegelt die
/// aktuell sichtbare Seite in <see cref="Visible"/>, an die die Vorschau gebunden ist.
/// Reine Datenhaltung: Auswahl, Dirty-Markierung und Statusmeldungen bleiben beim
/// Aufrufer.
/// </summary>
public sealed class PageDocument<T> : IPageDocument
{
    private readonly List<List<T>> _pages = [];
    private int _currentIndex;

    /// <summary>Elemente der aktuell sichtbaren Seite (Binding-Ziel).</summary>
    public ObservableCollection<T> Visible { get; } = [];

    /// <summary>Alle Seiten in Reihenfolge (nur lesen; aendern ueber die Methoden).</summary>
    public IReadOnlyList<IReadOnlyList<T>> Pages => _pages;

    public int PageCount => _pages.Count;
    public int CurrentIndex => _currentIndex;

    /// <summary>Alle Elemente aller Seiten.</summary>
    public IEnumerable<T> AllItems => _pages.SelectMany(p => p);

    public IReadOnlyList<T> PageAt(int index) => _pages[index];

    /// <summary>Alle Seiten verwerfen und mit genau einer Seite neu beginnen; die
    /// neue Seite wird sichtbar.</summary>
    public void Reset(List<T> firstPage)
    {
        _pages.Clear();
        _pages.Add(firstPage);
        Show(0);
    }

    /// <summary>Alle Seiten verwerfen (Visible wird geleert).</summary>
    public void Clear()
    {
        _pages.Clear();
        Visible.Clear();
        _currentIndex = 0;
    }

    /// <summary>Seite anhaengen, ohne die sichtbare Seite zu wechseln.</summary>
    public void AddPage(List<T> page) => _pages.Add(page);

    /// <summary>Seite entfernen; die sichtbare Seite wird auf einen gueltigen Index
    /// nachgefuehrt. False, wenn es die letzte Seite ist.</summary>
    public bool RemovePage(int index)
    {
        if (_pages.Count <= 1 || index < 0 || index >= _pages.Count) return false;
        _pages.RemoveAt(index);
        Show(Math.Min(index, _pages.Count - 1));
        return true;
    }

    /// <summary>Stellt sicher, dass Seite <paramref name="index"/> existiert (fehlende
    /// Seiten werden ueber <paramref name="factory"/> angelegt).</summary>
    public void EnsurePage(int index, Func<List<T>> factory)
    {
        while (index >= _pages.Count)
            _pages.Add(factory());
    }

    /// <summary>Seite <paramref name="index"/> sichtbar machen. False bei ungueltigem
    /// Index (dann bleibt alles unveraendert).</summary>
    public bool Show(int index)
    {
        if (index < 0 || index >= _pages.Count) return false;
        _currentIndex = index;
        Visible.Clear();
        foreach (var item in _pages[index])
            Visible.Add(item);
        return true;
    }
}
