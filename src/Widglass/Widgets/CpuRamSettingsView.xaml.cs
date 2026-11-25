using System.Globalization;
using System.Windows.Controls;
using Widglass.Services;

namespace Widglass.Widgets;

public partial class CpuRamSettingsView : UserControl
{
    public CpuRamSettingsView()
    {
        InitializeComponent();

        CpuNameText.Text = SystemStats.ProcessorName;
        CoresText.Text = $"{SystemStats.CoreCount} threads";
        Loaded += async (_, _) =>
        {
            using var stats = await SystemStats.CreateAsync();
            MemoryText.Text = string.Format(CultureInfo.InvariantCulture, "{0:0} GB", stats.Sample().TotalGb);
        };
    }
}
