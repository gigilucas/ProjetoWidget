using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ProjetoWidget.Services;

namespace ProjetoWidget.Widgets;

/// <summary>Music widget: what the chosen app is playing, with playback controls.</summary>
public partial class MusicView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _refreshing;
    private NowPlaying? _last;

    public MusicView()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) =>
        {
            Settings.Changed += OnSettingsChanged;
            await MusicService.DetectAsync();
            UpdateIdleText();
            _timer.Start();
            await RefreshAsync();
        };
        Unloaded += (_, _) =>
        {
            Settings.Changed -= OnSettingsChanged;
            _timer.Stop();
        };
        Track.SizeChanged += (_, _) => UpdateProgress();
    }

    private static string AppId => MusicService.Normalize(Settings.Current.MusicApp);

    private async void OnSettingsChanged()
    {
        UpdateIdleText();
        await RefreshAsync();
    }

    private void UpdateIdleText()
    {
        // Open the chosen app, or the place to get it if it isn't installed
        var app = MusicService.FindApp(AppId)!;
        OpenAppText.Text = app.IsInstalled ? $"Open {app.Name}" : $"Get {app.Name}";
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            _last = await MusicService.GetNowPlayingAsync(AppId);
        }
        catch (Exception)
        {
            _last = null; // media controls unavailable for a moment
        }
        finally
        {
            _refreshing = false;
        }
        Show(_last);
    }

    private void Show(NowPlaying? now)
    {
        if (now is null)
        {
            PlayerView.Visibility = Visibility.Collapsed;
            IdleView.Visibility = Visibility.Visible;
            return;
        }

        TitleText.Text = now.Title;
        ArtistText.Text = now.Artist;
        SourceText.Text = now.SourceName;
        ArtImage.Background = now.Art is null ? null : new ImageBrush(now.Art) { Stretch = Stretch.UniformToFill };
        PlayPauseGlyph.Data = (Geometry)FindResource(now.IsPlaying ? "PauseIcon" : "PlayIcon");

        var active = (Brush)FindResource("LogoGradient");
        var soft = (Brush)FindResource("InkSoftBrush");
        ShuffleButton.IsEnabled = now.Shuffle is not null;
        ShuffleGlyph.Fill = now.Shuffle == true ? active : soft;
        ShuffleButton.Opacity = now.Shuffle is null ? 0.4 : 1;
        RepeatButton.IsEnabled = now.Repeat is not null;
        RepeatGlyph.Fill = now.Repeat is MediaPlaybackAutoRepeatModeValue.List or MediaPlaybackAutoRepeatModeValue.Track ? active : soft;
        RepeatOne.Visibility = now.Repeat == MediaPlaybackAutoRepeatModeValue.Track ? Visibility.Visible : Visibility.Collapsed;
        RepeatButton.Opacity = now.Repeat is null ? 0.4 : 1;
        PreviousButton.IsEnabled = now.CanPrevious;
        PreviousButton.Opacity = now.CanPrevious ? 1 : 0.4;
        NextButton.IsEnabled = now.CanNext;
        NextButton.Opacity = now.CanNext ? 1 : 0.4;
        Track.Cursor = now.CanSeek ? System.Windows.Input.Cursors.Hand : null;

        UpdateProgress();

        IdleView.Visibility = Visibility.Collapsed;
        PlayerView.Visibility = Visibility.Visible;
    }

    // ---------- Progress bar and seeking ----------

    private double? _dragRatio;          // set while the knob is being dragged
    private TimeSpan? _seekTarget;       // where we just jumped to...
    private DateTime _seekTime;          // ...and when, until the app reports the new spot

    /// <summary>Position to draw: the drag spot, a just-requested jump, or what the app reports.</summary>
    private TimeSpan ShownPosition()
    {
        if (_last is not { } n) return TimeSpan.Zero;
        if (_dragRatio is { } r) return n.Duration * r;
        if (_seekTarget is { } target && DateTime.Now - _seekTime < TimeSpan.FromSeconds(2))
            return target + (n.IsPlaying ? DateTime.Now - _seekTime : TimeSpan.Zero);
        _seekTarget = null;
        return n.Position;
    }

    private void UpdateProgress()
    {
        var duration = _last?.Duration ?? TimeSpan.Zero;
        var position = ShownPosition();
        var ratio = duration > TimeSpan.Zero ? Math.Clamp(position / duration, 0, 1) : 0;

        var width = Track.ActualWidth;
        Progress.Width = width * ratio;
        Knob.Margin = new Thickness(Math.Max(0, width * ratio - Knob.Width / 2), 0, 0, 0);
        Knob.Visibility = duration > TimeSpan.Zero ? Visibility.Visible : Visibility.Collapsed;

        ElapsedText.Text = Format(position);
        RemainingText.Text = duration > TimeSpan.Zero ? "-" + Format(duration - position) : "";
    }

    private double RatioAt(System.Windows.Input.MouseEventArgs e) =>
        Track.ActualWidth > 0 ? Math.Clamp(e.GetPosition(Track).X / Track.ActualWidth, 0, 1) : 0;

    private void Track_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true; // don't start dragging the widget
        if (_last is not { CanSeek: true }) return;
        _dragRatio = RatioAt(e);
        Track.CaptureMouse();
        UpdateProgress();
    }

    private void Track_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragRatio is null) return;
        _dragRatio = RatioAt(e);
        UpdateProgress();
    }

    private async void Track_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_dragRatio is null || _last is not { } n) return;
        var target = n.Duration * RatioAt(e);
        _dragRatio = null;
        Track.ReleaseMouseCapture();

        _seekTarget = target;
        _seekTime = DateTime.Now;
        UpdateProgress();
        await MusicService.SeekAsync(AppId, target);
    }

    private void Track_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragRatio is null) return;
        _dragRatio = null; // drag cancelled
        UpdateProgress();
    }

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    private async void PlayPause_Click(object sender, RoutedEventArgs e) { await MusicService.PlayPauseAsync(AppId); await RefreshAsync(); }
    private async void Previous_Click(object sender, RoutedEventArgs e) { await MusicService.PreviousAsync(AppId); await RefreshAsync(); }
    private async void Next_Click(object sender, RoutedEventArgs e) { await MusicService.NextAsync(AppId); await RefreshAsync(); }
    private async void Shuffle_Click(object sender, RoutedEventArgs e) { await MusicService.ToggleShuffleAsync(AppId); await RefreshAsync(); }
    private async void Repeat_Click(object sender, RoutedEventArgs e) { await MusicService.CycleRepeatAsync(AppId); await RefreshAsync(); }

    private void OpenApp_Click(object sender, RoutedEventArgs e) => MusicService.FindApp(AppId)?.Launch();
}
