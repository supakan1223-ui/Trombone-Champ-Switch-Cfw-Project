using AssetsTools.NET;
using AssetsTools.NET.Extra;
using static AssetField;

// ไล่ SpriteRenderer -> Sprite -> Texture2D -> GameObject/Transform (รวม parent chain)
// แล้วให้คะแนนว่าตัวไหนน่าจะเป็น "Main Background" มากที่สุด
//
// เดิม logic ชุดนี้ถูกเขียนซ้ำคำต่อคำ 2 รอบใน AnalyzeBackground และ FindMainBackground
// (InspectBackground ใช้ transform โดยตรงแบบไม่ไล่ parent chain ซึ่งเป็นพฤติกรรมที่ต่างกันจริง
//  จึงตั้งใจไม่รวมเข้าที่นี่ ดู Commands/InspectCommand.cs)
static class BackgroundAnalyzer
{
    public static List<BgCandidate> GatherCandidates(
        AssetsManager manager,
        AssetBundleFile bun,
        Func<int, AssetsFileInstance?> loadAssetsFile)
    {
        var cands = new List<BgCandidate>();

        int idx = 0;
        while (idx < bun.GetAllFileNames().Count)
        {
            AssetsFileInstance? afile = null;
            try { afile = loadAssetsFile(idx); } catch { }
            idx++;
            if (afile == null) continue;

            // Texture2D
            var textures = new Dictionary<long, (string Name, int W, int H, int Fmt)>();
            foreach (var t in afile.file.GetAssetsOfType(AssetClassID.Texture2D))
            {
                var tb = manager.GetBaseField(afile, t);
                textures[t.PathId] = (SafeS(tb["m_Name"]), SafeI(tb["m_Width"]), SafeI(tb["m_Height"]), SafeI(tb["m_TextureFormat"]));
            }

            // Sprite
            var sprites = new Dictionary<long, (string Name, long TexId, float X, float Y, float W, float H, float Ppu)>();
            foreach (var s in afile.file.GetAssetsOfType(AssetClassID.Sprite))
            {
                var sb = manager.GetBaseField(afile, s);
                long texId = SafeL(sb["m_RD"]["texture"]["m_PathID"]);
                var r = sb["m_Rect"];
                float ppu = SafeF(sb["m_PixelsToUnits"], 100f);
                if (ppu <= 0) ppu = 100f;
                sprites[s.PathId] = (SafeS(sb["m_Name"]), texId, SafeF(r["x"]), SafeF(r["y"]), SafeF(r["width"]), SafeF(r["height"]), ppu);
            }

            // GameObject
            var gos = new Dictionary<long, (string Name, bool Active)>();
            foreach (var g in afile.file.GetAssetsOfType(AssetClassID.GameObject))
            {
                var gb = manager.GetBaseField(afile, g);
                gos[g.PathId] = (SafeS(gb["m_Name"]), SafeB(gb["m_IsActive"], true));
            }

            // Transform / RectTransform: เก็บ local + parent เพื่อคำนวณ world
            var tr = new Dictionary<long, (long Go, long Father, float X, float Y, float Z, float SX, float SY)>();
            foreach (var cid in new[] { AssetClassID.Transform, AssetClassID.RectTransform })
            {
                foreach (var ti in afile.file.GetAssetsOfType(cid))
                {
                    var tb = manager.GetBaseField(afile, ti);
                    var p = tb["m_LocalPosition"];
                    var sc = tb["m_LocalScale"];
                    tr[ti.PathId] = (SafeL(tb["m_GameObject"]["m_PathID"]), SafeL(tb["m_Father"]["m_PathID"]),
                        SafeF(p["x"]), SafeF(p["y"]), SafeF(p["z"]), SafeF(sc["x"], 1f), SafeF(sc["y"], 1f));
                }
            }
            var goToTr = new Dictionary<long, long>();
            foreach (var kv in tr) goToTr[kv.Value.Go] = kv.Key;

            (float X, float Y, float Z, float SX, float SY) World(long trId)
            {
                float x = 0, y = 0, z = 0, sx = 1, sy = 1;
                // ไล่จากตัวเองขึ้นไปหา parent (ไม่คิด rotation)
                var chain = new List<(float X, float Y, float Z, float SX, float SY)>();
                int guard = 0;
                while (trId != 0 && tr.TryGetValue(trId, out var t) && guard++ < 64)
                {
                    chain.Add((t.X, t.Y, t.Z, t.SX, t.SY));
                    trId = t.Father;
                }
                for (int i = chain.Count - 1; i >= 0; i--)
                {
                    var c = chain[i];
                    x += sx * c.X; y += sy * c.Y; z += c.Z;
                    sx *= c.SX; sy *= c.SY;
                }
                return (x, y, z, sx, sy);
            }

            foreach (var sr in afile.file.GetAssetsOfType(AssetClassID.SpriteRenderer))
            {
                var srb = manager.GetBaseField(afile, sr);
                long spriteId = SafeL(srb["m_Sprite"]["m_PathID"]);
                if (spriteId == 0 || !sprites.TryGetValue(spriteId, out var spr)) continue;
                if (!textures.TryGetValue(spr.TexId, out var tex)) continue;

                long goId = SafeL(srb["m_GameObject"]["m_PathID"]);
                gos.TryGetValue(goId, out var go);
                var w = goToTr.TryGetValue(goId, out var trId) ? World(trId) : (0f, 0f, 0f, 1f, 1f);

                int drawMode = SafeI(srb["m_DrawMode"]);
                float baseW = spr.W / spr.Ppu, baseH = spr.H / spr.Ppu;
                if (drawMode != 0)
                {
                    float sw = SafeF(srb["m_Size"]["x"]), sh = SafeF(srb["m_Size"]["y"]);
                    if (sw > 0 && sh > 0) { baseW = sw; baseH = sh; }
                }

                cands.Add(new BgCandidate
                {
                    GameObject = go.Name ?? "(unknown)",
                    Sprite = spr.Name, Texture = tex.Name,
                    TexW = tex.W, TexH = tex.H, TexFormat = tex.Fmt,
                    RectX = spr.X, RectY = spr.Y, RectW = spr.W, RectH = spr.H, Ppu = spr.Ppu,
                    PosX = w.Item1, PosY = w.Item2, PosZ = w.Item3, ScaleX = w.Item4, ScaleY = w.Item5,
                    WorldW = baseW * Math.Abs(w.Item4), WorldH = baseH * Math.Abs(w.Item5),
                    DrawMode = drawMode,
                    Enabled = SafeB(srb["m_Enabled"], true),
                    GoActive = go.Name != null ? go.Active : true,
                    SortOrder = SafeI(srb["m_SortingOrder"]),
                    File = afile.name
                });
            }
        }

        return cands;
    }

    // ---------------- Scoring (สัมพัทธ์ภายใน bundle เดียวกัน ไม่ hardcode ค่า) ----------------
    // 50  พื้นที่ที่แสดงจริง (หารด้วย scale ถ้าถูกขยายเกิน 1 เท่า) เทียบกับตัวที่ใหญ่สุด
    // 15  ขนาด texture เทียบกับตัวที่ใหญ่สุด
    // 10  sprite rect เต็ม texture
    // 15  sorting order ต่ำ (อยู่หลังสุด)
    // 10  z ไกลกล้อง (Unity 2D มอง +z)
    // ตัวคูณ 0.25 ถ้า renderer ปิด หรือ GameObject inactive
    public static void ScoreCandidates(List<BgCandidate> cands)
    {
        if (cands.Count == 0) return;

        float maxArea = cands.Max(c => c.EffArea);
        long maxTex = cands.Max(c => c.TexArea);
        int minSort = cands.Min(c => c.SortOrder), maxSort = cands.Max(c => c.SortOrder);
        float minZ = cands.Min(c => c.PosZ), maxZ = cands.Max(c => c.PosZ);

        foreach (var c in cands)
        {
            double area = maxArea > 0 ? 50.0 * c.EffArea / maxArea : 0;
            double tex = maxTex > 0 ? 15.0 * c.TexArea / maxTex : 0;
            double fill = c.TexArea > 0 ? 10.0 * Math.Min(1.0, (double)c.RectW * c.RectH / c.TexArea) : 0;
            double sort = maxSort > minSort ? 15.0 * (maxSort - c.SortOrder) / (maxSort - minSort) : 7.5;
            double z = maxZ > minZ ? 10.0 * (c.PosZ - minZ) / (maxZ - minZ) : 5.0;
            double total = area + tex + fill + sort + z;
            if (!c.Enabled || !c.GoActive) { total *= 0.25; c.Note = "renderer/GameObject ปิดอยู่"; }
            c.Score = total;
        }
    }

    // เปิด bundle เอง, gather + score แล้วคืนตัวที่คะแนนสูงสุด (throw ถ้าไม่เจอเลย)
    // ใช้โดย patchbg เพื่อหา main background ทั้งฝั่ง target และ source
    public static BgCandidate FindMain(string bundlePath)
    {
        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var bun = bunInst.file;

        var cands = GatherCandidates(manager, bun,
            idx => { try { return manager.LoadAssetsFileFromBundle(bunInst, idx, false); } catch { return null; } });

        if (cands.Count == 0)
            throw new InvalidOperationException($"ไม่พบ Background ใน '{bundlePath}'");

        ScoreCandidates(cands);
        return cands.OrderByDescending(c => c.Score).First();
    }

    public static void PrintReport(List<BgCandidate> ordered)
    {
        Console.WriteLine("=== BACKGROUND CANDIDATES ===");
        foreach (var c in ordered)
        {
            Console.WriteLine();
            Console.WriteLine($"[Score {Math.Round(c.Score)}]");
            Console.WriteLine($"GameObject : {c.GameObject}");
            Console.WriteLine($"Sprite     : {c.Sprite}");
            Console.WriteLine($"Texture    : {c.Texture}");
            Console.WriteLine($"Size       : {c.TexW}x{c.TexH}");
            Console.WriteLine($"Format     : {c.TexFormat}");
            Console.WriteLine($"Rect       : {c.RectX:F0},{c.RectY:F0},{c.RectW:F0},{c.RectH:F0}");
            Console.WriteLine($"Position   : {c.PosX:F2},{c.PosY:F2},{c.PosZ:F2}");
            Console.WriteLine($"Scale      : {c.ScaleX:F2},{c.ScaleY:F2}");
            Console.WriteLine($"WorldSize  : {c.WorldW:F2} x {c.WorldH:F2} units (ppu={c.Ppu:F0}, drawMode={c.DrawMode})");
            Console.WriteLine($"SortOrder  : {c.SortOrder}");
            Console.WriteLine($"Enabled    : {c.Enabled}");
            if (c.Note != "") Console.WriteLine($"Note       : {c.Note}");
        }

        Console.WriteLine();
        var best = ordered[0];
        Console.WriteLine($"=> MAIN BACKGROUND: GameObject '{best.GameObject}' / Texture '{best.Texture}' (score {Math.Round(best.Score)})");
        if (ordered.Count > 1)
        {
            double margin = best.Score - ordered[1].Score;
            Console.WriteLine($"   ห่างอันดับ 2 = {margin:F1} คะแนน" + (margin < 10 ? "  [!] ใกล้กันมาก ควรตรวจด้วยตา" : ""));
        }
    }
}
