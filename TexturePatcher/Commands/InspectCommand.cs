using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static class InspectCommand
{
    public static void InspectBackground(string bundlePath)
    {
        var candidate = BackgroundAnalyzer.FindLargestBackgroundTexture(bundlePath);
        if (candidate == null)
        {
            Console.WriteLine("ไม่พบ Background จาก SpriteRenderer");
            return;
        }

        Console.WriteLine("Background candidate:");
        Console.WriteLine($"  Texture : {candidate.TextureName}");
        Console.WriteLine($"  Size    : {candidate.Width}x{candidate.Height}");
        Console.WriteLine($"  Format  : {candidate.TextureFormat} {TextureCodec.FormatName(candidate.TextureFormat)}");
        Console.WriteLine($"  Sprite  : {candidate.SpriteName}");
        Console.WriteLine($"  Object  : {candidate.GameObjectName}");
        Console.WriteLine($"  Enabled : {candidate.Enabled}");
        Console.WriteLine($"  Scale   : ({candidate.ScaleX:F2},{candidate.ScaleY:F2})");
        Console.WriteLine($"  Rect    : ({candidate.RectX:F0},{candidate.RectY:F0},{candidate.RectW:F0},{candidate.RectH:F0})");
        Console.WriteLine($"  Score   : {candidate.Score:N0}");
    }
}
