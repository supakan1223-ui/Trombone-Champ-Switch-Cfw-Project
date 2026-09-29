using AssetsTools.NET.Extra;

static class AnalyzeCommand
{
    public static void AnalyzeBackground(string bundlePath)
    {
        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var bun = bunInst.file;

        var cands = BackgroundAnalyzer.GatherCandidates(manager, bun,
            idx => { try { return manager.LoadAssetsFileFromBundle(bunInst, idx, false); } catch { return null; } });

        if (cands.Count == 0)
        {
            Console.WriteLine("ไม่พบ SpriteRenderer -> Sprite -> Texture2D ใน bundle นี้");
            return;
        }

        BackgroundAnalyzer.ScoreCandidates(cands);
        var ordered = cands.OrderByDescending(c => c.Score).ToList();
        BackgroundAnalyzer.PrintReport(ordered);
    }

    // ใช้ระบบ analyze เดียวกันกับ .trombackground (มันคือ AssetBundle เหมือนกัน)
    public static void AnalyzeBackgroundSource(string bundlePath)
    {
        if (!File.Exists(bundlePath))
            throw new FileNotFoundException("ไม่พบ .trombackground", bundlePath);

        Console.WriteLine("=== BACKGROUND SOURCE ===");
        Console.WriteLine("File: " + bundlePath);
        Console.WriteLine();

        AnalyzeBackground(bundlePath);
    }
}
