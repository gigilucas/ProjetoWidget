using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Widglass.Services;

namespace Widglass.Widgets;

public partial class PhotosSettingsView : UserControl
{
    private const int PreviewCount = 8;
    private const string PickedPhotos = "\0photos"; // the row that stands for all single photos

    private bool _ready;
    private int _refreshId;
    private int _frameIndex; // the frame being edited, from 0

    public PhotosSettingsView()
    {
        InitializeComponent();
        (Settings.Current.PhotoFrameCount switch { 2 => Frames2, 3 => Frames3, 4 => Frames4, 5 => Frames5, _ => Frames1 }).IsChecked = true;
        ShowFramePicker();
        ShowFrameOptions();
        _ready = true;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private PhotoFrame Frame => Settings.Current.Frame(_frameIndex);

    /// <summary>The "Editing frame" buttons: one per frame on the desktop.</summary>
    private void ShowFramePicker()
    {
        var count = Settings.Current.PhotoFrameCount;
        if (_frameIndex >= count) _frameIndex = 0;
        FramePicker.Children.Clear();
        for (var i = 0; i < count; i++)
        {
            var option = new RadioButton
            {
                Content = (i + 1).ToString(),
                Tag = i,
                GroupName = "EditingFrame",
                IsChecked = i == _frameIndex,
            };
            option.SetResourceReference(StyleProperty, "SegmentOption");
            option.Checked += Frame_Checked;
            FramePicker.Children.Add(option);
        }
        FramePickerPanel.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        FramesHint.Text = count == 1
            ? "Add more frames to put several photo widgets on the desktop."
            : "Each frame has its own photos and options. Pick a frame below to change it; it shows its number on the desktop.";
    }

    /// <summary>Puts the edited frame's options in the controls.</summary>
    private void ShowFrameOptions()
    {
        var wasReady = _ready;
        _ready = false; // setting the controls must not save anything
        var f = Frame;
        (f.Interval switch { <= 5 => Every5, <= 15 => Every15, <= 60 => Every60, _ => Every300 }).IsChecked = true;
        (f.Wide ? WideOption : SquareOption).IsChecked = true;
        ShuffleToggle.IsChecked = f.Shuffle;
        _ready = wasReady;
    }

    private void Frames_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton { Tag: string tag }) return;
        var count = int.Parse(tag);
        var added = count > Settings.Current.PhotoFrameCount;
        Settings.Current.PhotoFrameCount = count;
        Settings.Current.Save();
        if (added) _frameIndex = count - 1; // go straight to the new frame
        ShowFramePicker();
        ShowFrameOptions();
        _ = RefreshAsync();
        if (added)
        {
            // once the new frame's window has opened
            var index = _frameIndex;
            Dispatcher.BeginInvoke(() => PhotosView.IdentifyFrame(index), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
    }

    private void Frame_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: int index } || index == _frameIndex) return;
        _frameIndex = index;
        ShowFrameOptions();
        _ = RefreshAsync();
        PhotosView.IdentifyFrame(index);
    }

    /// <summary>A row in the list: one folder, or all the single photos together.</summary>
    public sealed record SourceRow(string Path, string Name, string Detail, string Glyph);

    /// <summary>Lists the folders and photos, and shows the first few photos as a preview.</summary>
    private async Task RefreshAsync()
    {
        var id = ++_refreshId;
        var sources = Frame.Sources.ToList();
        var size = (int)(80 * VisualTreeHelper.GetDpi(this).DpiScaleX);

        var (rows, photos, thumbs) = await Task.Run(() =>
        {
            var rows = new List<SourceRow>();
            foreach (var folder in sources.Where(Directory.Exists))
            {
                var n = PhotoLibrary.Count(folder);
                rows.Add(new SourceRow(folder, System.IO.Path.GetFileName(folder.TrimEnd('\\')) is { Length: > 0 } name ? name : folder,
                    n == 1 ? "1 photo" : $"{n} photos", ""));
            }
            var files = sources.Where(p => !Directory.Exists(p)).ToList();
            if (files.Count == 1)
                rows.Add(new SourceRow(files[0], System.IO.Path.GetFileName(files[0]), "Photo", ""));
            else if (files.Count > 1)
                rows.Add(new SourceRow(PickedPhotos, "Picked photos", $"{files.Count} photos", ""));

            var photos = PhotoLibrary.Expand(sources);
            var thumbs = photos.Take(PreviewCount).Select(p => PhotoLibrary.Load(p, size)).Where(b => b is not null).ToList();
            return (rows, photos, thumbs);
        });
        if (id != _refreshId) return; // a newer refresh is on its way

        Sources.ItemsSource = rows;
        TotalText.Text = photos.Count switch { 0 => "None yet", 1 => "1 photo", var n => $"{n} photos" };

        Preview.Children.Clear();
        for (var i = 0; i < thumbs.Count; i++)
        {
            var tile = new Grid { Height = 70, Margin = new Thickness(3) };
            tile.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new ImageBrush(thumbs[i]) { Stretch = Stretch.UniformToFill },
            });
            // "+12" on the last tile when there are more photos
            var more = photos.Count - thumbs.Count;
            if (i == thumbs.Count - 1 && more > 0)
            {
                tile.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(Color.FromArgb(0x88, 0x1C, 0x1B, 0x2E)),
                    Child = new TextBlock
                    {
                        Text = $"+{more}", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    },
                });
            }
            Preview.Children.Add(tile);
        }
        Preview.Visibility = thumbs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void AddPhotos_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pick photos",
            Multiselect = true,
            Filter = PhotoLibrary.DialogFilter,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        Settings.Current.UpdateFrame(_frameIndex, f => f.AddSources(dialog.FileNames));
        Settings.Current.Save();
        await RefreshAsync();
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Pick a folder of photos",
            Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        Settings.Current.UpdateFrame(_frameIndex, f => f.AddSources(dialog.FolderNames));
        Settings.Current.Save();
        await RefreshAsync();
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path }) return;
        var s = Settings.Current;
        if (path == PickedPhotos)
            s.UpdateFrame(_frameIndex, f => f.Sources.RemoveAll(p => !Directory.Exists(p))); // all the single photos
        else
            s.UpdateFrame(_frameIndex, f => f.Sources.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)));
        s.Save();
        await RefreshAsync();
    }

    private void Interval_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton { Tag: string tag }) return;
        Settings.Current.UpdateFrame(_frameIndex, f => f.Interval = int.Parse(tag));
        Settings.Current.Save();
    }

    private void Shape_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        Settings.Current.UpdateFrame(_frameIndex, f => f.Wide = WideOption.IsChecked == true);
        Settings.Current.Save();
    }

    private void Shuffle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        Settings.Current.UpdateFrame(_frameIndex, f => f.Shuffle = ShuffleToggle.IsChecked == true);
        Settings.Current.Save();
    }
}
