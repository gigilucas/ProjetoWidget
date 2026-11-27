using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Widglass.Services;

/// <summary>Finds the photos in the picked files and folders and loads them small and upright.</summary>
public static class PhotoLibrary
{
    public static readonly string[] Extensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".jfif"];

    /// <summary>File filter for the "Add photos" dialog.</summary>
    public static string DialogFilter =>
        "Photos|" + string.Join(";", Extensions.Select(e => "*" + e)) + "|All files|*.*";

    public static bool IsPhoto(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Every photo in the sources: files as they are, folders with their subfolders.</summary>
    public static List<string> Expand(IEnumerable<string> sources)
    {
        var photos = new List<string>();
        foreach (var source in sources)
        {
            try
            {
                if (Directory.Exists(source))
                    photos.AddRange(Directory.EnumerateFiles(source, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                        .Where(IsPhoto)
                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
                else if (File.Exists(source) && IsPhoto(source))
                    photos.Add(source);
            }
            catch (Exception)
            {
                // A folder that went away or can't be read: skip it.
            }
        }
        return photos.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Number of photos in one source (a file counts as 1).</summary>
    public static int Count(string source) => Expand([source]).Count;

    /// <summary>
    /// Loads a photo scaled down so its short side is about <paramref name="size"/> pixels,
    /// turned the right way up (phone photos store their rotation separately). Frozen, so any thread can use it.
    /// Null if it can't be read.
    /// </summary>
    public static BitmapSource? Load(string path, int size)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var rotation = Rotation(frame);
            var (w, h) = (frame.PixelWidth, frame.PixelHeight);

            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            // Decode just big enough for the widget: the short side becomes `size`
            if (w > 0 && h > 0 && Math.Min(w, h) > size)
            {
                if (w <= h) image.DecodePixelWidth = size;
                else image.DecodePixelHeight = size;
            }
            image.EndInit();

            BitmapSource result = image;
            if (rotation != 0)
                result = new TransformedBitmap(image, new RotateTransform(rotation));
            result.Freeze();
            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The EXIF orientation as degrees to turn clockwise.</summary>
    private static double Rotation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata meta && meta.GetQuery("System.Photo.Orientation") is ushort orientation)
                return orientation switch { 3 => 180, 6 => 90, 8 => 270, _ => 0 };
        }
        catch (Exception)
        {
            // No readable orientation (PNG, BMP...): leave it as is.
        }
        return 0;
    }
}
