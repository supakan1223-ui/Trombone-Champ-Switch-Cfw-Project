using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using BCnEncoder.ImageSharp;

// แปลง PNG เป็น raw DXT bytes ตาม TextureFormat ของ Unity
static class TextureCodec
{
    public static string FormatName(int format) => format switch
    {
        10 => "(DXT1/BC1)",
        12 => "(DXT5/BC3)",
        _ => ""
    };

    public static byte[] EncodeBackgroundTexture(
        string imagePath,
        int targetWidth,
        int targetHeight,
        int unityFormat,
        float rectX,
        float rectY,
        float rectW,
        float rectH)
    {
        using var source = Image.Load<Rgba32>(imagePath);

            int areaX = Math.Clamp((int)Math.Round(rectX), 0, Math.Max(0, targetWidth - 1));
            int areaY = Math.Clamp((int)Math.Round(rectY), 0, Math.Max(0, targetHeight - 1));
            int areaWidth = Math.Max(1, (int)Math.Round(rectW > 0 ? rectW : targetWidth));
            int areaHeight = Math.Max(1, (int)Math.Round(rectH > 0 ? rectH : targetHeight));

            areaWidth = Math.Min(areaWidth, targetWidth - areaX);
            areaHeight = Math.Min(areaHeight, targetHeight - areaY);

            // Preserve aspect ratio and fit the PNG inside the actual Sprite rectangle.
            double scale = Math.Min(
                (double)areaWidth / source.Width,
                (double)areaHeight / source.Height);

            int newWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            int newHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

            Console.WriteLine($"  Source  : {source.Width}x{source.Height}");
            Console.WriteLine($"  Target  : {targetWidth}x{targetHeight}");
            Console.WriteLine($"  Sprite  : {areaWidth}x{areaHeight} @ ({areaX},{areaY})");
            Console.WriteLine($"  Fit     : {newWidth}x{newHeight}");

            source.Mutate(ctx => ctx.Resize(newWidth, newHeight));

            using var canvas = new Image<Rgba32>(targetWidth, targetHeight, Color.Transparent);

            int x = areaX + (areaWidth - newWidth) / 2;
            int y = areaY + (areaHeight - newHeight) / 2;

            Console.WriteLine($"  Position: x={x}, y={y}");

            canvas.Mutate(ctx => ctx.DrawImage(source, new Point(x, y), 1f));

            // Unity's texture coordinate origin is opposite to the ImageSharp canvas orientation.
            canvas.Mutate(ctx => ctx.Flip(FlipMode.Vertical));

            var pixels = new ColorRgba32[targetWidth * targetHeight];
            canvas.ProcessPixelRows(accessor =>
            {
                for (int py = 0; py < targetHeight; py++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(py);
                    for (int px = 0; px < targetWidth; px++)
                    {
                        Rgba32 p = row[px];
                        pixels[py * targetWidth + px] = new ColorRgba32(p.R, p.G, p.B, p.A);
                    }
                }
            });

            return EncodeRaw(canvas, unityFormat);
    }

    public static Image<Rgba32> DecodeRaw(
        byte[] raw,
        int width,
        int height,
        int unityFormat)
    {
        if (unityFormat != 10 && unityFormat != 12)
            throw new InvalidOperationException(
                $"TextureFormat ไม่รองรับ: {unityFormat}");

        var compression = unityFormat == 10
            ? CompressionFormat.Bc1
            : CompressionFormat.Bc3;

        var decoder = new BcDecoder();

        using var stream = new MemoryStream(raw);

        return decoder.DecodeRawToImageRgba32(
            stream,
            width,
            height,
            compression);
    }

    public static byte[] EncodeRaw(
        Image<Rgba32> image,
        int unityFormat)
    {
        if (unityFormat != 10 && unityFormat != 12)
            throw new InvalidOperationException(
                $"TextureFormat ไม่รองรับ: {unityFormat}");

        var pixels = new ColorRgba32[image.Width * image.Height];

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);

                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 p = row[x];
                    pixels[y * image.Width + x] =
                        new ColorRgba32(p.R, p.G, p.B, p.A);
                }
            }
        });

        var encoder = new BcEncoder();

        encoder.OutputOptions.GenerateMipMaps = false;
        encoder.OutputOptions.Format = unityFormat == 10
            ? CompressionFormat.Bc1
            : CompressionFormat.Bc3;

        var memory =
            new CommunityToolkit.HighPerformance.ReadOnlyMemory2D<ColorRgba32>(
                pixels,
                image.Height,
                image.Width);

        return encoder.EncodeToRawBytes(memory)[0];
    }
}
