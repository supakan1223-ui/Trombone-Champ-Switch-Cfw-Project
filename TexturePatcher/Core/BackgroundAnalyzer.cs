using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using static AssetField;

static class BackgroundAnalyzer
{
   public static BackgroundCandidate? FindLargestBackgroundTexture(string bundlePath)
   {
       var manager = new AssetsManager();
       var bunInst = manager.LoadBundleFile(bundlePath, true);
       var bun = bunInst.file;
       BackgroundCandidate? best = null;

       int index = 0;
       while (index < bun.GetAllFileNames().Count)
       {
           AssetsFileInstance? afileInst = null;
           try { afileInst = manager.LoadAssetsFileFromBundle(bunInst, index, false); }
           catch { }
           index++;
           if (afileInst == null) continue;

           var textures = new Dictionary<long, (string Name, int W, int H, int Format)>();
           foreach (var t in afileInst.file.GetAssetsOfType(AssetClassID.Texture2D))
           {
               try
               {
                   var tb = manager.GetBaseField(afileInst, t);
                   textures[t.PathId] = (
                       tb["m_Name"].AsString,
                       tb["m_Width"].AsInt,
                       tb["m_Height"].AsInt,
                       tb["m_TextureFormat"].AsInt);
               }
               catch { }
           }

           var sprites = new Dictionary<long, (string Name, long TexPathId, float X, float Y, float W, float H)>();
           foreach (var s in afileInst.file.GetAssetsOfType(AssetClassID.Sprite))
           {
               try
               {
                   var sb = manager.GetBaseField(afileInst, s);
                   long texId = sb["m_RD"]["texture"]["m_PathID"].AsLong;
                   var rect = sb["m_Rect"];
                   sprites[s.PathId] = (
                       sb["m_Name"].AsString,
                       texId,
                       rect["x"].AsFloat,
                       rect["y"].AsFloat,
                       rect["width"].AsFloat,
                       rect["height"].AsFloat);
               }
               catch { }
           }

           var transforms = new Dictionary<long, (float X, float Y, float SX, float SY)>();
           foreach (var tr in afileInst.file.GetAssetsOfType(AssetClassID.Transform))
           {
               try
               {
                   var tb = manager.GetBaseField(afileInst, tr);
                   long goId = tb["m_GameObject"]["m_PathID"].AsLong;
                   var pos = tb["m_LocalPosition"];
                   var scale = tb["m_LocalScale"];
                   transforms[goId] = (pos["x"].AsFloat, pos["y"].AsFloat, scale["x"].AsFloat, scale["y"].AsFloat);
               }
               catch { }
           }

           var gameObjects = new Dictionary<long, string>();
           foreach (var go in afileInst.file.GetAssetsOfType(AssetClassID.GameObject))
           {
               try { gameObjects[go.PathId] = manager.GetBaseField(afileInst, go)["m_Name"].AsString; }
               catch { }
           }

           foreach (var sr in afileInst.file.GetAssetsOfType(AssetClassID.SpriteRenderer))
           {
               try
               {
                   var sb = manager.GetBaseField(afileInst, sr);
                   long spriteId = sb["m_Sprite"]["m_PathID"].AsLong;
                   long goId = sb["m_GameObject"]["m_PathID"].AsLong;
                   bool enabled = sb["m_Enabled"].AsBool;

                   if (!sprites.TryGetValue(spriteId, out var sprite)) continue;
                   if (!textures.TryGetValue(sprite.TexPathId, out var texture)) continue;

                   var tr = transforms.TryGetValue(goId, out var tv) ? tv : (X: 0f, Y: 0f, SX: 1f, SY: 1f);
                   string goName = gameObjects.TryGetValue(goId, out var n) ? n : "(unknown)";

                   double rectArea = Math.Max(1, Math.Abs(sprite.W) * Math.Abs(sprite.H));
                   double textureArea = (double)Math.Max(1, texture.W) * Math.Max(1, texture.H);
                   double scale = Math.Max(0.01, Math.Abs(tr.SX) * Math.Abs(tr.SY));

                   // Large enabled SpriteRenderers are much more likely to be the main background.
                   // Tiny overlays/lights/flags naturally score much lower.
                   double score = textureArea * scale * 0.75 + rectArea * 0.25;
                   if (!enabled) score *= 0.05;
                   if (texture.W < 512 || texture.H < 512) score *= 0.15;

                   var candidate = new BackgroundCandidate(
                       texture.Name, texture.W, texture.H, texture.Format,
                       sprite.Name, goName, goId, enabled,
                       tr.SX, tr.SY,
                       sprite.X, sprite.Y, sprite.W, sprite.H,
                       score, afileInst, sr);

                   if (best == null || candidate.Score > best.Score)
                       best = candidate;
               }
               catch { }
           }
       }

       return best;
   }

   public static List<BackgroundTarget> FindBackgroundTargets(string bundlePath)
   {
       var manager = new AssetsManager();
       var bunInst = manager.LoadBundleFile(bundlePath, true);
       var bun = bunInst.file;
       var candidates = new List<BackgroundTarget>();

       int assetsFileIndex = 0;
       while (assetsFileIndex < bun.GetAllFileNames().Count)
       {
           AssetsFileInstance? afileInst = null;
           try { afileInst = manager.LoadAssetsFileFromBundle(bunInst, assetsFileIndex, false); }
           catch { }
           assetsFileIndex++;
           if (afileInst == null) continue;

           var textures = new Dictionary<long, (string Name, int W, int H, int Format)>();
           foreach (var texInfo in afileInst.file.GetAssetsOfType(AssetClassID.Texture2D))
           {
               try
               {
                   var tb = manager.GetBaseField(afileInst, texInfo);
                   textures[texInfo.PathId] = (tb["m_Name"].AsString, tb["m_Width"].AsInt, tb["m_Height"].AsInt, tb["m_TextureFormat"].AsInt);
               }
               catch { }
           }

           var sprites = new Dictionary<long, (string Name, long TexturePathId, float X, float Y, float W, float H)>();
           foreach (var spriteInfo in afileInst.file.GetAssetsOfType(AssetClassID.Sprite))
           {
               try
               {
                   var sb = manager.GetBaseField(afileInst, spriteInfo);
                   long texturePathId = sb["m_RD"]["texture"]["m_PathID"].AsLong;
                   var rect = sb["m_Rect"];
                   sprites[spriteInfo.PathId] = (sb["m_Name"].AsString, texturePathId, rect["x"].AsFloat, rect["y"].AsFloat, rect["width"].AsFloat, rect["height"].AsFloat);
               }
               catch { }
           }

           var transforms = new Dictionary<long, (float SX, float SY)>();
           foreach (var trInfo in afileInst.file.GetAssetsOfType(AssetClassID.Transform))
           {
               try
               {
                   var tb = manager.GetBaseField(afileInst, trInfo);
                   long goId = tb["m_GameObject"]["m_PathID"].AsLong;
                   var scale = tb["m_LocalScale"];
                   transforms[goId] = (scale["x"].AsFloat, scale["y"].AsFloat);
               }
               catch { }
           }

           var gameObjects = new Dictionary<long, string>();
           foreach (var goInfo in afileInst.file.GetAssetsOfType(AssetClassID.GameObject))
           {
               try { gameObjects[goInfo.PathId] = manager.GetBaseField(afileInst, goInfo)["m_Name"].AsString; }
               catch { }
           }

           foreach (var srInfo in afileInst.file.GetAssetsOfType(AssetClassID.SpriteRenderer))
           {
               try
               {
                   var sr = manager.GetBaseField(afileInst, srInfo);
                   long spriteId = sr["m_Sprite"]["m_PathID"].AsLong;
                   long goId = sr["m_GameObject"]["m_PathID"].AsLong;
                   bool enabled = sr["m_Enabled"].AsBool;
                   if (!sprites.TryGetValue(spriteId, out var sprite)) continue;
                   if (!textures.TryGetValue(sprite.TexturePathId, out var texture)) continue;

                   var scale = transforms.TryGetValue(goId, out var sc) ? sc : (SX: 1f, SY: 1f);
                   string goName = gameObjects.TryGetValue(goId, out var name) ? name : "(unknown)";

                   double rectArea = Math.Max(1, Math.Abs(sprite.W) * Math.Abs(sprite.H));
                   double textureArea = (double)Math.Max(1, texture.W) * Math.Max(1, texture.H);
                   double scaleArea = Math.Max(0.01, Math.Abs(scale.SX) * Math.Abs(scale.SY));
                   double rawScore = textureArea * 0.70 + rectArea * 0.30;
                   rawScore *= scaleArea;
                   if (!enabled) rawScore *= 0.05;
                   if (texture.W < 512 || texture.H < 512) rawScore *= 0.10;

                   candidates.Add(new BackgroundTarget(texture.Name, sprite.Name, goName, goId, texture.W, texture.H, texture.Format, enabled, sprite.X, sprite.Y, sprite.W, sprite.H, scale.SX, scale.SY, rawScore));
               }
               catch { }
           }
       }

       if (candidates.Count == 0) return candidates;

       double maxRawScore = candidates.Max(c => c.Score);
       if (maxRawScore > 0)
       {
           candidates = candidates
               .Select(c => c with { Score = c.Score / maxRawScore * 100.0 })
               .ToList();
       }

       return candidates.OrderByDescending(c => c.Score).ToList();
   }
}
