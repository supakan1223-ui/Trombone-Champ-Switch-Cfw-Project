using AssetsTools.NET;
using AssetsTools.NET.Extra;

static class HideCommand
{
    // เก็บ renderer ของ GameObject ที่ชื่อ keepGameObject ไว้ ปิด renderer ที่เปิดอยู่ตัวอื่นทั้งหมด
    // (ต่างจาก RendererHider.HideOtherSpriteRenderers ที่เช็คจากชื่อ Texture2D และทำเฉพาะ SpriteRenderer)
    public static void HideOthers(string bundlePath, string keepGameObject, string outputPath)
    {
        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var bun = bunInst.file;

        var modified = new List<AssetsFileInstance>();
        bool keepFound = false;
        int totalHidden = 0;

        var rendererTypes = new[]
        {
            AssetClassID.SpriteRenderer,
            AssetClassID.MeshRenderer,
            AssetClassID.ParticleSystemRenderer
        };

        int idx = 0;
        while (idx < bun.GetAllFileNames().Count)
        {
            AssetsFileInstance? afileInst = null;
            try { afileInst = manager.LoadAssetsFileFromBundle(bunInst, idx, false); } catch { }
            idx++;
            if (afileInst == null) continue;

            var goNames = new Dictionary<long, string>();
            foreach (var go in afileInst.file.GetAssetsOfType(AssetClassID.GameObject))
                goNames[go.PathId] = manager.GetBaseField(afileInst, go)["m_Name"].AsString;

            int hiddenHere = 0;
            foreach (var type in rendererTypes)
            {
                foreach (var info in afileInst.file.GetAssetsOfType(type))
                {
                    var field = manager.GetBaseField(afileInst, info);
                    long goId = field["m_GameObject"]["m_PathID"].AsLong;
                    string goName = goNames.TryGetValue(goId, out var n) ? n : "(unknown)";

                    if (goName == keepGameObject)
                    {
                        keepFound = true;
                        Console.WriteLine($"  KEEP  {type,-22} on '{goName}'");
                        continue;
                    }

                    RendererHider.SetDisabled(field["m_Enabled"]);
                    info.Replacer = new ContentReplacerFromBuffer(field.WriteToByteArray());
                    hiddenHere++;
                    Console.WriteLine($"  HIDE  {type,-22} on '{goName}'");
                }
            }

            if (hiddenHere > 0)
            {
                totalHidden += hiddenHere;
                modified.Add(afileInst);
            }
        }

        if (!keepFound)
            throw new InvalidOperationException($"ไม่พบ renderer ของ GameObject '{keepGameObject}' — ยกเลิกเพื่อไม่ให้ซ่อนทุกอย่าง");

        foreach (var inst in modified)
        {
            string temp = outputPath + ".tmp_cab";
            using (var w = new AssetsFileWriter(temp))
                inst.file.Write(w, 0);
            byte[] data = File.ReadAllBytes(temp);
            File.Delete(temp);

            var cabDir = bun.BlockAndDirInfo.DirectoryInfos.FirstOrDefault(d => d.Name == inst.name)
                ?? throw new InvalidOperationException($"ไม่พบ directory entry ของ {inst.name}");
            cabDir.Replacer = new ContentReplacerFromBuffer(data);
        }

        BundleTextureIO.WriteBundle(bun, outputPath);

        Console.WriteLine();
        Console.WriteLine($"ซ่อนทั้งหมด {totalHidden} renderer -> {Path.GetFullPath(outputPath)}");
    }
}
