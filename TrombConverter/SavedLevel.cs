using System;
using System.Collections.Generic;

[Serializable]
public class SavedLevel
{
    public List<float[]> savedleveldata;
    public List<float[]> bgdata;
    public float endpoint;
    public List<float[]> improv_zones;
    public List<float[]> lyricspos;
    public List<string> lyricstxt;
    public float[] note_color_end;
    public float[] note_color_start;
    public int savednotespacing;
    public float tempo;
    public int timesig;
}