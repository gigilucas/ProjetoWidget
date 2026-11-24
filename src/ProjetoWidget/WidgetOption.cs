using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace ProjetoWidget;

/// <summary>One row in the manager window: a widget the user can switch on or off.</summary>
public sealed class WidgetOption : INotifyPropertyChanged
{
    private bool _isOn;

    public WidgetOption(string id, string name, string glyph, string fromColor, string toColor)
    {
        Id = id;
        Name = name;
        Glyph = glyph;
        IconBrush = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(fromColor),
            (Color)ColorConverter.ConvertFromString(toColor),
            new Point(0, 0), new Point(1, 1));
        IconBrush.Freeze();
    }

    public string Id { get; }
    public string Name { get; }
    public string Glyph { get; }
    public Brush IconBrush { get; }

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value) return;
            _isOn = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
