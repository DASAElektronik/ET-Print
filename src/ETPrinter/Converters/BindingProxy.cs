using System.Windows;

namespace ETPrinter.Converters;

/// <summary>
/// Freezable-Bruecke, um den DataContext in Elemente zu bringen, die NICHT im
/// Visual-/Logical-Tree des Fensters haengen (ContextMenu, RowDefinition, ...).
/// Dort traegt RelativeSource AncestorType=Window nicht — die Commands im
/// Etiketten-Kontextmenue waren deshalb null ("Cannot find source for binding").
/// Verwendung: &lt;converters:BindingProxy x:Key="Proxy" Data="{Binding}"/&gt;
/// und dann {Binding Data.CopyCommand, Source={StaticResource Proxy}}.
/// </summary>
public class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));
}
