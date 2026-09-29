// ผลลัพธ์ 1 แถวจากการไล่ SpriteRenderer -> Sprite -> Texture2D ใน AssetBundle
// ใช้ร่วมกันทั้ง analyze / inspect / patchbg
sealed class BgCandidate
{
    public string GameObject = "";
    public string Sprite = "";
    public string Texture = "";
    public int TexW, TexH, TexFormat;
    public float RectX, RectY, RectW, RectH;
    public float Ppu = 100f;
    public float PosX, PosY, PosZ;         // world position (ประมาณ ไม่คิด rotation)
    public float ScaleX = 1f, ScaleY = 1f; // world scale
    public float WorldW, WorldH;           // ขนาดที่แสดงจริงในหน่วย Unity
    public int DrawMode;
    public bool Enabled, GoActive;
    public int SortOrder;
    public string File = "";
    public double Score;
    public string Note = "";

    public float WorldArea => WorldW * WorldH;

    // ลดน้ำหนักของ sprite ที่ถูกขยายเกิน 1 เท่า (glow/overlay ที่ยืดจาก texture เล็ก)
    public float EffArea
    {
        get
        {
            float st = Math.Max(Math.Abs(ScaleX), Math.Abs(ScaleY));
            return st > 1f ? WorldArea / st : WorldArea;
        }
    }

    public long TexArea => (long)TexW * TexH;
}
