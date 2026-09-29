using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// แปลงภาพ <-> raw DXT bytes ตามที่ Unity TextureFormat ต้องการ
// รองรับ TextureFormat 10 (DXT1/BC1) และ 12 (DXT5/BC3) เท่านั้น เหมือนเดิม
static class TextureCodec
{
    public static CompressionFormat ToCompressionFormat(int unityTextureFormat) => unityTextureFormat switch
    {
        10 => CompressionFormat.Bc1,
        12 => CompressionFormat.Bc3,
        _ => throw new InvalidOperationException($"TextureFormat ไม่รองรับ: {unityTextureFormat}")
    };

    public static byte[] EncodeTextureFile(string imagePath, int targetWidth, int targetHeight, int unityFormat)
    {
        using var image = Image.Load<Rgba32>(imagePath);
        return EncodeTexture(image, targetWidth, targetHeight, unityFormat);
    }

    public static byte[] EncodeTexture(Image<Rgba32> image, int targetWidth, int targetHeight, int unityFormat)
    {
        if (image.Width != targetWidth || image.Height != targetHeight)
            throw new InvalidOperationException(
                $"Image size ไม่ตรง Target: {image.Width}x{image.Height} vs {targetWidth}x{targetHeight}");

        using var ms = new MemoryStream();
        var encoder = new BcEncoder
        {
            OutputOptions =
            {
                GenerateMipMaps = false,
                Format = ToCompressionFormat(unityFormat),
                FileFormat = OutputFileFormat.Dds
            }
        };
        encoder.EncodeToStream(image, ms);
        byte[] dds = ms.ToArray();

        const int ddsHeaderSize = 128;
        if (dds.Length <= ddsHeaderSize)
            throw new InvalidOperationException("Encode DDS ไม่สำเร็จ");

        byte[] raw = new byte[dds.Length - ddsHeaderSize];
        Buffer.BlockCopy(dds, ddsHeaderSize, raw, 0, raw.Length);
        return raw;
    }

    // raw DXT bytes (ตัดมาจาก .resS ตรงๆ ไม่มี header) -> ภาพที่ดู/crop/resize ได้
    public static Image<Rgba32> DecodeRaw(byte[] raw, int width, int height, int unityFormat)
    {
        var decoder = new BcDecoder();
        using var rawStream = new MemoryStream(raw);
        return decoder.DecodeRawToImageRgba32(rawStream, width, height, ToCompressionFormat(unityFormat));
    }
}
