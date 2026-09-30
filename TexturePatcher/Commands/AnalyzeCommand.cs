using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static class AnalyzeCommand
{
    public static void AnalyzeBackground(string bundlePath)
    {
        if (!File.Exists(bundlePath))
            throw new FileNotFoundException("ไม่พบ AssetBundle", bundlePath);

        Console.WriteLine("[1/1] วิเคราะห์ Background candidates...");

        var candidates = BackgroundAnalyzer.FindBackgroundTargets(bundlePath)
            .Where(c => c.Enabled)
            .OrderByDescending(c => c.Score)
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException(
                "ไม่พบ Enabled Background Texture2D จาก SpriteRenderer ใน AssetBundle");

        double maxScore = candidates[0].Score;
        const double scoreTolerance = 5.0;

        var selected = candidates
            .Where(c => c.Score >= maxScore - scoreTolerance)
            .ToList();

        Console.WriteLine($"  Max Score : {maxScore:F2}");
        Console.WriteLine(
            $"  Rule      : Score >= {maxScore - scoreTolerance:F2} (±{scoreTolerance:F0})");

        Console.WriteLine();
        Console.WriteLine("=== BACKGROUND DECISION ===");

        foreach (var c in candidates)
        {
            bool patch = selected.Contains(c);

            Console.WriteLine(
                $"  {(patch ? "PATCH" : "HIDE ")} " +
                $"Score={c.Score:F2} | {c.GameObjectName} -> {c.TextureName} | " +
                $"{c.Width}x{c.Height} | " +
                $"rect=({c.RectX:F0},{c.RectY:F0},{c.RectW:F0},{c.RectH:F0}) | " +
                $"scale=({c.ScaleX:F2},{c.ScaleY:F2})");
        }

        Console.WriteLine();
        Console.WriteLine($"PATCH ทั้งหมด {selected.Count} background candidate(s)");
        Console.WriteLine("HIDE ทุก Renderer ที่ไม่อยู่ในกลุ่ม PATCH");
    }

    public static void AnalyzeBackgroundSource(string sourcePath)
    {
        throw new NotSupportedException(
            "analyze-bg-source ยังไม่ถูกย้ายจากเครื่องมือรุ่นก่อน");
    }


    public static void DumpEffects(string bundlePath, string? outputPath)
    {
        if (!File.Exists(bundlePath))
            throw new FileNotFoundException("ไม่พบ AssetBundle", bundlePath);

        var lines = new List<string>();
        void Log(string text)
        {
            Console.WriteLine(text);
            lines.Add(text);
        }

        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var bun = bunInst.file;

        Log($"TexturePatcher dumpfx");
        Log($"Bundle: {Path.GetFullPath(bundlePath)}");
        Log("");

        int assetsFileIndex = 0;
        while (assetsFileIndex < bun.GetAllFileNames().Count)
        {
            AssetsFileInstance? afileInst = null;
            try { afileInst = manager.LoadAssetsFileFromBundle(bunInst, assetsFileIndex, false); }
            catch (Exception ex)
            {
                Log($"[AssetsFile {assetsFileIndex}] LOAD ERROR: {ex.Message}");
                assetsFileIndex++;
                continue;
            }

            if (afileInst == null)
            {
                assetsFileIndex++;
                continue;
            }

            Log($"================ AssetsFile {assetsFileIndex}: {afileInst.name} ================");

            var gameObjects = new Dictionary<long, string>();
            foreach (var goInfo in afileInst.file.GetAssetsOfType(AssetClassID.GameObject))
            {
                try
                {
                    var go = manager.GetBaseField(afileInst, goInfo);
                    gameObjects[goInfo.PathId] = go["m_Name"].AsString;
                }
                catch { }
            }

            var sprites = new Dictionary<long, (string Name, long TexturePathId)>();
            foreach (var spriteInfo in afileInst.file.GetAssetsOfType(AssetClassID.Sprite))
            {
                try
                {
                    var sprite = manager.GetBaseField(afileInst, spriteInfo);
                    long texturePathId = sprite["m_RD"]["texture"]["m_PathID"].AsLong;
                    sprites[spriteInfo.PathId] = (sprite["m_Name"].AsString, texturePathId);
                }
                catch { }
            }

            var textures = new Dictionary<long, string>();
            foreach (var texInfo in afileInst.file.GetAssetsOfType(AssetClassID.Texture2D))
            {
                try
                {
                    textures[texInfo.PathId] = manager.GetBaseField(afileInst, texInfo)["m_Name"].AsString;
                }
                catch { }
            }

            Log("");
            Log("--- SpriteRenderer ---");
            int srCount = 0;
            foreach (var srInfo in afileInst.file.GetAssetsOfType(AssetClassID.SpriteRenderer))
            {
                srCount++;
                try
                {
                    var sr = manager.GetBaseField(afileInst, srInfo);
                    long goId = sr["m_GameObject"]["m_PathID"].AsLong;
                    string goName = gameObjects.TryGetValue(goId, out var gn) ? gn : "(unknown)";

                    long spriteId = sr["m_Sprite"]["m_PathID"].AsLong;
                    string spriteName = sprites.TryGetValue(spriteId, out var sp) ? sp.Name : "(none)";
                    string textureName = sprites.TryGetValue(spriteId, out sp) && textures.TryGetValue(sp.TexturePathId, out var tn)
                        ? tn
                        : "(none)";

                    bool enabled = false;
                    try { enabled = sr["m_Enabled"].AsBool; } catch { }

                    string color = "(unavailable)";
                    try
                    {
                        var c = sr["m_Color"];
                        color = $"({c["r"].AsFloat:F3},{c["g"].AsFloat:F3},{c["b"].AsFloat:F3},{c["a"].AsFloat:F3})";
                    }
                    catch { }

                    string material = "(none)";
                    try
                    {
                        var mats = sr["m_Materials"];
                        int count = mats.Children.Count;
                        material = $"{count} material(s)";
                    }
                    catch { }

                    Log($"  SR PathID={srInfo.PathId}");
                    Log($"    GameObject : '{goName}' (PathID={goId})");
                    Log($"    Enabled    : {enabled}");
                    Log($"    Sprite     : '{spriteName}' (PathID={spriteId})");
                    Log($"    Texture    : '{textureName}'");
                    Log($"    Color      : {color}");
                    Log($"    Materials  : {material}");
                }
                catch (Exception ex)
                {
                    Log($"  SR PathID={srInfo.PathId} ERROR: {ex.Message}");
                }
            }
            Log($"  SpriteRenderer count: {srCount}");

            Log("");
            Log("--- Animator ---");
            int animatorCount = 0;
            foreach (var info in afileInst.file.GetAssetsOfType(AssetClassID.Animator))
            {
                animatorCount++;
                try
                {
                    var a = manager.GetBaseField(afileInst, info);
                    long goId = a["m_GameObject"]["m_PathID"].AsLong;
                    string goName = gameObjects.TryGetValue(goId, out var gn) ? gn : "(unknown)";
                    long controllerId = 0;
                    try { controllerId = a["m_Controller"]["m_PathID"].AsLong; } catch { }
                    bool enabled = true;
                    try { enabled = a["m_Enabled"].AsBool; } catch { }

                    Log($"  Animator PathID={info.PathId}");
                    Log($"    GameObject : '{goName}' (PathID={goId})");
                    Log($"    Enabled    : {enabled}");
                    Log($"    Controller : PathID={controllerId}");
                }
                catch (Exception ex)
                {
                    Log($"  Animator PathID={info.PathId} ERROR: {ex.Message}");
                }
            }
            Log($"  Animator count: {animatorCount}");

            Log("");
            Log("--- Animation ---");
            int animationCount = 0;
            foreach (var info in afileInst.file.GetAssetsOfType(AssetClassID.Animation))
            {
                animationCount++;
                try
                {
                    var a = manager.GetBaseField(afileInst, info);
                    long goId = a["m_GameObject"]["m_PathID"].AsLong;
                    string goName = gameObjects.TryGetValue(goId, out var gn) ? gn : "(unknown)";
                    bool enabled = true;
                    try { enabled = a["m_Enabled"].AsBool; } catch { }

                    Log($"  Animation PathID={info.PathId}");
                    Log($"    GameObject : '{goName}' (PathID={goId})");
                    Log($"    Enabled    : {enabled}");
                }
                catch (Exception ex)
                {
                    Log($"  Animation PathID={info.PathId} ERROR: {ex.Message}");
                }
            }
            Log($"  Animation count: {animationCount}");

            Log("");
            Log("--- MonoBehaviour ---");
            int monoCount = 0;
            foreach (var info in afileInst.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
            {
                monoCount++;
                try
                {
                    var mb = manager.GetBaseField(afileInst, info);
                    long goId = 0;
                    try { goId = mb["m_GameObject"]["m_PathID"].AsLong; } catch { }
                    string goName = gameObjects.TryGetValue(goId, out var gn) ? gn : "(unknown)";

                    long scriptId = 0;
                    try { scriptId = mb["m_Script"]["m_PathID"].AsLong; } catch { }
                    string scriptType = "(unknown)";
                    try { scriptType = mb["m_Script"]["m_FileID"].AsInt.ToString(); } catch { }

                    Log($"  MonoBehaviour PathID={info.PathId}");
                    Log($"    GameObject : '{goName}' (PathID={goId})");
                    Log($"    Script     : FileID={scriptType}, PathID={scriptId}");
                }
                catch (Exception ex)
                {
                    Log($"  MonoBehaviour PathID={info.PathId} ERROR: {ex.Message}");
                }
            }
            Log($"  MonoBehaviour count: {monoCount}");

            Log("");
            Log("--- Candidate GameObjects (names containing flash/flicker/overlay/light/white) ---");
            string[] keywords = { "flash", "flicker", "overlay", "light", "white", "effect", "screen", "bg" };
            int candidateCount = 0;
            foreach (var kv in gameObjects.OrderBy(x => x.Value, StringComparer.OrdinalIgnoreCase))
            {
                bool match = keywords.Any(k => kv.Value.Contains(k, StringComparison.OrdinalIgnoreCase));
                if (!match) continue;
                candidateCount++;
                Log($"  '{kv.Value}' (PathID={kv.Key})");
            }
            Log($"  Keyword candidates: {candidateCount}");
            Log("");

            assetsFileIndex++;
        }

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            File.WriteAllLines(outputPath, lines);
            Console.WriteLine($"\nDump saved: {Path.GetFullPath(outputPath)}");
        }
    }
}
