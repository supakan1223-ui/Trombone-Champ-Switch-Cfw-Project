using AssetsTools.NET;
using AssetsTools.NET.Extra;
using static AssetField;

// ทุกอย่างที่เกี่ยวกับ Texture2D และ raw texture bytes ใน .resS
static class BundleTextureIO
{
    public sealed record TextureHandle(
        AssetsFileInstance AssetsFile,
        AssetFileInfo Info,
        AssetTypeValueField Base,
        int Width, int Height, int Format, int MipCount,
        uint Offset, uint Size, string ResourceName);

    public static TextureHandle? FindTexture2D(
        AssetsManager manager,
        BundleFileInstance bunInst,
        string textureName)
    {
        int idx = 0;
        while (idx < bunInst.file.GetAllFileNames().Count)
        {
            AssetsFileInstance? afile = null;
            try { afile = manager.LoadAssetsFileFromBundle(bunInst, idx, false); }
            catch { }
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

    public static AssetBundleDirectoryInfo ResolveResourceDirectory(
        AssetBundleFile bun, string resourceName)
    {
        var dir = bun.BlockAndDirInfo.DirectoryInfos
            .FirstOrDefault(d => string.Equals(d.Name, resourceName, StringComparison.Ordinal));

        dir ??= bun.BlockAndDirInfo.DirectoryInfos
            .FirstOrDefault(d => d.Name.EndsWith(resourceName, StringComparison.Ordinal));

        return dir ?? throw new InvalidOperationException(
            $"ไม่พบ resource entry '{resourceName}' ใน AssetBundle");
    }

    public static byte[] ReadBundleDirectoryBytes(
        AssetBundleFile bundle,
        AssetBundleDirectoryInfo dir)
    {
        if (dir.Replacer != null)
            throw new InvalidOperationException(
                $"Resource '{dir.Name}' มี Replacer อยู่ก่อนแล้ว");

        bundle.DataReader.Position = dir.Offset;
        byte[] data = new byte[checked((int)dir.DecompressedSize)];

        int total = 0;
        while (total < data.Length)
        {
            int read = bundle.DataReader.BaseStream.Read(
                data, total, data.Length - total);

            if (read <= 0)
                throw new EndOfStreamException(
                    $"อ่าน resource '{dir.Name}' ไม่ครบ");

            total += read;
        }

        return data;
    }

    public static byte[] ReadTextureRaw(
        AssetBundleFile bun,
        TextureHandle tex)
    {
        var resourceDir = ResolveResourceDirectory(bun, tex.ResourceName);
        byte[] resourceBytes = ReadBundleDirectoryBytes(bun, resourceDir);

        if ((long)tex.Offset + tex.Size > resourceBytes.LongLength)
            throw new InvalidOperationException("Texture range เกิน resource");

        byte[] raw = new byte[tex.Size];
        Buffer.BlockCopy(
            resourceBytes,
            checked((int)tex.Offset),
            raw,
            0,
            checked((int)tex.Size));

        return raw;
    }
}
