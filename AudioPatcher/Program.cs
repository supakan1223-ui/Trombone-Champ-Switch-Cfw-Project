using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Linq;
using System.Diagnostics;

class Program
{
    static float GetAudioDuration(string audioPath)
{
    var psi = new ProcessStartInfo
    {
        FileName = "ffprobe.exe",
        Arguments =
            $"-v error -show_entries format=duration " +
            $"-of default=noprint_wrappers=1:nokey=1 \"{audioPath}\"",

        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(psi)!;

    string output = process.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();
    

    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new Exception(
            $"ffprobe failed:\n{error}");
    }

    if (!float.TryParse(
        output.Trim(),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture,
        out float duration))
    {
        throw new Exception(
            $"ไม่สามารถอ่านความยาว audio ได้: {output}");
    }

    return duration;
}

    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("AudioPatcher <bundle_path> <new_audio_file>");
            return;
        }

        string bundlePath = args[0];
        string newAudioPath = args[1];
        string targetClipName = "track_" + Path.GetFileNameWithoutExtension(bundlePath);

        var am = new AssetsManager();

        var bunInst = am.LoadBundleFile(bundlePath);
        var afile = am.LoadAssetsFileFromBundle(bunInst, 0);

        AssetFileInfo? audioClipInfo = null;

        foreach (var info in afile.file.GetAssetsOfType(AssetClassID.AudioClip))
{
    var baseField = am.GetBaseField(afile, info);

    try
    {
        var name = baseField["m_Name"].AsString;

        

        Console.WriteLine("Found AudioClip: " + name);

        if (name == targetClipName)
{
    audioClipInfo = info;
    break;
}
    }
    catch
    {
        Console.WriteLine("AudioClip found, but name could not be read.");
    }
}
        if (audioClipInfo == null)
        {
            Console.WriteLine(
    $"AudioClip '{targetClipName}' not found!");
            return;
        }

        var clipField = am.GetBaseField(afile, audioClipInfo);

        Console.WriteLine($"=== {targetClipName} ===");
        Console.WriteLine("Old m_Size    : " +
            clipField["m_Resource"]["m_Size"].AsLong);

        Console.WriteLine("Old m_Offset  : " +
            clipField["m_Resource"]["m_Offset"].AsLong);
            Console.WriteLine("m_Source    : " +
    clipField["m_Resource"]["m_Source"].AsString);

        Console.WriteLine("New audio size: " +
            new System.IO.FileInfo(newAudioPath).Length);

        Console.WriteLine();
        Console.WriteLine("Asset file:");
        Console.WriteLine(afile.name);

        Console.WriteLine();
        Console.WriteLine("Bundle:");
        Console.WriteLine(bunInst.name);
        Console.WriteLine("=== Bundle Directory ===");

foreach (var dir in bunInst.file.BlockAndDirInfo.DirectoryInfos)
{
    Console.WriteLine(
    $"Name={dir.Name} | Offset={dir.Offset} | Size={dir.DecompressedSize} | Replacer={dir.Replacer}"
);
}
var resourceDir = bunInst.file.BlockAndDirInfo.DirectoryInfos
    .FirstOrDefault(x => x.Name.EndsWith(".resource"));

if (resourceDir == null)
{
    Console.WriteLine("ERROR: .resource not found.");
    return;
}

Console.WriteLine();
Console.WriteLine("=== Resource Target ===");
Console.WriteLine("Name: " + resourceDir.Name);
Console.WriteLine("Old Size: " + resourceDir.DecompressedSize);

// โหลด golden.ogg เข้า memory
byte[] newAudioData = File.ReadAllBytes(newAudioPath);

// สร้าง replacer
resourceDir.Replacer = new ContentReplacerFromBuffer(newAudioData);

Console.WriteLine("New Size: " + newAudioData.Length);
Console.WriteLine("Replacer assigned successfully.");
// แก้ m_Size ของ AudioClip
clipField["m_Resource"]["m_Size"].AsLong = newAudioData.Length;

float duration = GetAudioDuration(newAudioPath);

clipField["m_Length"].AsFloat = duration;

Console.WriteLine("New m_Length : " + duration);
Console.WriteLine();
Console.WriteLine("=== Modified AudioClip ===");
Console.WriteLine("New m_Size   : " +
    clipField["m_Resource"]["m_Size"].AsLong);
Console.WriteLine("m_Offset     : " +
    clipField["m_Resource"]["m_Offset"].AsLong);
    byte[] modifiedClipData = clipField.WriteToByteArray();

audioClipInfo.Replacer =
    new ContentReplacerFromBuffer(modifiedClipData);

Console.WriteLine("AudioClip replacer assigned.");
Console.WriteLine("Modified AudioClip size: " +
    modifiedClipData.Length);
    Console.WriteLine();
Console.WriteLine("=== Write Test Info ===");
Console.WriteLine("Bundle file type: " + bunInst.file.GetType().FullName);
Console.WriteLine("Asset file type: " + afile.file.GetType().FullName);
// ==========================================
// สร้าง CAB ที่แก้ไขแล้วเป็นไฟล์ชั่วคราว
// ==========================================

string tempAssetsPath = Path.Combine(
    Path.GetDirectoryName(bundlePath)!,
    "temp_modified_assets"
);

Console.WriteLine();
Console.WriteLine("=== Writing Modified CAB ===");
Console.WriteLine("Temp: " + tempAssetsPath);

using (var writer = new AssetsFileWriter(tempAssetsPath))
{
    afile.file.Write(writer, 0);
}

byte[] modifiedAssetsData = File.ReadAllBytes(tempAssetsPath);

Console.WriteLine(
    "Modified CAB size: " + modifiedAssetsData.Length
);

// หา directory entry ของ CAB
var cabDir = bunInst.file.BlockAndDirInfo.DirectoryInfos
    .FirstOrDefault(x =>
        x.Name == afile.name);

if (cabDir == null)
{
    Console.WriteLine("ERROR: CAB directory entry not found.");
    return;
}

Console.WriteLine();
Console.WriteLine("=== CAB Target ===");
Console.WriteLine("Name: " + cabDir.Name);
Console.WriteLine("Old Size: " + cabDir.DecompressedSize);
Console.WriteLine("New Size: " + modifiedAssetsData.Length);

// เอา CAB ที่แก้แล้วเข้า Bundle
cabDir.Replacer =
    new ContentReplacerFromBuffer(modifiedAssetsData);

Console.WriteLine("CAB replacer assigned.");

// ==========================================
// เขียน Bundle ใหม่
// ==========================================

string bundleName = Path.GetFileNameWithoutExtension(bundlePath);

string outputPath = Path.Combine(
    Path.GetDirectoryName(bundlePath)!,
    bundleName + "_patched"
);

Console.WriteLine();
Console.WriteLine("=== Writing Bundle ===");
Console.WriteLine("Output: " + outputPath);

using (var writer = new AssetsFileWriter(outputPath))
{
    bunInst.file.Write(writer, 0);
}

Console.WriteLine("Bundle written successfully.");

// ลบไฟล์ชั่วคราว
File.Delete(tempAssetsPath);

Console.WriteLine("Temporary file deleted.");
    }
    
}