using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json.Linq;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  Convert: Assembly-CSharp.exe convert <input.json> <output.tmb>");
            Console.WriteLine("  Read:    Assembly-CSharp.exe read <input.tmb>");
            return;
        }

        if (args[0] == "read")
        {
            ReadTmb(args[1]);
            return;
        }
        else if (args[0] == "convert")
        {
            ConvertJsonToTmb(args[1], args[2]);
            return;
        }

        Console.WriteLine("Unknown mode: " + args[0]);
    }

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
            if (song.savedleveldata != null && song.savedleveldata.Count > 0)
            {
                Console.WriteLine("  first note: [" + string.Join(", ", song.savedleveldata[0]) + "]");
                Console.WriteLine("  last note: [" + string.Join(", ", song.savedleveldata[song.savedleveldata.Count - 1]) + "]");
            }
            Console.WriteLine("bgdata count: " + (song.bgdata != null ? song.bgdata.Count.ToString() : "null"));
            Console.WriteLine("endpoint: " + song.endpoint);
            Console.WriteLine("improv_zones count: " + (song.improv_zones != null ? song.improv_zones.Count.ToString() : "null"));
            Console.WriteLine("lyricspos count: " + (song.lyricspos != null ? song.lyricspos.Count.ToString() : "null"));
            Console.WriteLine("lyricstxt count: " + (song.lyricstxt != null ? song.lyricstxt.Count.ToString() : "null"));
            Console.WriteLine("note_color_end: " + ArrToStr(song.note_color_end));
            Console.WriteLine("note_color_start: " + ArrToStr(song.note_color_start));
            Console.WriteLine("savednotespacing: " + song.savednotespacing);
            Console.WriteLine("tempo: " + song.tempo);
            Console.WriteLine("timesig: " + song.timesig);
        }
    }

    static string ArrToStr(float[] arr)
    {
        if (arr == null) return "null";
        return "[" + string.Join(", ", arr) + "]";
    }

   static void ConvertJsonToTmb(string inputPath, string outputPath)
{
    string jsonText = File.ReadAllText(inputPath);
    JObject json = JObject.Parse(jsonText);

    SavedLevel song = new SavedLevel();

    song.endpoint = (float)(double)(json["endpoint"] ?? 0.0);
    song.tempo = (float)(double)(json["tempo"] ?? 120.0);           // ← อ่านจาก JSON จริง
    song.timesig = (int)(json["timesig"] ?? 4);                     // ← อ่านจาก JSON จริง
    song.savednotespacing = (int)(json["savednotespacing"] ?? 420); // ← อ่านจาก JSON จริง

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

    BinaryFormatter formatter = new BinaryFormatter();
    using (FileStream fs = new FileStream(outputPath, FileMode.Create))
    {
        formatter.Serialize(fs, song);
    }

    Console.WriteLine("Done! Wrote: " + outputPath);
    Console.WriteLine("  tempo: " + song.tempo);
    Console.WriteLine("  timesig: " + song.timesig);
    Console.WriteLine("  savednotespacing: " + song.savednotespacing);
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
}