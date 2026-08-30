using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// วาดธงชาติกับไอคอนฟันเฟืองด้วยโค้ด
///
/// วาดเองด้วยเหตุผลเดียวกับภาพบทช่วยสอน คือไม่ต้องกังวลเรื่องลิขสิทธิ์
/// และแก้ที่เดียวได้ทั้งโปรเจกต์ แต่ที่นี่มีเหตุผลเพิ่มอีกข้อ
///
/// สัญลักษณ์พวกนี้มีไว้ให้คนที่ "อ่านข้อความในเกมไม่ออก" ใช้
/// ถ้าเขาอ่านไม่ออก ทางเดียวที่จะกลับไปภาษาที่ตัวเองอ่านออกได้
/// คือดูจากรูป ธงกับฟันเฟืองจึงต้องอยู่ในเกมเสมอ ห้ามหายไปพร้อมแพ็กเกจอื่น
/// </summary>
public static class IconArtGenerator
{
    private const string Folder = "Assets/Art/Generated/Icons";

    /// <summary>ธงไทย ใช้แทนภาษาไทย</summary>
    public static Sprite ThaiFlag() { return Bake("flag_th", 96, 64, DrawThaiFlag); }

    /// <summary>ธงสหราชอาณาจักร ใช้แทนภาษาอังกฤษตามที่นิยมใช้กันทั่วไป</summary>
    public static Sprite EnglishFlag() { return Bake("flag_en", 96, 64, DrawUnionFlag); }

    /// <summary>ฟันเฟือง สัญลักษณ์สากลของการตั้งค่า</summary>
    public static Sprite GearIcon() { return Bake("icon_gear", 64, 64, DrawGear); }

    // ---------- ธงไทย ----------

    private static void DrawThaiFlag(Color32[] px, int w, int h)
    {
        // แถบสัดส่วน 1:1:2:1:1 จากบนลงล่าง แดง ขาว น้ำเงิน ขาว แดง
        var red = new Color32(165, 25, 49, 255);
        var white = new Color32(244, 245, 248, 255);
        var blue = new Color32(45, 42, 74, 255);

        for (int y = 0; y < h; y++)
        {
            // y = 0 คือแถวล่างสุดของเทกซ์เจอร์ พลิกให้แถบแรกอยู่บน
            float t = 1f - (y + 0.5f) / h;

            Color32 stripe;
            if (t < 1f / 6f) stripe = red;
            else if (t < 2f / 6f) stripe = white;
            else if (t < 4f / 6f) stripe = blue;
            else if (t < 5f / 6f) stripe = white;
            else stripe = red;

            for (int x = 0; x < w; x++) px[y * w + x] = stripe;
        }

        Border(px, w, h, new Color32(30, 34, 46, 255));
    }

    // ---------- ธงสหราชอาณาจักร ----------

    private static void DrawUnionFlag(Color32[] px, int w, int h)
    {
        var navy = new Color32(1, 33, 105, 255);
        var white = new Color32(244, 245, 248, 255);
        var red = new Color32(200, 16, 46, 255);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w;
                float ny = (y + 0.5f) / h;

                Color32 c = navy;

                // กากบาททแยงมุม วัดจากระยะถึงเส้นทแยงทั้งสองเส้นในพิกัดปรับให้เป็นสี่เหลี่ยมจัตุรัส
                // ไม่งั้นธงที่กว้างกว่าสูงจะทำให้เส้นทแยงหนาไม่เท่ากันสองฝั่ง
                float diagonal = Mathf.Min(Mathf.Abs(nx - ny), Mathf.Abs(nx + ny - 1f));
                if (diagonal < 0.155f) c = white;
                if (diagonal < 0.062f) c = red;

                // กากบาทตั้งฉากวาดทับทแยง เส้นขาวหนากว่าเส้นแดงเพื่อให้เห็นขอบขาว
                float dx = Mathf.Abs(nx - 0.5f);
                float dy = Mathf.Abs(ny - 0.5f);
                if (dx < 0.115f || dy < 0.155f) c = white;
                if (dx < 0.068f || dy < 0.092f) c = red;

                px[y * w + x] = c;
            }
        }

        Border(px, w, h, new Color32(30, 34, 46, 255));
    }

    // ---------- ฟันเฟือง ----------

    private static void DrawGear(Color32[] px, int w, int h)
    {
        var ink = new Color32(60, 52, 44, 255);

        var center = new Vector2(w * 0.5f, h * 0.5f);
        float outer = w * 0.42f;
        float inner = w * 0.30f;
        float hole = w * 0.13f;
        const int teeth = 8;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float dist = Vector2.Distance(p, center);

                // ขอบนอกขยับเข้าออกตามมุม ทำให้เกิดฟันรอบวง
                float angle = Mathf.Atan2(p.y - center.y, p.x - center.x);
                float wave = Mathf.Cos(angle * teeth);
                float edge = Mathf.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, wave * 0.5f + 0.5f));

                bool solid = dist <= edge && dist >= hole;
                px[y * w + x] = solid ? ink : new Color32(0, 0, 0, 0);
            }
        }
    }

    // ---------- ตัวช่วย ----------

    /// <summary>เส้นขอบบาง ๆ กันธงพื้นสีอ่อนกลืนไปกับพื้นหลังการ์ด</summary>
    private static void Border(Color32[] px, int w, int h, Color32 color)
    {
        for (int x = 0; x < w; x++)
        {
            px[x] = color;
            px[(h - 1) * w + x] = color;
        }

        for (int y = 0; y < h; y++)
        {
            px[y * w] = color;
            px[y * w + w - 1] = color;
        }
    }

    private static Sprite Bake(string name, int w, int h, System.Action<Color32[], int, int> draw)
    {
        EnsureFolder();

        var pixels = new Color32[w * h];
        draw(pixels, w, h);

        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();

        string path = Folder + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder("Assets/Art/Generated"))
            AssetDatabase.CreateFolder("Assets/Art", "Generated");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Art/Generated", "Icons");
    }
}
