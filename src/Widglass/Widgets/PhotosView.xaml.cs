using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Widglass.Services;

namespace Widglass.Widgets;

/// <summary>
/// Photos widget: the picked photos take turns filling the card, crossfading
/// with a slow zoom. Square, or wide for landscape photos.
/// </summary>
public partial class PhotosView : UserControl
{
    private static readonly TimeSpan FadeTime = TimeSpan.FromSeconds(1.2);

    private readonly DispatcherTimer _timer = new();
    private readonly Random _random = new();
    private List<string> _photos = [];
    private int _index = -1;
    private string _sourcesKey = "\0";
    private bool _wide;
    private Border _front;
    private bool _busy;
    private readonly int _frameIndex;

    /// <summary>Asks the frame with this number (from 0) to show its number for a moment, so you can find it.</summary>
    public static event Action<int>? Identify;

    public static void IdentifyFrame(int index) => Identify?.Invoke(index);

    public PhotosView(int frame = 0)
    {
        InitializeComponent();
        _frameIndex = frame;
        _front = LayerB;
        _wide = Frame.Wide;
        BadgeText.Text = (frame + 1).ToString();

        _timer.Tick += async (_, _) => await NextAsync();
        Loaded += async (_, _) =>
        {
            Settings.Changed += OnSettingsChanged;
            Identify += OnIdentify;
            await ReloadAsync();
        };
        Unloaded += (_, _) =>
        {
            Settings.Changed -= OnSettingsChanged;
            Identify -= OnIdentify;
            _timer.Stop();
        };
    }

    private PhotoFrame Frame => Settings.Current.Frame(_frameIndex);

    /// <summary>Shows the frame number in a glass badge, then fades it out.</summary>
    private void OnIdentify(int index)
    {
        if (index != _frameIndex) return;
        var show = new DoubleAnimationUsingKeyFrames();
        show.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.2))));
        show.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.8))));
        show.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.4))));
        Badge.BeginAnimation(OpacityProperty, show);
    }

    private async void OnSettingsChanged()
    {
        var frame = Frame;
        _timer.Interval = TimeSpan.FromSeconds(frame.Interval);

        if (frame.Wide != _wide)
        {
            _wide = frame.Wide;
            (Window.GetWindow(this) as WidgetWindow)?.SetWide(_wide);
            await ShowAsync(_index, fade: false); // again, decoded for the new size
        }
        await ReloadAsync();
    }

    /// <summary>Finds the photos again if the picked files/folders or the order changed.</summary>
    private async Task ReloadAsync()
    {
        var s = Frame;
        var key = string.Join("|", s.Sources) + "|" + s.Shuffle;
        if (key == _sourcesKey) return;
        _sourcesKey = key;

        var sources = s.Sources.ToList();
        var photos = await Task.Run(() => PhotoLibrary.Expand(sources));
        if (key != _sourcesKey) return; // changed again meanwhile
        if (s.Shuffle)
            photos = photos.OrderBy(_ => _random.Next()).ToList();
        _photos = photos;
        _index = -1;

        _timer.Stop();
        if (_photos.Count == 0)
        {
            ShowEmpty();
            return;
        }
        await NextAsync();
        _timer.Interval = TimeSpan.FromSeconds(s.Interval);
        if (_photos.Count > 1) _timer.Start();
    }

    private async Task NextAsync()
    {
        if (_photos.Count == 0) return;
        if (_index + 1 >= _photos.Count && _index >= 0)
            await RescanAsync(); // after a full round: pick up photos added to the folders since
        await ShowAsync((_index + 1) % _photos.Count, fade: _index >= 0);
    }

    /// <summary>Looks in the folders again; new photos join the next round (shuffled again if Shuffle is on).</summary>
    private async Task RescanAsync()
    {
        var s = Frame;
        var sources = s.Sources.ToList();
        var photos = await Task.Run(() => PhotoLibrary.Expand(sources));
        if (photos.Count == 0) return;
        if (s.Shuffle)
            photos = photos.OrderBy(_ => _random.Next()).ToList();
        _photos = photos;
        _index = photos.Count - 1; // so the next photo is the first of the new round, still with a fade
    }

    /// <summary>Shows photo number <paramref name="index"/>, skipping ones that can't be opened.</summary>
    private async Task ShowAsync(int index, bool fade)
    {
        if (_busy || _photos.Count == 0 || index < 0) return;
        _busy = true;
        try
        {
            var size = PixelSize();
            for (var tries = 0; tries < Math.Min(_photos.Count, 10); tries++)
            {
                var i = (index + tries) % _photos.Count;
                var path = _photos[i];
                var image = await Task.Run(() => PhotoLibrary.Load(path, size));
                if (image is null) continue;
                _index = i;
                Present(image, fade);
                return;
            }
            if (_index < 0)
            {
                MessageTitle.Text = "Can't open these photos";
                MessageText.Text = "Try other photos in settings.";
                ShowEmpty();
            }
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Puts the photo on the back layer, then fades it in over the current one with a slow zoom.</summary>
    private void Present(BitmapSource image, bool fade)
    {
        var next = _front == LayerA ? LayerB : LayerA;
        next.Background = new ImageBrush(image)
        {
            Stretch = Stretch.UniformToFill,
            RelativeTransform = new ScaleTransform { CenterX = 0.5, CenterY = 0.5 },
        };
        var scale = (ScaleTransform)((ImageBrush)next.Background).RelativeTransform;

        Panel.SetZIndex(next, 1);
        Panel.SetZIndex(_front, 0);
        next.BeginAnimation(OpacityProperty, new DoubleAnimation(fade ? 0 : 1, 1, fade ? FadeTime : TimeSpan.Zero));

        // Ken Burns: grow 6% while on screen
        var stay = TimeSpan.FromSeconds(Frame.Interval) + FadeTime;
        var grow = new DoubleAnimation(1, 1.06, stay) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

        _front = next;
        Rim.Visibility = Visibility.Visible;
        MessageView.Visibility = Visibility.Collapsed;
    }

    private void ShowEmpty()
    {
        LayerA.BeginAnimation(OpacityProperty, null);
        LayerB.BeginAnimation(OpacityProperty, null);
        LayerA.Opacity = LayerB.Opacity = 0;
        LayerA.Background = LayerB.Background = null;
        Rim.Visibility = Visibility.Collapsed;
        if (Frame.Sources.Count == 0)
        {
            MessageTitle.Text = "No photos yet";
            MessageText.Text = "Pick photos or a folder in settings.";
        }
        else if (_photos.Count == 0)
        {
            MessageTitle.Text = "No photos found";
            MessageText.Text = "The picked folders have no photos, or were moved.";
        }
        MessageView.Visibility = Visibility.Visible;
    }

    /// <summary>How many pixels the photo's short side needs to look sharp on this screen.</summary>
    private int PixelSize()
    {
        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var width = Frame.Wide ? WidgetWindow.WideWidth : WidgetWindow.SquareWidth;
        return (int)Math.Ceiling(width * dpi * 1.08); // a little extra for the zoom
    }
}
