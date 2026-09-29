using AssetsTools.NET.Extra;

// หมายเหตุ: inspect ใช้ Transform ของ GameObject โดยตรง (ไม่ไล่ parent chain เหมือน analyze)
// เป็นพฤติกรรมเดิมของโค้ดต้นฉบับ คงไว้เหมือนเดิมโดยตั้งใจเพื่อไม่ให้ผลลัพธ์เปลี่ยน
static class InspectCommand
{
    public static void InspectBackground(string bundlePath)
    {
        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var bun = bunInst.file;

        var results = new List<(string TexName, int W, int H, string SpriteName,
            string GoName, bool Enabled, float PosX, float PosY, float PosZ,
            float ScaleX, float ScaleY, int SortOrder,
            float RectX, float RectY, float RectW, float RectH)>();

        int assetsFileIndex = 0;
        while (assetsFileIndex < bun.GetAllFileNames().Count)
        {
            AssetsFileInstance? afileInst = null;
            try { afileInst = manager.LoadAssetsFileFromBundle(bunInst, assetsFileIndex, false); }
            catch { assetsFileIndex++; continue; }
            if (afileInst == null) { assetsFileIndex++; continue; }

            var textures = new Dictionary<long, (string Name, int W, int H)>();
            foreach (var t in afileInst.file.GetAssetsOfType(AssetClassID.Texture2D))
            {
                var tb = manager.GetBaseField(afileInst, t);
                textures[t.PathId] = (tb["m_Name"].AsString, tb["m_Width"].AsInt, tb["m_Height"].AsInt);
            }

            var sprites = new Dictionary<long, (string Name, long TexPathId, float RectX, float RectY, float RectW, float RectH)>();
            foreach (var s in afileInst.file.GetAssetsOfType(AssetClassID.Sprite))
            {
                var sb = manager.GetBaseField(afileInst, s);
                long texId = 0;
                try { texId = sb["m_RD"]["texture"]["m_PathID"].AsLong; } catch { }

                float rectX = 0, rectY = 0, rectW = 0, rectH = 0;
                try
                {
                    var rect = sb["m_Rect"];
                    rectX = rect["x"].AsFloat; rectY = rect["y"].AsFloat;
                    rectW = rect["width"].AsFloat; rectH = rect["height"].AsFloat;
                }
                catch { }

                sprites[s.PathId] = (sb["m_Name"].AsString, texId, rectX, rectY, rectW, rectH);
            }

            var transforms = new Dictionary<long, (float X, float Y, float Z, float SX, float SY)>();
            foreach (var tr in afileInst.file.GetAssetsOfType(AssetClassID.Transform))
            {
                var trb = manager.GetBaseField(afileInst, tr);
                try
                {
                    long goId = trb["m_GameObject"]["m_PathID"].AsLong;
                    var pos = trb["m_LocalPosition"];
                    var scl = trb["m_LocalScale"];
                    transforms[goId] = (pos["x"].AsFloat, pos["y"].AsFloat, pos["z"].AsFloat, scl["x"].AsFloat, scl["y"].AsFloat);
                }
                catch { }
            }

            var gameObjects = new Dictionary<long, string>();
            foreach (var go in afileInst.file.GetAssetsOfType(AssetClassID.GameObject))
            {
                var gb = manager.GetBaseField(afileInst, go);
                gameObjects[go.PathId] = gb["m_Name"].AsString;
            }

            foreach (var sr in afileInst.file.GetAssetsOfType(AssetClassID.SpriteRenderer))
            {
                var srb = manager.GetBaseField(afileInst, sr);
                try
                {
                    long spriteId = srb["m_Sprite"]["m_PathID"].AsLong;
                    long goId = srb["m_GameObject"]["m_PathID"].AsLong;
                    bool enabled = srb["m_Enabled"].AsBool;
                    int sortOrder = 0;
                    try { sortOrder = srb["m_SortingOrder"].AsInt; } catch { }

                    if (!sprites.TryGetValue(spriteId, out var spr)) continue;
                    if (!textures.TryGetValue(spr.TexPathId, out var tex)) continue;

                    string goName = gameObjects.TryGetValue(goId, out var n) ? n : "(unknown)";
                    var t = transforms.TryGetValue(goId, out var tv) ? tv : (0, 0, 0, 1, 1);

                    results.Add((tex.Name, tex.W, tex.H, spr.Name, goName, enabled,
                        t.Item1, t.Item2, t.Item3, t.Item4, t.Item5, sortOrder,
                        spr.RectX, spr.RectY, spr.RectW, spr.RectH));
                }
                catch { }
            }

            assetsFileIndex++;
        }

        Console.WriteLine(
            $"{"Texture",-16} {"Size",-11} {"Sprite",-14} {"GameObject",-14} " +
            $"{"Enabled",-8} {"Position",-22} {"Scale",-14} {"Area(px²)",-11} " +
            $"{"Rect",-25} {"SortOrder"}");
        Console.WriteLine(new string('-', 130));

        foreach (var r in results.OrderByDescending(r => (long)(r.W * Math.Abs(r.ScaleX)) * (long)(r.H * Math.Abs(r.ScaleY))))
        {
            long area = (long)(r.W * Math.Abs(r.ScaleX)) * (long)(r.H * Math.Abs(r.ScaleY));
            string pos = $"({r.PosX:F0},{r.PosY:F0},{r.PosZ:F0})";
            string scale = $"({r.ScaleX:F2},{r.ScaleY:F2})";
            string rect = $"({r.RectX:F0},{r.RectY:F0},{r.RectW:F0},{r.RectH:F0})";

            Console.WriteLine(
                $"{r.TexName,-16} {r.W}x{r.H,-6} {r.SpriteName,-14} {r.GoName,-14} " +
                $"{r.Enabled,-8} {pos,-22} {scale,-14} {area,-11:N0} {rect,-25} {r.SortOrder}");
        }
    }
}
