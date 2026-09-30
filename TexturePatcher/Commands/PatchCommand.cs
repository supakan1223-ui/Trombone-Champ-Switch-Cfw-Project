using AssetsTools.NET;
using AssetsTools.NET.Extra;
using static AssetField;

static class PatchCommand
{
    public static void PatchBundle(
        string bundlePath,
        string textureName,
        string imagePath,
        string outputPath)
    {
        if (!File.Exists(bundlePath))
            throw new FileNotFoundException("ไม่พบ AssetBundle", bundlePath);
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("ไม่พบภาพ", imagePath);

        Console.WriteLine("[1/5] เปิด AssetBundle...");
        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var bun = bunInst.file;

        if (bun.DataIsCompressed)
            throw new InvalidOperationException(
                "AssetBundle ยังอยู่ในสถานะ compressed หลัง LoadBundleFile(..., true)");

        Console.WriteLine("[2/5] หา Texture2D...");
        var texture = BundleTextureIO.FindTexture2D(manager, bunInst, textureName);

        if (texture == null)
            throw new InvalidOperationException(
                $"ไม่พบ Texture2D ชื่อ '{textureName}' ใน AssetBundle");

        Console.WriteLine($"  Texture : {textureName}");
        Console.WriteLine($"  Size    : {texture.Width}x{texture.Height}");
        Console.WriteLine($"  Format  : TextureFormat={texture.Format} {TextureCodec.FormatName(texture.Format)}");
        Console.WriteLine($"  MipCount: {texture.MipCount}");
        Console.WriteLine($"  Resource: {texture.ResourceName}");
        Console.WriteLine($"  Offset  : {texture.Offset:N0}");
        Console.WriteLine($"  Size    : {texture.Size:N0}");

        if (texture.Format != 10 && texture.Format != 12)
            throw new InvalidOperationException(
                $"รองรับ DXT1 (10) หรือ DXT5 (12) เท่านั้น; พบ {texture.Format}");

        if (texture.MipCount != 1)
            throw new InvalidOperationException(
                $"Texture นี้มี mipCount={texture.MipCount}; เวอร์ชันนี้รองรับ mipCount=1 เท่านั้น");

        Console.WriteLine("[3/5] Encode ภาพ...");
        byte[] encoded = TextureCodec.EncodeBackgroundTexture(
            imagePath,
            texture.Width,
            texture.Height,
            texture.Format,
            0, 0,
            texture.Width,
            texture.Height);

        Console.WriteLine($"  ได้ {encoded.Length:N0} bytes (ต้องการ {texture.Size:N0} bytes)");

        if (encoded.LongLength != texture.Size)
            throw new InvalidOperationException(
                $"ขนาด encoded ไม่ตรง: ได้ {encoded.Length}, ต้องการ {texture.Size}");

        var resourceDir =
            BundleTextureIO.ResolveResourceDirectory(bun, texture.ResourceName);

        if ((long)texture.Offset + texture.Size > resourceDir.DecompressedSize)
            throw new InvalidOperationException(
                $"Texture range เกิน resource: offset={texture.Offset}, size={texture.Size}, resourceSize={resourceDir.DecompressedSize}");

        Console.WriteLine("[4/5] แก้ raw texture ใน .resS...");
        byte[] resourceBytes =
            BundleTextureIO.ReadBundleDirectoryBytes(bun, resourceDir);

        Buffer.BlockCopy(
            encoded,
            0,
            resourceBytes,
            checked((int)texture.Offset),
            encoded.Length);

        resourceDir.Replacer =
            new ContentReplacerFromBuffer(resourceBytes);

        Console.WriteLine("[5/5] เขียน AssetBundle...");

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        using (var writer = new AssetsFileWriter(outputPath))
            bun.Write(writer);

        Console.WriteLine($"เสร็จแล้ว: {Path.GetFullPath(outputPath)}");
    }
}
