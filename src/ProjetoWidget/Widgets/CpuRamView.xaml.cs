using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ProjetoWidget.Services;

namespace ProjetoWidget.Widgets;

/// <summary>CPU / RAM widget: live usage of this PC, refreshed every second.</summary>
public partial class CpuRamView : UserControl
{
    private readonly RingGauge _cpuRing = new(Color.FromRgb(0x3F, 0xB8, 0xFF), Color.FromRgb(0xFF, 0x5F, 0xA8));
    private readonly RingGauge _ramRing = new(Color.FromRgb(0x8B, 0x5C, 0xFF), Color.FromRgb(0xFF, 0xB1, 0x3D));
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private SystemStats? _stats;

    public CpuRamView()
    {
        InitializeComponent();
        CpuRingHost.Content = _cpuRing;
        RamRingHost.Content = _ramRing;

        _timer.Tick += (_, _) => Refresh();
        Loaded += async (_, _) =>
        {
            _stats ??= await SystemStats.CreateAsync();
            Refresh();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Refresh()
    {
        if (_stats is null) return;

        var s = _stats.Sample();
        _cpuRing.Value = s.CpuPercent;
        _ramRing.Value = s.MemoryPercent;
        CpuSub.Text = s.CpuGHz > 0 ? s.CpuGHz.ToString("0.00", CultureInfo.InvariantCulture) + " GHz" : $"{SystemStats.CoreCount} cores";
        RamSub.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.0} / {1:0} GB", s.UsedGb, s.TotalGb);
    }
}
