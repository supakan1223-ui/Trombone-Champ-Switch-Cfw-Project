using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static class HideCommand
{
    public static void SetDisabled(AssetTypeValueField f)
    {
        try { f.AsBool = false; return; } catch { }
        try { f.AsInt = 0; return; } catch { }
        f.AsUInt = 0;
    }

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
            try { afileInst = manager.LoadAssetsFileFromBundle(bunInst, idx, false); }
            catch { }
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

                    SetDisabled(field["m_Enabled"]);
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
            using (var w = new AssetsFileWriter(temp)) inst.file.Write(w, 0);
            byte[] data = File.ReadAllBytes(temp);
            File.Delete(temp);

            var cabDir = bun.BlockAndDirInfo.DirectoryInfos.FirstOrDefault(d => d.Name == inst.name)
                ?? throw new InvalidOperationException($"ไม่พบ directory entry ของ {inst.name}");
            cabDir.Replacer = new ContentReplacerFromBuffer(data);
        }

        if (File.Exists(outputPath)) File.Delete(outputPath);
        using (var writer = new AssetsFileWriter(outputPath)) bun.Write(writer);

        Console.WriteLine();
        Console.WriteLine($"ซ่อนทั้งหมด {totalHidden} renderer -> {Path.GetFullPath(outputPath)}");
    }

    public static void HideOthersExcept(string bundlePath, HashSet<string> keepGameObjects, string outputPath)
    {
        var manager = new AssetsManager();
        BundleFileInstance? bunInst = null;

        try
        {
            bunInst = manager.LoadBundleFile(bundlePath, true);
            var bun = bunInst.file;
            var modified = new List<AssetsFileInstance>();
            var foundKeep = new HashSet<string>(StringComparer.Ordinal);
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
                try { afileInst = manager.LoadAssetsFileFromBundle(bunInst, idx, false); }
                catch { }
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

                        if (keepGameObjects.Contains(goName))
                        {
                            foundKeep.Add(goName);
                            Console.WriteLine($"  KEEP  {type,-22} on '{goName}'");
                            continue;
                        }

                        SetDisabled(field["m_Enabled"]);
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

            var missing = keepGameObjects.Where(x => !foundKeep.Contains(x)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"ไม่พบ renderer ของ GameObject ที่ต้อง KEEP: {string.Join(", ", missing)} — ยกเลิกเพื่อไม่ให้ซ่อนทุกอย่าง");

            foreach (var inst in modified)
            {
                string temp = outputPath + ".tmp_cab";
                if (File.Exists(temp)) File.Delete(temp);
                using (var w = new AssetsFileWriter(temp)) inst.file.Write(w, 0);
                byte[] data = File.ReadAllBytes(temp);
                File.Delete(temp);
                var cabDir = bun.BlockAndDirInfo.DirectoryInfos.FirstOrDefault(d => d.Name == inst.name)
                    ?? throw new InvalidOperationException($"ไม่พบ directory entry ของ {inst.name}");
                cabDir.Replacer = new ContentReplacerFromBuffer(data);
            }

            if (File.Exists(outputPath)) File.Delete(outputPath);
            using (var writer = new AssetsFileWriter(outputPath)) bun.Write(writer);
            Console.WriteLine();
            Console.WriteLine($"ซ่อนทั้งหมด {totalHidden} renderer -> {Path.GetFullPath(outputPath)}");
        }
        finally
        {
            try
            {
                if (bunInst != null)
                    manager.UnloadBundleFile(bunInst);
                else
                    manager.UnloadAllBundleFiles();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] ปิด AssetBundle หลัง Hide ไม่สำเร็จ: {ex.Message}");
            }
        }
    }
}
