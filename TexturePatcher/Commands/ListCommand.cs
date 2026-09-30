using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static class ListCommand
{
    public static void ListTextures(string bundlePath)
    {
        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var bun = bunInst.file;

        Console.WriteLine("AssetBundle files:");
        foreach (var dir in bun.BlockAndDirInfo.DirectoryInfos)
            Console.WriteLine($"  {dir.Name} ({dir.DecompressedSize:N0} bytes)");

        Console.WriteLine();
        Console.WriteLine("Texture2D:");

        int assetsFileIndex = 0;
        while (assetsFileIndex < bun.GetAllFileNames().Count)
        {
            try
            {
                var afileInst = manager.LoadAssetsFileFromBundle(bunInst, assetsFileIndex, false);
                if (afileInst == null)
                {
                    assetsFileIndex++;
                    continue;
                }

                foreach (var texInfo in afileInst.file.GetAssetsOfType(AssetClassID.Texture2D))
                {
                    var texBase = manager.GetBaseField(afileInst, texInfo);
                    string name = texBase["m_Name"].AsString;
                    int width = texBase["m_Width"].AsInt;
                    int height = texBase["m_Height"].AsInt;
                    int format = texBase["m_TextureFormat"].AsInt;
                    Console.WriteLine($"  {name} | {width}x{height} | TextureFormat={format} | PathID={texInfo.PathId}");
                }
            }
            catch { }

            assetsFileIndex++;
        }
    }
}
