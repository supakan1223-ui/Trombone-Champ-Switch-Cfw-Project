using AssetsTools.NET.Extra;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using static BundleTextureIO;

static class PatchBgCommand
{
    public static void PatchBackground(string targetBundlePath, string sourceTromBackgroundPath, string outputPath)
    {
        Console.WriteLine("=== PATCH BACKGROUND ===");
        Console.WriteLine($"Target : {targetBundlePath}");
        Console.WriteLine($"Source : {sourceTromBackgroundPath}");
        Console.WriteLine($"Output : {outputPath}");
        Console.WriteLine();

        if (!File.Exists(targetBundlePath))
            throw new FileNotFoundException("ไม่พบ target AssetBundle", targetBundlePath);
        if (!File.Exists(sourceTromBackgroundPath))
            throw new FileNotFoundException("ไม่พบ source .trombackground", sourceTromBackgroundPath);

        // 1. หา Main Background ของทั้งสองฝั่ง
        Console.WriteLine("[1/6] วิเคราะห์ Target Background...");
        var target = BackgroundAnalyzer.FindMain(targetBundlePath);
        Console.WriteLine($"  TARGET BG : {target.GameObject}");
        Console.WriteLine($"  Texture    : {target.Texture}");
        Console.WriteLine($"  Size       : {target.TexW}x{target.TexH}");
        Console.WriteLine($"  Rect       : {target.RectX:F0},{target.RectY:F0},{target.RectW:F0},{target.RectH:F0}");
        Console.WriteLine();

        Console.WriteLine("[2/6] วิเคราะห์ Source Background...");
        var source = BackgroundAnalyzer.FindMain(sourceTromBackgroundPath);
        Console.WriteLine($"  SOURCE BG : {source.GameObject}");
        Console.WriteLine($"  Texture    : {source.Texture}");
        Console.WriteLine($"  Size       : {source.TexW}x{source.TexH}");
        Console.WriteLine($"  Rect       : {source.RectX:F0},{source.RectY:F0},{source.RectW:F0},{source.RectH:F0}");

        // 2. อ่าน source texture
        Console.WriteLine();
        Console.WriteLine("[3/6] อ่าน Source Texture จาก .resS...");
        using Image<Rgba32> sourceImage = LoadTextureImage(sourceTromBackgroundPath, source);
        Console.WriteLine($"  decoded : {sourceImage.Width}x{sourceImage.Height}");

        // 3. ตัดเฉพาะ Sprite Rect ของ Source
        int sourceCropX = Math.Clamp((int)Math.Round(source.RectX), 0, source.TexW - 1);
        int sourceCropY = Math.Clamp(source.TexH - (int)Math.Round(source.RectY + source.RectH), 0, source.TexH - 1);
        int sourceCropW = Math.Clamp((int)Math.Round(source.RectW), 1, source.TexW - sourceCropX);
        int sourceCropH = Math.Clamp((int)Math.Round(source.RectH), 1, source.TexH - sourceCropY);

        Console.WriteLine($"  source crop : {sourceCropX},{sourceCropY} {sourceCropW}x{sourceCropH}");
        sourceImage.Mutate(ctx => ctx.Crop(new Rectangle(sourceCropX, sourceCropY, sourceCropW, sourceCropH)));

        // 4. จัด Source ให้เข้ากับ Target Sprite Rect
        Console.WriteLine();
        Console.WriteLine("[4/6] จัด Source ให้เข้ากับ Target Sprite Rect...");

        int targetRectW = Math.Max(1, (int)Math.Round(target.RectW));
        int targetRectH = Math.Max(1, (int)Math.Round(target.RectH));
        double scale = Math.Min((double)targetRectW / sourceImage.Width, (double)targetRectH / sourceImage.Height);
        int newWidth = Math.Max(1, (int)Math.Round(sourceImage.Width * scale));
        int newHeight = Math.Max(1, (int)Math.Round(sourceImage.Height * scale));

        Console.WriteLine($"  fit : {sourceImage.Width}x{sourceImage.Height} -> {newWidth}x{newHeight}");
        sourceImage.Mutate(ctx => ctx.Resize(newWidth, newHeight));

        using var finalImage = new Image<Rgba32>(target.TexW, target.TexH, Color.White);

        // Unity Sprite Rect ใช้พิกัดจากด้านล่าง, ImageSharp ใช้พิกัดจากด้านบน
        int targetX = (int)Math.Round(target.RectX + (targetRectW - newWidth) / 2.0);
        int targetY = target.TexH - (int)Math.Round(target.RectY + (targetRectH - newHeight) / 2.0) - newHeight;

        Console.WriteLine($"  target position : {targetX},{targetY}");
        finalImage.Mutate(ctx => ctx.DrawImage(sourceImage, new Point(targetX, targetY), 1f));

        // 5. Encode ตาม format ของ Target
        Console.WriteLine();
        Console.WriteLine("[5/6] Encode เป็น format เดิมของ Target...");
        byte[] encoded = TextureCodec.EncodeTexture(finalImage, target.TexW, target.TexH, target.TexFormat);
        Console.WriteLine($"  encoded : {encoded.Length:N0} bytes");

        // 6. เปิด Target ใหม่ แล้ว patch .resS
        Console.WriteLine();
        Console.WriteLine("[6/6] Patch Target AssetBundle...");
        PatchTargetTextureRaw(targetBundlePath, target.Texture, encoded, outputPath);

        Console.WriteLine();
        Console.WriteLine($"เสร็จแล้ว: {Path.GetFullPath(outputPath)}");
        Console.WriteLine("Foreground ของ Target ไม่ได้ถูกแก้");
    }

    // อ่าน + decode raw texture ของ candidate จาก bundle มาเป็นภาพที่ crop/resize ได้
    static Image<Rgba32> LoadTextureImage(string bundlePath, BgCandidate candidate)
    {
        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var bun = bunInst.file;

        var tex = FindTexture2D(manager, bun,
            idx => { try { return manager.LoadAssetsFileFromBundle(bunInst, idx, false); } catch { return null; } },
            candidate.Texture);

        if (tex == null)
            throw new InvalidOperationException($"ไม่พบ Source Texture '{candidate.Texture}'");

        if (tex.Format != 10 && tex.Format != 12)
            throw new InvalidOperationException($"Source Texture '{candidate.Texture}' ใช้ TextureFormat={tex.Format} ซึ่งยังไม่รองรับ");
        if (tex.MipCount != 1)
            throw new InvalidOperationException($"Source Texture '{candidate.Texture}' มี mipCount={tex.MipCount}; ตอนนี้รองรับเฉพาะ 1");

        byte[] raw = ReadTextureRaw(bun, tex);
        var image = TextureCodec.DecodeRaw(raw, tex.Width, tex.Height, tex.Format);

        Console.WriteLine($"  source raw : {raw.Length:N0} bytes");
        Console.WriteLine($"  source res : {tex.ResourceName}");
        Console.WriteLine($"  source off : {tex.Offset:N0}");
        Console.WriteLine($"  source size: {tex.Size:N0}");

        return image;
    }

    // แก้ raw bytes ของ target texture แล้วเขียน bundle ใหม่ทั้งไฟล์
    static void PatchTargetTextureRaw(string bundlePath, string textureName, byte[] encoded, string outputPath)
    {
        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var bun = bunInst.file;

        var tex = FindTexture2D(manager, bun,
            idx => { try { return manager.LoadAssetsFileFromBundle(bunInst, idx, false); } catch { return null; } },
            textureName);

        if (tex == null)
            throw new InvalidOperationException($"ไม่พบ Target Texture '{textureName}'");

        Console.WriteLine($"  target texture : {textureName}");
        Console.WriteLine($"  target size    : {tex.Width}x{tex.Height}");
        Console.WriteLine($"  target format  : {tex.Format}");
        Console.WriteLine($"  target raw     : {tex.Size:N0} bytes");
        Console.WriteLine($"  replacement    : {encoded.Length:N0} bytes");

        WriteTextureRaw(bun, tex, encoded);
        WriteBundle(bun, outputPath);
    }
}
