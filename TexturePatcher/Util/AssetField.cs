using AssetsTools.NET;

// อ่านค่า AssetTypeValueField แบบปลอดภัย
static class AssetField
{
    public static float SafeF(AssetTypeValueField f, float d = 0f)
    {
        try { return f == null ? d : f.AsFloat; } catch { return d; }
    }

    public static long SafeL(AssetTypeValueField f, long d = 0)
    {
        try { return f == null ? d : f.AsLong; } catch { return d; }
    }

    public static int SafeI(AssetTypeValueField f, int d = 0)
    {
        try { return f == null ? d : f.AsInt; } catch { return d; }
    }

    public static bool SafeB(AssetTypeValueField f, bool d = false)
    {
        try { return f == null ? d : f.AsBool; } catch { return d; }
    }

    public static string SafeS(AssetTypeValueField f, string d = "")
    {
        try { return f == null ? d : f.AsString; } catch { return d; }
    }
}
