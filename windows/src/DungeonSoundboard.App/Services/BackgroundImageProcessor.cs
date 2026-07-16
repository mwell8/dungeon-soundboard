using Avalonia.Media.Imaging;
using SkiaSharp;

namespace DungeonSoundboard.App.Services;

internal static class BackgroundImageProcessor
{
    private const double MinimumBlurRadius = 0.01;
    private const int BlurRadiusPrecision = 2;
    private static readonly object CacheLock = new();
    private static BlurCacheEntry? cachedBlur;
    private static int cacheMissCountForTesting;

    public static Bitmap LoadBitmap(string imagePath, double blurRadius)
    {
        if (!ShouldBlur(blurRadius))
        {
            return new Bitmap(imagePath);
        }

        return new Bitmap(new MemoryStream(EncodeBlurredPng(imagePath, blurRadius)));
    }

    internal static byte[] EncodeBlurredPng(string imagePath, double blurRadius)
    {
        var cacheKey = CreateCacheKey(imagePath, blurRadius);

        lock (CacheLock)
        {
            if (cachedBlur?.Key == cacheKey)
            {
                return cachedBlur.Bytes.ToArray();
            }
        }

        var bytes = EncodeBlurredPngUncached(imagePath, cacheKey.BlurRadius);

        lock (CacheLock)
        {
            cachedBlur = new BlurCacheEntry(cacheKey, bytes);
            cacheMissCountForTesting++;
        }

        return bytes.ToArray();
    }

    internal static int CacheMissCountForTesting
    {
        get
        {
            lock (CacheLock)
            {
                return cacheMissCountForTesting;
            }
        }
    }

    internal static void ClearCacheForTesting()
    {
        lock (CacheLock)
        {
            cachedBlur = null;
            cacheMissCountForTesting = 0;
        }
    }

    private static byte[] EncodeBlurredPngUncached(string imagePath, double blurRadius)
    {
        using var sourceBitmap = SKBitmap.Decode(imagePath)
            ?? throw new InvalidOperationException($"Failed to decode background image: {imagePath}");
        using var sourceImage = SKImage.FromBitmap(sourceBitmap);
        using var surface = SKSurface.Create(new SKImageInfo(
            sourceBitmap.Width,
            sourceBitmap.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));

        if (surface is null)
        {
            throw new InvalidOperationException("Failed to create background image blur surface.");
        }

        using var paint = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateBlur(
                (float)Math.Max(0, blurRadius),
                (float)Math.Max(0, blurRadius),
                SKShaderTileMode.Clamp)
        };

        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(sourceImage, 0, 0, paint);
        surface.Canvas.Flush();

        using var blurredImage = surface.Snapshot();
        using var encoded = blurredImage.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Failed to encode blurred background image.");

        return encoded.ToArray();
    }

    private static bool ShouldBlur(double blurRadius)
    {
        return double.IsFinite(blurRadius) && blurRadius > MinimumBlurRadius;
    }

    private static BlurCacheKey CreateCacheKey(string imagePath, double blurRadius)
    {
        var fileInfo = new FileInfo(imagePath);
        var exists = fileInfo.Exists;

        return new BlurCacheKey(
            fileInfo.FullName,
            exists ? fileInfo.LastWriteTimeUtc.Ticks : 0,
            exists ? fileInfo.Length : -1,
            NormalizeBlurRadius(blurRadius));
    }

    private static double NormalizeBlurRadius(double blurRadius)
    {
        if (!double.IsFinite(blurRadius) || blurRadius < 0)
        {
            return 0;
        }

        return Math.Round(blurRadius, BlurRadiusPrecision, MidpointRounding.AwayFromZero);
    }

    private sealed record BlurCacheEntry(BlurCacheKey Key, byte[] Bytes);

    private sealed record BlurCacheKey(string ImagePath, long LastWriteUtcTicks, long Length, double BlurRadius);
}
