using System;

[Serializable]
public class SongDataCustom
{
    public string name;
    public string shortName;
    public string trackref;
    public string author;
    public string description;
    public string genre;
    public string year;
    public string folder;
    public bool bg_hasmovie;
    public bool bg_hasimage;
    public int difficulty;
    public float[][] notes;
    public int tempo;
    public int timesig;
    public float endpoint;
    public int savednotespacing;
    public float[] note_color_start;
    public float[] note_color_end;
    public int bgmove;
}