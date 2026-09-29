using AssetsTools.NET;
using AssetsTools.NET.Extra;

// ปิด SpriteRenderer ตัวอื่นที่ "ไม่ใช่" texture ที่เพิ่ง patch ไป โดยเช็คจากชื่อ Texture2D
// (ต่างจาก Commands/HideCommand.cs ที่เช็คจากชื่อ GameObject และรองรับ renderer หลายชนิดกว่า)
// ใช้ภายในคำสั่ง patch เท่านั้น (ดู Commands/PatchCommand.cs)
static class RendererHider
{
    public static void HideOtherSpriteRenderers(
        AssetsManager manager,
        AssetsFileInstance afileInst,
        string keepTextureName)
    {
        int hidden = 0;

        foreach (var srInfo in afileInst.file.GetAssetsOfType(AssetClassID.SpriteRenderer))
        {
            try
            {
                var srBase = manager.GetBaseField(afileInst, srInfo);

                long spritePathId = srBase["m_Sprite"]["m_PathID"].AsLong;
                if (spritePathId == 0) continue;

                var spriteInfo = afileInst.file.GetAssetInfo(spritePathId);
                if (spriteInfo == null) continue;
                var spriteBase = manager.GetBaseField(afileInst, spriteInfo);

                long texturePathId = spriteBase["m_RD"]["texture"]["m_PathID"].AsLong;
                if (texturePathId == 0) continue;

                var textureInfo = afileInst.file.GetAssetInfo(texturePathId);
                if (textureInfo == null) continue;
                var textureBase = manager.GetBaseField(afileInst, textureInfo);

                string textureName = textureBase["m_Name"].AsString;

                // ตัวที่เป็น texture ใหม่ของเรา ห้ามปิด
                if (string.Equals(textureName, keepTextureName, StringComparison.Ordinal))
                    continue;

                srBase["m_Enabled"].AsBool = false;
                srInfo.Replacer = new ContentReplacerFromBuffer(srBase.WriteToByteArray());
                hidden++;

                Console.WriteLine($"  [HIDE] SpriteRenderer -> Texture2D '{textureName}'");
            }
            catch
            {
                // SpriteRenderer บางชนิดอาจมี structure ต่างกัน ข้ามตัวที่อ่านไม่ได้
            }
        }

        Console.WriteLine($"  ปิด SpriteRenderer เดิมแล้ว {hidden} ตัว");
    }

    public static void SetDisabled(AssetTypeValueField f)
    {
        try { f.AsBool = false; return; } catch { }
        try { f.AsInt = 0; return; } catch { }
        f.AsUInt = 0;
    }
}
