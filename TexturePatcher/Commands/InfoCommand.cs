using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

static class InfoCommand
{
    public static void PrintImageInfo(string imagePath)
    {
        using var image = Image.Load<Rgba32>(imagePath);
        long dxt1 = (long)((image.Width + 3) / 4) * ((image.Height + 3) / 4) * 8;
        long dxt5 = (long)((image.Width + 3) / 4) * ((image.Height + 3) / 4) * 16;
        long rgba = (long)image.Width * image.Height * 4;

        Console.WriteLine($"ภาพ: {Path.GetFileName(imagePath)}");
        Console.WriteLine($"ขนาด: {image.Width}x{image.Height}");
        Console.WriteLine($"  DXT1     -> {dxt1:N0} bytes");
        Console.WriteLine($"  DXT5     -> {dxt5:N0} bytes");
        Console.WriteLine($"  RGBA32   -> {rgba:N0} bytes");
    }
}
