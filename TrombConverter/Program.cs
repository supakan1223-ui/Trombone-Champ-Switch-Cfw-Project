using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Newtonsoft.Json.Linq;

class Program
{
    // โปรเจกต์นี้ถูก compile ด้วย AssemblyVersion=0.0.0.0 (ตั้งใน .csproj)
    // เพราะ SavedLevel (.tmb) ต้องการ Version=0.0.0.0 ในเกมจริง
    //
    // songdata.tchamp (SongData) ต้องการ Version=1.0.0.0 แทน
    // แก้ปัญหาด้วยการ serialize ตามปกติ (ได้ "0.0.0.0" ติดมาในไฟล์)
    // แล้วแก้ byte ของ string "0.0.0.0" -> "1.0.0.0" หลัง serialize
    // (ความยาวสตริงเท่ากันเป๊ะ 7 ตัวอักษร จึงไม่กระทบ offset ส่วนอื่นในไฟล์)

    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            PrintUsage();
            return;
        }

        try
        {
            switch (args[0])
            {
                case "convert":
                    // convert <chart.json> <output.tmb> <trackref> <songdata.tchamp>
                    if (args.Length < 5) { PrintUsage(); return; }
                    ConvertAll(args[1], args[2], args[3], args[4]);
                    break;

                case "read":
                    if (args.Length < 2) { PrintUsage(); return; }
                    ReadTmb(args[1]);
                    break;

                case "readsongdata":
                    if (args.Length < 2) { PrintUsage(); return; }
                    ReadSongData(args[1]);
                    break;

                default:
                    PrintUsage();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ERROR] " + ex.GetType().FullName + ": " + ex.Message);
            Console.WriteLine(ex.StackTrace);
            Environment.ExitCode = 1;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  Convert (ทำทั้งคู่):");
        Console.WriteLine("    Assembly-CSharp.exe convert <pc_song.tmb> <output.tmb> <trackref> <songdata.tchamp>");
        Console.WriteLine("    (pc_song.tmb คือไฟล์ .tmb จาก TrombLoader บน PC — เนื้อหาเป็น JSON แม้นามสกุลเป็น .tmb)");
        Console.WriteLine("    -> สร้าง <output.tmb> (ไปที่ leveldata/ บน Switch)");
        Console.WriteLine("    -> แก้ <songdata.tchamp> แทนที่ -> <output.tmb-folder>\\songdata_patch.tchamp (ไปที่ leveldata/)");
        Console.WriteLine();
        Console.WriteLine("  Read:          Assembly-CSharp.exe read <input.tmb>");
        Console.WriteLine("  ReadSongData:  Assembly-CSharp.exe readsongdata <songdata.tchamp>");
    }

    // ========================= .tmb (SavedLevel) =========================

    static void ReadTmb(string path)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        using (FileStream fs = new FileStream(path, FileMode.Open))
        {
            object obj = formatter.Deserialize(fs);
            SavedLevel song = obj as SavedLevel;

            if (song == null)
            {
                Console.WriteLine("Deserialized, but type is: " + obj.GetType().FullName);
                return;
            }

            Console.WriteLine("savedleveldata count: " + (song.savedleveldata != null ? song.savedleveldata.Count.ToString() : "null"));
            Console.WriteLine("bgdata count: " + (song.bgdata != null ? song.bgdata.Count.ToString() : "null"));
            Console.WriteLine("endpoint: " + song.endpoint);
            Console.WriteLine("savednotespacing: " + song.savednotespacing);
            Console.WriteLine("tempo: " + song.tempo);
            Console.WriteLine("timesig: " + song.timesig);
        }
    }

    static float[] ReadFloatArray(JToken token)
    {
        if (token == null) return new float[0];
        JArray arr = (JArray)token;
        float[] result = new float[arr.Count];
        for (int i = 0; i < arr.Count; i++)
            result[i] = (float)(double)arr[i];
        return result;
    }

    // ===================== songdata.tchamp (SongData) =====================

    static void ReadSongData(string path)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        object obj;
        using (var fs = new FileStream(path, FileMode.Open))
            obj = formatter.Deserialize(fs);

        var data = obj as SongData;
        if (data == null)
        {
            Console.WriteLine("Deserialize สำเร็จ แต่ type ไม่ตรง: " + obj.GetType().FullName);
            return;
        }

        Console.WriteLine("Row count: " + data.data_tracktitles.Length);

        int maxCols = 0;
        foreach (var r in data.data_tracktitles)
            if (r.Length > maxCols) maxCols = r.Length;
        Console.WriteLine("Max columns found across all rows: " + maxCols);
        Console.WriteLine();

        for (int i = 0; i < data.data_tracktitles.Length; i++)
        {
            var row = data.data_tracktitles[i];
            string trackref = row.Length > 2 ? row[2] : "(none)";
            string name = row.Length > 0 ? row[0] : "(none)";
            Console.WriteLine($"[{i,2}] cols={row.Length,-3} trackref=\"{trackref}\" name=\"{name}\"");

            // พิมพ์ทุกคอลัมน์แบบ index ตรงๆ เพื่อดูว่ามีคอลัมน์เกิน 9 (col 10+) จริงไหม
            for (int c = 0; c < row.Length; c++)
                Console.WriteLine($"      [{i},{c}] = \"{row[c]}\"");
        }

        if (maxCols > 10)
            Console.WriteLine();
        Console.WriteLine($"[NOTE] ถ้า maxCols > 10 แปลว่ามีคอลัมน์ที่ schema เดิม ([0]-[9]) ยังไม่รู้จัก");
        Console.WriteLine("       ต้องดูค่าจริงด้านบนก่อนตัดสินใจว่าคอลัมน์ 10+ คืออะไร แล้วค่อยเพิ่ม field ให้ PatchSongData");
    }

    /// <summary>
    /// Serialize object ใดก็ได้ด้วย BinaryFormatter ปกติ (ได้ Version=0.0.0.0 ของโปรเจกต์นี้)
    /// แล้วแก้ byte ของ "0.0.0.0" -> targetVersion หลัง serialize
    /// ใช้ได้เฉพาะ targetVersion ที่มีความยาวสตริงเท่ากับ "0.0.0.0" (7 ตัวอักษร) เท่านั้น
    /// </summary>
    static byte[] SerializeWithVersion(object graph, string targetVersion)
    {
        const string originalVersion = "0.0.0.0";
        if (targetVersion.Length != originalVersion.Length)
            throw new ArgumentException($"targetVersion ต้องยาว {originalVersion.Length} ตัวอักษรเท่ากับ \"{originalVersion}\" เท่านั้น (ได้ \"{targetVersion}\")");

        BinaryFormatter formatter = new BinaryFormatter();
        byte[] data;
        using (var ms = new MemoryStream())
        {
            formatter.Serialize(ms, graph);
            data = ms.ToArray();
        }

        if (targetVersion == originalVersion)
            return data; // ไม่ต้องแก้อะไร

        byte[] find = Encoding.ASCII.GetBytes(originalVersion);
        byte[] replace = Encoding.ASCII.GetBytes(targetVersion);

        int replacedCount = 0;
        for (int i = 0; i <= data.Length - find.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < find.Length; j++)
            {
                if (data[i + j] != find[j]) { match = false; break; }
            }
            if (match)
            {
                Array.Copy(replace, 0, data, i, replace.Length);
                replacedCount++;
                i += find.Length - 1;
            }
        }

        Console.WriteLine($"  [version patch] แทนที่ \"{originalVersion}\" -> \"{targetVersion}\" จำนวน {replacedCount} จุด");
        if (replacedCount == 0)
            Console.WriteLine("  [WARN] ไม่พบ version string ให้แก้ — ตรวจสอบว่าโปรเจกต์นี้ compile ด้วย AssemblyVersion=0.0.0.0 จริงหรือไม่");

        return data;
    }

    static void PatchSongData(string songdataPath, string outputPath, string trackrefToFind,
        string newName, string newShortname, string newAuthor, string newGenre,
        string newDescription, string newDifficulty, string newTime, string newBpm)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        SongData data;
        using (var fs = new FileStream(songdataPath, FileMode.Open))
        {
            object obj = formatter.Deserialize(fs);
            data = obj as SongData;
            if (data == null)
                throw new Exception("songdata.tchamp deserialize ได้ แต่ type ไม่ตรง: " + obj.GetType().FullName);
        }

        bool found = false;
        foreach (var row in data.data_tracktitles)
        {
            if (row.Length > 2 && row[2] == trackrefToFind)
            {
                Console.WriteLine($"[songdata] พบ trackref=\"{trackrefToFind}\": \"{row[0]}\" -> \"{newName}\"");
                row[0] = newName;
                if (row.Length > 1) row[1] = newShortname;
                if (row.Length > 4) row[4] = newAuthor;
                if (row.Length > 5) row[5] = newGenre;
                if (row.Length > 6) row[6] = newDescription;
                if (row.Length > 7) row[7] = newDifficulty;
                if (row.Length > 8) row[8] = newTime;
                if (row.Length > 9) row[9] = newBpm;
                found = true;
                break;
            }
        }

        if (!found)
            throw new Exception($"[songdata] ไม่พบ trackref '{trackrefToFind}' ใน songdata.tchamp");

        byte[] outBytes = SerializeWithVersion(data, "1.0.0.0");
        File.WriteAllBytes(outputPath, outBytes);
        Console.WriteLine("[songdata] Wrote: " + outputPath);
    }

    // =========================== รวมทั้งคู่ ===========================

    static void ConvertAll(string pcSongTmbPath, string outputTmbPath, string trackref, string songdataPath)
    {
        // pcSongTmbPath: ไฟล์ .tmb จาก PC TrombLoader — นามสกุลเป็น .tmb แต่เนื้อหาเป็น JSON ล้วน
        string jsonText = File.ReadAllText(pcSongTmbPath);
        JObject json = JObject.Parse(jsonText);

        // ---------- 1) .tmb ----------
        SavedLevel song = new SavedLevel();
        song.endpoint = (float)(double)(json["endpoint"] ?? 0.0);
        song.tempo = (float)(double)(json["tempo"] ?? 120.0);
        song.timesig = (int)(json["timesig"] ?? 4);
        song.savednotespacing = (int)(json["savednotespacing"] ?? 420);

        JArray notesArr = (JArray)json["notes"];
        song.savedleveldata = new List<float[]>();
        if (notesArr != null)
        {
            foreach (JArray inner in notesArr)
            {
                float[] row = new float[inner.Count];
                for (int j = 0; j < inner.Count; j++)
                    row[j] = (float)(double)inner[j];
                song.savedleveldata.Add(row);
            }
        }
        song.bgdata = new List<float[]>();
        song.improv_zones = new List<float[]>();
        song.lyricspos = new List<float[]>();
        song.lyricstxt = new List<string>();
        song.note_color_start = ReadFloatArray(json["note_color_start"]);
        song.note_color_end = ReadFloatArray(json["note_color_end"]);

        byte[] tmbBytes = SerializeWithVersion(song, "0.0.0.0"); // ไม่ต้องแก้ ตรงอยู่แล้ว
        File.WriteAllBytes(outputTmbPath, tmbBytes);
        Console.WriteLine("[.tmb] Wrote: " + outputTmbPath);
        Console.WriteLine("  tempo=" + song.tempo + " timesig=" + song.timesig + " savednotespacing=" + song.savednotespacing);

        // ---------- 2) songdata.tchamp ----------
        string name = (string)(json["name"] ?? "");
        string shortname = name.Length > 20 ? name.Substring(0, 20) : name;
        string author = (string)(json["author"] ?? json["charter"] ?? "");
        string genre = (string)(json["genre"] ?? "");
        string description = (string)(json["description"] ?? "");
        string difficulty = ((int)(json["difficulty"] ?? 1)).ToString();

        float timeSeconds = song.tempo > 0 ? (song.endpoint / song.tempo) * 60f : 0f;
        string time = Math.Round(timeSeconds).ToString();
        string bpm = Math.Round(song.tempo).ToString();

        string songdataOutputPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(outputTmbPath)) ?? ".",
            "songdata_patch.tchamp");

        PatchSongData(songdataPath, songdataOutputPath, trackref,
            name, shortname, author, genre, description, difficulty, time, bpm);

        Console.WriteLine();
        Console.WriteLine("=== เสร็จสมบูรณ์ ===");
        Console.WriteLine("  leveldata/<trackref>.tmb     <- " + outputTmbPath);
        Console.WriteLine("  leveldata/songdata.tchamp    <- " + songdataOutputPath);
    }
}