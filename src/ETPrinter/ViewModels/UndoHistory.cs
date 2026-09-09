using ETPrinter.Models;

namespace ETPrinter.ViewModels;

/// <summary>Snapshot des Arbeitsbereichs fuer Undo/Redo: kompletter Projektzustand
/// plus Cursor (sichtbare Seite, ausgewaehltes Etikett/Modul).</summary>
public sealed record EditorState(LabelProject Project, int PageIndex, int SelectedIndex);

/// <summary>
/// Undo/Redo-Verlauf nach dem Memento-Prinzip (ABSCHLUSSPLAN AP9c): Der Aufrufer
/// meldet nach jeder Aenderung den NEUEN Zustand (<see cref="Record"/>); der vorherige
/// Zustand wandert auf den Undo-Stapel. Aenderungen mit demselben Schluessel aus
/// <paramref name="coalesceKeys"/> innerhalb des Zeitfensters (Tippen in einer Zelle,
/// Ziffern in einem Randfeld) werden zu einem Schritt zusammengefasst.
/// </summary>
public sealed class UndoHistory<T> where T : class
{
    private readonly List<(T State, string Label, DateTime Time)> _undo = [];
    private readonly List<(T State, string Label)> _redo = [];
    private readonly HashSet<string> _coalesceKeys;
    private T? _current;

    public UndoHistory(IEnumerable<string>? coalesceKeys = null, int capacity = 100)
    {
        _coalesceKeys = new HashSet<string>(coalesceKeys ?? [], StringComparer.Ordinal);
        Capacity = Math.Max(1, capacity);
    }

    /// <summary>Maximale Anzahl Undo-Schritte (aelteste fallen weg).</summary>
    public int Capacity { get; }

    /// <summary>Zeitfenster, in dem gleichartige Aenderungen zusammengefasst werden.</summary>
    public TimeSpan CoalesceWindow { get; set; } = TimeSpan.FromSeconds(1.5);

    /// <summary>Uhr (Tests koennen sie stellen).</summary>
    public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

    /// <summary>Zuletzt gemeldeter Zustand (entspricht dem aktuellen Arbeitsbereich).</summary>
    public T? Current => _current;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;

    /// <summary>Beschriftung des naechsten Undo-Schritts (null = keiner).</summary>
    public string? NextUndoLabel => _undo.Count > 0 ? _undo[^1].Label : null;
    public string? NextRedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;

    public event Action? Changed;

    /// <summary>Verlauf verwerfen und mit <paramref name="current"/> neu beginnen
    /// (Laden, Neues Projekt).</summary>
    public void Reset(T current)
    {
        _undo.Clear();
        _redo.Clear();
        _current = current;
        Changed?.Invoke();
    }

    /// <summary>Aktuellen Zustand nachfuehren, ohne einen Schritt zu erzeugen (Cursor:
    /// Seite/Auswahl aendern sich ohne Inhaltsaenderung).</summary>
    public void TouchCurrent(Func<T, T> update)
    {
        if (_current is not null) _current = update(_current);
    }

    /// <summary>Nach einer Aenderung: <paramref name="newState"/> ist jetzt aktuell, der
    /// bisherige Zustand wird (sofern nicht zusammengefasst) zum Undo-Schritt.</summary>
    public void Record(T newState, string label)
    {
        var now = Clock();
        bool coalesce = _undo.Count > 0
            && _coalesceKeys.Contains(label)
            && _undo[^1].Label == label
            && now - _undo[^1].Time < CoalesceWindow;

        if (coalesce)
        {
            // Fenster verlaengern: fortlaufendes Tippen bleibt ein Schritt
            _undo[^1] = (_undo[^1].State, label, now);
        }
        else if (_current is not null)
        {
            _undo.Add((_current, label, now));
            if (_undo.Count > Capacity) _undo.RemoveAt(0);
        }

        _redo.Clear();
        _current = newState;
        Changed?.Invoke();
    }

    /// <summary>Einen Schritt zurueck; liefert den wiederherzustellenden Zustand und die
    /// Beschriftung des rueckgaengig gemachten Schritts, null wenn nichts vorliegt.</summary>
    public (T State, string Label)? Undo()
    {
        if (_undo.Count == 0 || _current is null) return null;
        var (state, label, _) = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add((_current, label));
        _current = state;
        Changed?.Invoke();
        return (state, label);
    }

    public (T State, string Label)? Redo()
    {
        if (_redo.Count == 0 || _current is null) return null;
        var (state, label) = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add((_current, label, Clock() - CoalesceWindow)); // nicht mehr zusammenfassbar
        _current = state;
        Changed?.Invoke();
        return (state, label);
    }
}
