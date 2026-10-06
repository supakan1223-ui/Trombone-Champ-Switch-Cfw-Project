using AssetsTools.NET;
using AssetsTools.NET.Extra;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

static class PatchBgCommand
{
    public static void PatchBackground(
        string bundlePath,
        string imagePath,
        string outputPath)
    {
        if (!File.Exists(bundlePath))
            throw new FileNotFoundException("ไม่พบ Target AssetBundle", bundlePath);

        if (!File.Exists(imagePath))
            throw new FileNotFoundException("ไม่พบไฟล์ Background", imagePath);

        string ext = Path.GetExtension(imagePath).ToLowerInvariant();

        if (ext != ".png" && ext != ".trombackground" && ext != ".tromb" && ext != ".jpg" && ext != ".jpeg")
            throw new InvalidOperationException(
                "patchbg รองรับ Background เป็น .png, .trombackground หรือ .tromb เท่านั้น");

        Console.WriteLine("[1/7] วิเคราะห์ Background candidates...");

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
        Console.WriteLine(
            $"PATCH ทั้งหมด {selected.Count} background candidate(s)");
        Console.WriteLine(
            "HIDE ทุก Renderer ที่ไม่อยู่ในกลุ่ม PATCH");

        Console.WriteLine();
        Console.WriteLine("[2/7] เปิด Target AssetBundle...");

        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var bun = bunInst.file;

        var patchedTextures =
            new HashSet<string>(StringComparer.Ordinal);

        var resourceBuffers =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);

        var resourceDirs =
            new Dictionary<string, AssetBundleDirectoryInfo>(
                StringComparer.Ordinal);

        Image<Rgba32>? sourceTrombImage = null;
        BackgroundCandidate? sourceTrombCandidate = null;

        try
        {
            if (ext == ".trombackground" || ext == ".tromb")
            {
                Console.WriteLine();
                Console.WriteLine("[3/7] อ่าน Source .trombackground...");

                var sourceResult =
                    LoadTrombBackgroundImage(imagePath);

                sourceTrombImage = sourceResult.Image;
                sourceTrombCandidate = sourceResult.Candidate;

                Console.WriteLine(
                    $"  Source decoded : " +
                    $"{sourceTrombImage.Width}x{sourceTrombImage.Height}");
            }

            int patchedCount = 0;

            foreach (var target in selected)
            {
                if (!patchedTextures.Add(target.TextureName))
                {
                    Console.WriteLine(
                        $"  SKIP duplicate texture: {target.TextureName}");
                    continue;
                }

                var texture =
                    BundleTextureIO.FindTexture2D(
                        manager,
                        bunInst,
                        target.TextureName);

                if (texture == null)
                {
                    throw new InvalidOperationException(
                        $"ไม่พบ Texture2D '{target.TextureName}' ใน AssetBundle");
                }

                int width = texture.Width;
                int height = texture.Height;
                int textureFormat = texture.Format;
                int mipCount = texture.MipCount;
                uint offset = texture.Offset;
                uint size = texture.Size;

                if (textureFormat != 10 && textureFormat != 12)
                {
                    throw new InvalidOperationException(
                        $"Texture '{target.TextureName}' ใช้ " +
                        $"TextureFormat={textureFormat}; " +
                        "รองรับ DXT1 (10) หรือ DXT5 (12) เท่านั้น");
                }

                if (mipCount != 1)
                {
                    throw new InvalidOperationException(
                        $"Texture '{target.TextureName}' มี mipCount={mipCount}; " +
                        "เวอร์ชันนี้รองรับ mipCount=1 เท่านั้น");
                }

                var resourceDir =
                    BundleTextureIO.ResolveResourceDirectory(
                        bun,
                        texture.ResourceName);

                if ((long)offset + size >
                    resourceDir.DecompressedSize)
                {
                    throw new InvalidOperationException(
                        $"Texture '{target.TextureName}' range เกิน resource: " +
                        $"offset={offset}, size={size}, " +
                        $"resourceSize={resourceDir.DecompressedSize}");
                }

                Console.WriteLine();
                Console.WriteLine(
                    $"[PATCH {patchedCount + 1}/{selected.Count}] " +
                    $"{target.GameObjectName} -> {target.TextureName}");

                Console.WriteLine(
                    $"  Score   : {target.Score:F2}");

                Console.WriteLine(
                    $"  Size    : {width}x{height}");

                Console.WriteLine(
                    $"  Format  : {textureFormat} " +
                    $"{TextureCodec.FormatName(textureFormat)}");

                Console.WriteLine(
                    $"  Rect    : " +
                    $"({target.RectX:F0},{target.RectY:F0}," +
                    $"{target.RectW:F0},{target.RectH:F0})");

                byte[] encoded;

                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    encoded =
                        TextureCodec.EncodeBackgroundTexture(
                            imagePath,
                            width,
                            height,
                            textureFormat,
                            target.RectX,
                            target.RectY,
                            target.RectW,
                            target.RectH);
                }
                else
                {
                    using var prepared =
                        PrepareTrombBackgroundImage(
                            sourceTrombImage!,
                            sourceTrombCandidate!,
                            target,
                            width,
                            height);

                    encoded =
                        TextureCodec.EncodeRaw(
                            prepared,
                            textureFormat);
                }

                Console.WriteLine(
                    $"  Encoded : {encoded.Length:N0} bytes");

                Console.WriteLine(
                    $"  Target  : {size:N0} bytes");

                if (encoded.LongLength != size)
                {
                    throw new InvalidOperationException(
                        $"ขนาด encoded ของ '{target.TextureName}' ไม่ตรง: " +
                        $"ได้ {encoded.Length}, ต้องการ {size}");
                }

                if (!resourceBuffers.TryGetValue(
                        resourceDir.Name,
                        out var resourceBytes))
                {
                    resourceBytes =
                        BundleTextureIO.ReadBundleDirectoryBytes(
                            bun,
                            resourceDir);

                    resourceBuffers[resourceDir.Name] =
                        resourceBytes;

                    resourceDirs[resourceDir.Name] =
                        resourceDir;
                }

                Buffer.BlockCopy(
                    encoded,
                    0,
                    resourceBytes,
                    checked((int)offset),
                    encoded.Length);

                patchedCount++;
            }

            foreach (var pair in resourceBuffers)
            {
                var dir = resourceDirs[pair.Key];

                dir.Replacer =
                    new ContentReplacerFromBuffer(pair.Value);
            }

            Console.WriteLine();
            Console.WriteLine(
                "[6/7] เขียน Bundle ชั่วคราวหลัง PATCH...");

            string tempPatchedPath =
                outputPath + ".patchbg_tmp";

            if (File.Exists(tempPatchedPath))
                File.Delete(tempPatchedPath);

            using (var writer =
                   new AssetsFileWriter(tempPatchedPath))
            {
                bun.Write(writer);
            }

            Console.WriteLine(
                "[7/7] ใช้ Hide logic เดิม โดย KEEP " +
                "ทุก GameObject ที่ถูก PATCH...");

            var keepNames = selected
                .Select(x => x.GameObjectName)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x) &&
                    x != "(unknown)")
                .ToHashSet(StringComparer.Ordinal);

            if (keepNames.Count == 0)
            {
                throw new InvalidOperationException(
                    "ไม่พบ GameObject ที่ใช้เป็น Background สำหรับ KEEP");
            }

            try
            {
                HideCommand.HideOthersExcept(
                    tempPatchedPath,
                    keepNames,
                    outputPath);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPatchedPath))
                        File.Delete(tempPatchedPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[WARN] ลบไฟล์ชั่วคราวไม่สำเร็จ: {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                $"เสร็จแล้ว: {Path.GetFullPath(outputPath)}");

            Console.WriteLine(
                $"PATCH: {string.Join(
                    ", ",
                    selected.Select(
                        x => x.GameObjectName +
                             " -> " +
                             x.TextureName))}");

            Console.WriteLine(
                $"KEEP : {string.Join(", ", keepNames)}");
        }
        finally
        {
            sourceTrombImage?.Dispose();

            try
            {
                manager.UnloadBundleFile(bunInst);
            }
            catch
            {
                try { manager.UnloadAllBundleFiles(); }
                catch { }
            }
        }
    }

    private static (Image<Rgba32> Image, BackgroundCandidate Candidate)
        LoadTrombBackgroundImage(
            string sourceBundlePath)
    {
        var sourceCandidate =
            BackgroundAnalyzer.FindLargestBackgroundTexture(
                sourceBundlePath);

        if (sourceCandidate == null)
        {
            throw new InvalidOperationException(
                "ไม่พบ Background Texture2D ใน .trombackground");
        }

        Console.WriteLine(
            $"  Source BG : {sourceCandidate.GameObjectName}");

        Console.WriteLine(
            $"  Texture   : {sourceCandidate.TextureName}");

        Console.WriteLine(
            $"  Size      : " +
            $"{sourceCandidate.Width}x{sourceCandidate.Height}");

        Console.WriteLine(
            $"  Rect      : " +
            $"({sourceCandidate.RectX:F0}," +
            $"{sourceCandidate.RectY:F0}," +
            $"{sourceCandidate.RectW:F0}," +
            $"{sourceCandidate.RectH:F0})");

        var manager = new AssetsManager();
        var bunInst =
            manager.LoadBundleFile(
                sourceBundlePath,
                true);

        try
        {
            var texture =
                BundleTextureIO.FindTexture2D(
                    manager,
                    bunInst,
                    sourceCandidate.TextureName);

            if (texture == null)
            {
                throw new InvalidOperationException(
                    $"ไม่พบ Source Texture2D " +
                    $"'{sourceCandidate.TextureName}'");
            }

            if (texture.Format != 10 &&
                texture.Format != 12)
            {
                throw new InvalidOperationException(
                    $"Source Texture '{sourceCandidate.TextureName}' " +
                    $"ใช้ TextureFormat={texture.Format}; " +
                    "รองรับ DXT1 (10) หรือ DXT5 (12) เท่านั้น");
            }

            if (texture.MipCount != 1)
            {
                throw new InvalidOperationException(
                    $"Source Texture '{sourceCandidate.TextureName}' " +
                    $"มี mipCount={texture.MipCount}; " +
                    "รองรับเฉพาะ mipCount=1");
            }

            byte[] raw =
                BundleTextureIO.ReadTextureRaw(
                    bunInst.file,
                    texture);

            Console.WriteLine(
                $"  Source raw: {raw.Length:N0} bytes");

            var image = TextureCodec.DecodeRaw(
                raw,
                texture.Width,
                texture.Height,
                texture.Format);

            return (image, sourceCandidate);
        }
        finally
        {
            try
            {
                manager.UnloadBundleFile(bunInst);
            }
            catch
            {
                try { manager.UnloadAllBundleFiles(); }
                catch { }
            }
        }
    }

    private static Image<Rgba32> PrepareTrombBackgroundImage(
        Image<Rgba32> sourceImage,
        BackgroundCandidate sourceCandidate,
        BackgroundTarget target,
        int targetWidth,
        int targetHeight)
    {
        using var working = sourceImage.Clone();

        int sourceCropX =
            Math.Clamp(
                (int)Math.Round(sourceCandidate.RectX),
                0,
                Math.Max(0, working.Width - 1));

        int sourceCropY =
            Math.Clamp(
                working.Height -
                (int)Math.Round(
                    sourceCandidate.RectY +
                    sourceCandidate.RectH),
                0,
                Math.Max(0, working.Height - 1));

        int sourceCropW =
            Math.Clamp(
                (int)Math.Round(
                    sourceCandidate.RectW > 0
                        ? sourceCandidate.RectW
                        : working.Width),
                1,
                working.Width - sourceCropX);

        int sourceCropH =
            Math.Clamp(
                (int)Math.Round(
                    sourceCandidate.RectH > 0
                        ? sourceCandidate.RectH
                        : working.Height),
                1,
                working.Height - sourceCropY);

        Console.WriteLine(
            $"  Source crop : " +
            $"{sourceCropX},{sourceCropY} " +
            $"{sourceCropW}x{sourceCropH}");

        working.Mutate(ctx =>
            ctx.Crop(
                new Rectangle(
                    sourceCropX,
                    sourceCropY,
                    sourceCropW,
                    sourceCropH)));

        int targetRectW =
            Math.Max(
                1,
                (int)Math.Round(
                    target.RectW > 0
                        ? target.RectW
                        : targetWidth));

        int targetRectH =
            Math.Max(
                1,
                (int)Math.Round(
                    target.RectH > 0
                        ? target.RectH
                        : targetHeight));

        double scale =
            Math.Min(
                (double)targetRectW / working.Width,
                (double)targetRectH / working.Height);

        int newWidth =
            Math.Max(
                1,
                (int)Math.Round(
                    working.Width * scale));

        int newHeight =
            Math.Max(
                1,
                (int)Math.Round(
                    working.Height * scale));

        Console.WriteLine(
            $"  Source  : " +
            $"{working.Width}x{working.Height}");

        Console.WriteLine(
            $"  Sprite  : " +
            $"{targetRectW}x{targetRectH} @ " +
            $"({target.RectX:F0},{target.RectY:F0})");

        Console.WriteLine(
            $"  Fit     : " +
            $"{newWidth}x{newHeight}");

        working.Mutate(ctx =>
            ctx.Resize(newWidth, newHeight));

        using var canvas =
            new Image<Rgba32>(
                targetWidth,
                targetHeight,
                Color.Transparent);

        int targetX =
            (int)Math.Round(
                target.RectX +
                (targetRectW - newWidth) / 2.0);

        int targetY =
            targetHeight -
            (int)Math.Round(
                target.RectY +
                (targetRectH - newHeight) / 2.0) -
            newHeight;

        Console.WriteLine(
            $"  Position: x={targetX}, y={targetY}");

        canvas.Mutate(ctx =>
            ctx.DrawImage(
                working,
                new Point(targetX, targetY),
                1f));

        return canvas.Clone();
    }
}
