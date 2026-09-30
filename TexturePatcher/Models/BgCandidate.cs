// ผลการวิเคราะห์ SpriteRenderer -> Sprite -> Texture2D
// ใช้ร่วมกันทั้ง analyze / inspect / patchbg
using AssetsTools.NET;
using AssetsTools.NET.Extra;

sealed record BackgroundCandidate(
    string TextureName,
    int Width,
    int Height,
    int TextureFormat,
    string SpriteName,
    string GameObjectName,
    long GameObjectPathId,
    bool Enabled,
    float ScaleX,
    float ScaleY,
    float RectX,
    float RectY,
    float RectW,
    float RectH,
    double Score,
    AssetsFileInstance AssetsFile,
    AssetFileInfo SpriteRendererInfo);

sealed record BackgroundTarget(
    string TextureName,
    string SpriteName,
    string GameObjectName,
    long GameObjectPathId,
    int Width,
    int Height,
    int TextureFormat,
    bool Enabled,
    float RectX,
    float RectY,
    float RectW,
    float RectH,
    float ScaleX,
    float ScaleY,
    double Score);
