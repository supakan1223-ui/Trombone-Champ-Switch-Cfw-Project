using AssetsTools.NET;
using AssetsTools.NET.Extra;
using static AssetField;

// ทุกอย่างที่เกี่ยวกับ "หา Texture2D ตามชื่อ" + "อ่าน/เขียน raw texture bytes ใน .resS"
// เดิมโค้ดชุดนี้ถูกเขียนซ้ำคำต่อคำใน PatchBundle, LoadTextureImage, PatchTargetTextureRaw
static class BundleTextureIO
{
    // ผลการค้นหา Texture2D 1 ตัว พร้อมข้อมูลตำแหน่งใน .resS ที่ต้องใช้ patch/decode
    public sealed record TextureHandle(
        AssetsFileInstance AssetsFile,
        AssetFileInfo Info,
        AssetTypeValueField Base,
        int Width, int Height, int Format, int MipCount,
        uint Offset, uint Size, string ResourceName);

    // ไล่ทุก assets file ใน bundle หา Texture2D ที่ชื่อ name ตรงเป๊ะ (Ordinal)
    // loadAssetsFile ให้ผู้เรียกส่งเข้ามาเอง เพราะ bunInst (จาก manager.LoadBundleFile) ไม่มีชื่อ type ที่ตายตัวในสโคปนี้
    public static TextureHandle? FindTexture2D(
        AssetsManager manager,
        AssetBundleFile bun,
        Func<int, AssetsFileInstance?> loadAssetsFile,
        string textureName)
    {
        int idx = 0;
        while (idx < bun.GetAllFileNames().Count)
        {
            AssetsFileInstance? afile = null;
            try { afile = loadAssetsFile(idx); } catch { }
            idx++;
            if (afile == null) continue;

            foreach (var texInfo in afile.file.GetAssetsOfType(AssetClassID.Texture2D))
            {
                var texBase = manager.GetBaseField(afile, texInfo);
                string name = SafeS(texBase["m_Name"]);
                if (!string.Equals(name, textureName, StringComparison.Ordinal)) continue;

                var streamData = texBase["m_StreamData"];
                uint offset = streamData["offset"].AsUInt;
                uint size = streamData["size"].AsUInt;
                string resourcePath = streamData["path"].AsString;
                string resourceName = resourcePath.Replace('\\', '/').Split('/').Last();

                return new TextureHandle(
                    afile, texInfo, texBase,
                    SafeI(texBase["m_Width"]), SafeI(texBase["m_Height"]),
                    SafeI(texBase["m_TextureFormat"]), SafeI(texBase["m_MipCount"]),
                    offset, size, resourceName);
            }
        }
        return null;
    }

    // หา directory entry ใน bundle ที่ชื่อตรงกับ resourceName (เผื่อ path มี prefix ต่างกัน จึงเช็ค EndsWith ด้วย)
    public static AssetBundleDirectoryInfo ResolveResourceDirectory(AssetBundleFile bun, string resourceName)
    {
        var dir = bun.BlockAndDirInfo.DirectoryInfos
            .FirstOrDefault(d => string.Equals(d.Name, resourceName, StringComparison.Ordinal));

        dir ??= bun.BlockAndDirInfo.DirectoryInfos
            .FirstOrDefault(d => d.Name.EndsWith(resourceName, StringComparison.Ordinal));

        return dir ?? throw new InvalidOperationException($"ไม่พบ resource entry '{resourceName}' ใน AssetBundle");
    }

    public static byte[] ReadBundleDirectoryBytes(AssetBundleFile bundle, AssetBundleDirectoryInfo dir)
    {
        if (dir.Replacer != null)
            throw new InvalidOperationException($"Resource '{dir.Name}' มี Replacer อยู่ก่อนแล้ว");

        bundle.DataReader.Position = dir.Offset;
        byte[] data = new byte[checked((int)dir.DecompressedSize)];
        int total = 0;
        while (total < data.Length)
        {
            int read = bundle.DataReader.BaseStream.Read(data, total, data.Length - total);
            if (read <= 0) throw new EndOfStreamException($"อ่าน resource '{dir.Name}' ไม่ครบ");
            total += read;
        }
        return data;
    }

    // อ่าน raw compressed bytes (DXT1/DXT5) ของ texture ตรงๆ จาก .resS ยังไม่ decode เป็นภาพ
    public static byte[] ReadTextureRaw(AssetBundleFile bun, TextureHandle tex)
    {
        var resourceDir = ResolveResourceDirectory(bun, tex.ResourceName);
        byte[] resourceBytes = ReadBundleDirectoryBytes(bun, resourceDir);

        if ((long)tex.Offset + tex.Size > resourceBytes.LongLength)
            throw new InvalidOperationException("Texture range เกิน resource");

        byte[] raw = new byte[tex.Size];
        Buffer.BlockCopy(resourceBytes, checked((int)tex.Offset), raw, 0, checked((int)tex.Size));
        return raw;
    }

    // แก้ raw compressed bytes ของ texture ตรงๆ ใน .resS (encoded.Length ต้องเท่ากับ tex.Size เป๊ะ)
    public static void WriteTextureRaw(AssetBundleFile bun, TextureHandle tex, byte[] encoded)
    {
        if (encoded.LongLength != tex.Size)
            throw new InvalidOperationException(
                $"ขนาด encoded ไม่ตรง: ได้ {encoded.Length:N0}, ต้องการ {tex.Size:N0}");

        var resourceDir = ResolveResourceDirectory(bun, tex.ResourceName);

        if ((long)tex.Offset + tex.Size > resourceDir.DecompressedSize)
            throw new InvalidOperationException(
                $"Texture range เกิน resource: offset={tex.Offset}, size={tex.Size}, resourceSize={resourceDir.DecompressedSize}");

        byte[] resourceBytes = ReadBundleDirectoryBytes(bun, resourceDir);
        Buffer.BlockCopy(encoded, 0, resourceBytes, checked((int)tex.Offset), encoded.Length);
        resourceDir.Replacer = new ContentReplacerFromBuffer(resourceBytes);

        Console.WriteLine($"  patched: {tex.ResourceName} @ {tex.Offset:N0} + {tex.Size:N0}");
    }

    public static void WriteBundle(AssetBundleFile bun, string outputPath)
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
        using var writer = new AssetsFileWriter(outputPath);
        bun.Write(writer);
    }
}
