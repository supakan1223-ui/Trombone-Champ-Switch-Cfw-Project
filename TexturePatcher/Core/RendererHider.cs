using AssetsTools.NET;
using AssetsTools.NET.Extra;

// Renderer helper ที่ใช้โดยคำสั่ง patch
static class RendererHider
{
    public static void HideOtherSpriteRenderers(
        AssetsManager manager,
        AssetsFileInstance afileInst,
        long keepGameObjectPathId)
    {
        int hidden = 0;

        foreach (var srInfo in afileInst.file.GetAssetsOfType(AssetClassID.SpriteRenderer))
        {
            try
            {
                var srBase = manager.GetBaseField(afileInst, srInfo);
                long goId = srBase["m_GameObject"]["m_PathID"].AsLong;

                if (goId == keepGameObjectPathId)
                    continue;

                srBase["m_Enabled"].AsBool = false;
                srInfo.Replacer =
                    new ContentReplacerFromBuffer(srBase.WriteToByteArray());

                hidden++;
            }
            catch
            {
                // ข้าม SpriteRenderer ที่มี structure แปลก
            }
        }

        Console.WriteLine($"  ปิด SpriteRenderer ของ Background อื่น {hidden} ตัว");
    }
}
