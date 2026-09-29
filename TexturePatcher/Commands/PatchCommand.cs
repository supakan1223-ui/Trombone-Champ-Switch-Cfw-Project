using AssetsTools.NET.Extra;
using static BundleTextureIO;

static class PatchCommand
{
    public static void PatchBundle(string bundlePath, string textureName, string imagePath, string outputPath)
    {
        if (!File.Exists(bundlePath)) throw new FileNotFoundException("ไม่พบ AssetBundle", bundlePath);
        if (!File.Exists(imagePath)) throw new FileNotFoundException("ไม่พบภาพ", imagePath);

        Console.WriteLine("[1/5] เปิด AssetBundle...");
        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var bun = bunInst.file;

        if (bun.DataIsCompressed)
            throw new InvalidOperationException("AssetBundle ยังอยู่ในสถานะ compressed หลัง LoadBundleFile(..., true)");

        Console.WriteLine("[2/5] หา Texture2D...");
        var tex = FindTexture2D(manager, bun,
            idx => { try { return manager.LoadAssetsFileFromBundle(bunInst, idx, false); } catch { return null; } },
            textureName);

        if (tex == null)
            throw new InvalidOperationException($"ไม่พบ Texture2D ชื่อ '{textureName}' ใน AssetBundle");

        Console.WriteLine($"  Texture : {textureName}");
        Console.WriteLine($"  Size    : {tex.Width}x{tex.Height}");
        Console.WriteLine($"  Format  : TextureFormat={tex.Format} {(tex.Format == 12 ? "(DXT5/BC3)" : tex.Format == 10 ? "(DXT1/BC1)" : "")}");
        Console.WriteLine($"  MipCount: {tex.MipCount}");
        Console.WriteLine($"  Resource: {tex.ResourceName}");
        Console.WriteLine($"  Offset  : {tex.Offset:N0}");
        Console.WriteLine($"  Size    : {tex.Size:N0}");

        if (tex.Format != 10 && tex.Format != 12)
            throw new InvalidOperationException($"รองรับ DXT1 (10) หรือ DXT5 (12) เท่านั้น; พบ {tex.Format}");
        if (tex.MipCount != 1)
            throw new InvalidOperationException($"Texture นี้มี mipCount={tex.MipCount}; เวอร์ชันนี้รองรับ mipCount=1 เท่านั้น");

        Console.WriteLine("[3/5] Encode ภาพเป็น DXT5...");
        byte[] encoded = TextureCodec.EncodeTextureFile(imagePath, tex.Width, tex.Height, tex.Format);
        Console.WriteLine($"  ได้ {encoded.Length:N0} bytes (ต้องการ {tex.Size:N0} bytes)");

        Console.WriteLine("[4/5] แก้เฉพาะช่วง texture ใน .resS ภายใน AssetBundle...");
        WriteTextureRaw(bun, tex, encoded);

        Console.WriteLine("[5/5] ปิด SpriteRenderer ของ BG เดิม...");
        RendererHider.HideOtherSpriteRenderers(manager, tex.AssetsFile, textureName);

        Console.WriteLine("[6/6] เขียน AssetBundle ใหม่...");
        WriteBundle(bun, outputPath);

        Console.WriteLine();
        Console.WriteLine($"เสร็จแล้ว: {Path.GetFullPath(outputPath)}");
        Console.WriteLine("Texture2D metadata เดิมไม่ได้ถูกเปลี่ยน");
        Console.WriteLine("แก้ raw texture bytes และปิด SpriteRenderer อื่นแล้ว");
    }
}
