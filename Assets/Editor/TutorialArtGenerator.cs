using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// วาดภาพประกอบบทช่วยสอนด้วยโค้ด
///
/// วาดเองแทนการหาภาพมาใส่ เพราะ
/// 1. ภาพต้องตรงกับกติกาในเกมเป๊ะ ๆ ถ้าเอาภาพสำเร็จรูปมาจะไม่ตรง
/// 2. แก้กติกาแล้วแก้ภาพตามได้ทันทีที่เดียว
/// 3. ไม่มีเรื่องลิขสิทธิ์ให้กังวล
///
/// ภาพเป็นสัญลักษณ์ล้วน ไม่มีตัวหนังสือในภาพ จึงใช้ได้ทั้งไทยและอังกฤษ
/// โดยไม่ต้องทำภาพสองชุด ตัวหนังสืออยู่ในตารางแปลแยกต่างหาก
/// </summary>
public static class TutorialArtGenerator
{
    private const string Folder = "Assets/Art/Generated/Tutorial";
    private const int Width = 640;
    private const int Height = 360;

    // สีเดียวกับที่ใช้ในเกมจริง ผู้เล่นจะได้จำสีธาตุได้ตั้งแต่อ่านวิธีเล่น
    private static readonly Color32 Ink = new Color32(240, 244, 252, 255);
    private static readonly Color32 Dim = new Color32(120, 132, 156, 255);
    private static readonly Color32 Accent = new Color32(251, 169, 64, 255);
    private static readonly Color32 Water = new Color32(77, 166, 255, 255);
    private static readonly Color32 Fire = new Color32(255, 115, 38, 255);
    private static readonly Color32 Earth = new Color32(166, 128, 64, 255);
    private static readonly Color32 Wind = new Color32(179, 255, 217, 255);

    public static Sprite[] CreateAll()
    {
        EnsureFolder();

        return new[]
        {
            Bake("tut_draw", DrawPageDrawing),
            Bake("tut_shapes", DrawPageShapes),
            Bake("tut_shield", DrawPageShield),
            Bake("tut_fire", DrawPageFire),
            Bake("tut_counter", DrawPageCounter),
        };
    }

    // ---------- หน้าที่ 1: ลากเมาส์วาด ----------

    private static void DrawPageDrawing(Color32[] px)
    {
        var center = new Vector2(Width * 0.55f, Height * 0.5f);

        // วงกลมที่กำลังวาด ลากไม่ครบรอบเพื่อให้เห็นว่ากำลังลากอยู่
        Circle(px, center, 95f, 3.5f, Accent, 0.08f, 0.92f);

        // ลูกศรเมาส์ที่ปลายเส้น
        float endAngle = Mathf.PI * 2f * 0.92f;
        var tip = center + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * 95f;
        Cursor(px, tip);

        // เส้นประบอกทิศการลาก
        Circle(px, center, 118f, 1.6f, Dim, 0.1f, 0.75f, dashed: true);
    }

    // ---------- หน้าที่ 2: รูปทรงกับธาตุ ----------

    private static void DrawPageShapes(Color32[] px)
    {
        float y = Height * 0.52f;
        float step = Width / 4f;
        float r = 52f;

        Circle(px, new Vector2(step * 0.5f, y), r, 4f, Water);
        Polygon(px, new Vector2(step * 1.5f, y), r, 3, 4f, Fire, Mathf.PI * 0.5f);
        Polygon(px, new Vector2(step * 2.5f, y), r, 4, 4f, Earth, Mathf.PI * 0.25f);

        // ธาตุลมคือขีดตรงสี่ขีด ไม่ใช่รูปปิด
        var windCenter = new Vector2(step * 3.5f, y);
        for (int i = 0; i < 4; i++)
        {
            float offset = (i - 1.5f) * 22f;
            Line(px,
                windCenter + new Vector2(offset - 14f, -r * 0.8f),
                windCenter + new Vector2(offset + 14f, r * 0.8f),
                4f, Wind);
        }
    }

    // ---------- หน้าที่ 3: วาดทับตัวเอง = โล่ ----------

    private static void DrawPageShield(Color32[] px)
    {
        var hero = new Vector2(Width * 0.5f, Height * 0.46f);

        // วงที่วาดครอบตัวเอง วาดก่อนเพื่อให้ตัวละครอยู่ทับด้านบน
        Circle(px, hero + new Vector2(0f, 12f), 108f, 5f, Water);
        Circle(px, hero + new Vector2(0f, 12f), 122f, 2f, Water, dashed: true);

        StickFigure(px, hero, 1f, Ink);
        Cursor(px, hero + new Vector2(76f, 88f));
    }

    // ---------- หน้าที่ 4: วาดข้าง ๆ แล้วยิง ----------

    private static void DrawPageFire(Color32[] px)
    {
        var hero = new Vector2(Width * 0.22f, Height * 0.46f);
        StickFigure(px, hero, 1f, Ink);

        // รูปที่วาดอยู่ข้าง ๆ ตัว ไม่ทับตัว
        Polygon(px, new Vector2(Width * 0.5f, Height * 0.55f), 48f, 3, 4f, Fire, Mathf.PI * 0.5f);

        // ลูกศรบอกทิศที่เวทจะพุ่งออกไป
        Arrow(px,
            new Vector2(Width * 0.62f, Height * 0.5f),
            new Vector2(Width * 0.92f, Height * 0.5f),
            4f, Accent);

        // หลอดเสียงเล็ก ๆ พร้อมเส้นเกณฑ์ บอกว่าต้องพูดให้ถึงเส้น
        VoiceBar(px, new Vector2(Width * 0.5f, Height * 0.16f));
    }

    // ---------- หน้าที่ 5: ธาตุแก้กัน ----------

    private static void DrawPageCounter(Color32[] px)
    {
        var center = new Vector2(Width * 0.5f, Height * 0.5f);
        float ring = 108f;

        Color32[] colors = { Water, Fire, Wind, Earth };
        var points = new Vector2[4];

        for (int i = 0; i < 4; i++)
        {
            float angle = Mathf.PI * 0.5f - i * Mathf.PI * 0.5f;
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
            Disc(px, points[i], 30f, colors[i]);
        }

        // ลูกศรวนบอกว่าใครชนะใคร น้ำ->ไฟ->ลม->ดิน->น้ำ
        for (int i = 0; i < 4; i++)
        {
            Vector2 from = points[i];
            Vector2 to = points[(i + 1) % 4];

            Vector2 dir = (to - from).normalized;
            Arrow(px, from + dir * 38f, to - dir * 38f, 3f, Dim);
        }
    }

    // ---------- เครื่องมือวาด ----------

    private static void Circle(
        Color32[] px, Vector2 center, float radius, float thickness, Color32 color,
        float startTurn = 0f, float endTurn = 1f, bool dashed = false)
    {
        const int steps = 220;
        int from = Mathf.RoundToInt(steps * startTurn);
        int to = Mathf.RoundToInt(steps * endTurn);

        for (int i = from; i < to; i++)
        {
            // เว้นช่วงให้เป็นเส้นประ ใช้บอกว่าเส้นนั้นเป็นแค่แนวคิด ไม่ใช่ของจริง
            if (dashed && (i / 8) % 2 == 1) continue;

            float a0 = (float)i / steps * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / steps * Mathf.PI * 2f;

            Line(px,
                center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
                center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
                thickness, color);
        }
    }

    private static void Polygon(
        Color32[] px, Vector2 center, float radius, int sides, float thickness,
        Color32 color, float rotation)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = rotation + (float)i / sides * Mathf.PI * 2f;
            float a1 = rotation + (float)(i + 1) / sides * Mathf.PI * 2f;

            Line(px,
                center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
                center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
                thickness, color);
        }
    }

    private static void Disc(Color32[] px, Vector2 center, float radius, Color32 color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
        int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt(center.x + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
        int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(center.y + radius));

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
            float alpha = Mathf.Clamp01(radius - d);
            if (alpha <= 0f) continue;

            Blend(px, y * Width + x, color, (byte)(alpha * 255f));
        }
    }

    /// <summary>ลูกศรเมาส์ วาดเป็นสามเหลี่ยมโปร่งแบบไอคอนเคอร์เซอร์</summary>
    private static void Cursor(Color32[] px, Vector2 tip)
    {
        Vector2 a = tip;
        Vector2 b = tip + new Vector2(4f, -30f);
        Vector2 c = tip + new Vector2(15f, -20f);

        Line(px, a, b, 3f, Ink);
        Line(px, b, c, 3f, Ink);
        Line(px, c, a, 3f, Ink);
    }

    private static void Arrow(Color32[] px, Vector2 from, Vector2 to, float thickness, Color32 color)
    {
        Line(px, from, to, thickness, color);

        Vector2 dir = (to - from).normalized;
        Vector2 side = new Vector2(-dir.y, dir.x);

        Line(px, to, to - dir * 18f + side * 11f, thickness, color);
        Line(px, to, to - dir * 18f - side * 11f, thickness, color);
    }

    private static void StickFigure(Color32[] px, Vector2 feet, float scale, Color32 color)
    {
        float h = 96f * scale;
        Vector2 hip = feet + new Vector2(0f, h * 0.45f);
        Vector2 shoulder = feet + new Vector2(0f, h * 0.85f);

        Circle(px, feet + new Vector2(0f, h), h * 0.16f, 3.5f, color);
        Line(px, shoulder, hip, 3.5f, color);
        Line(px, shoulder, shoulder + new Vector2(-26f, -22f) * scale, 3.5f, color);
        Line(px, shoulder, shoulder + new Vector2(26f, -22f) * scale, 3.5f, color);
        Line(px, hip, hip + new Vector2(-20f, -40f) * scale, 3.5f, color);
        Line(px, hip, hip + new Vector2(20f, -40f) * scale, 3.5f, color);
    }

    /// <summary>หลอดเสียงย่อส่วน พร้อมเส้นเกณฑ์ที่ต้องพูดให้ถึง</summary>
    private static void VoiceBar(Color32[] px, Vector2 center)
    {
        const float w = 190f;
        const float h = 26f;

        Vector2 min = center - new Vector2(w * 0.5f, h * 0.5f);

        // กรอบหลอด
        Line(px, min, min + new Vector2(w, 0f), 2f, Dim);
        Line(px, min + new Vector2(w, 0f), min + new Vector2(w, h), 2f, Dim);
        Line(px, min + new Vector2(w, h), min + new Vector2(0f, h), 2f, Dim);
        Line(px, min + new Vector2(0f, h), min, 2f, Dim);

        // ส่วนที่เต็มอยู่
        for (float x = 2f; x < w * 0.62f; x += 1f)
            Line(px, min + new Vector2(x, 2f), min + new Vector2(x, h - 2f), 1f, Accent);

        // เส้นเกณฑ์ที่ต้องพูดให้ถึง
        Line(px, min + new Vector2(w * 0.62f, -6f), min + new Vector2(w * 0.62f, h + 6f), 3f, Ink);
    }

    private static void Line(Color32[] px, Vector2 from, Vector2 to, float radius, Color32 color)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance));

        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, (float)i / steps);

            int minX = Mathf.Max(0, Mathf.FloorToInt(p.x - radius));
            int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt(p.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(p.y - radius));
            int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(p.y + radius));

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), p);
                float alpha = Mathf.Clamp01(radius - d);
                if (alpha <= 0f) continue;

                Blend(px, y * Width + x, color, (byte)(alpha * 255f));
            }
        }
    }

    /// <summary>ผสมสีลงพิกเซล ความทึบเอาค่าที่มากกว่าเพื่อให้ขอบเส้นเนียน</summary>
    private static void Blend(Color32[] px, int index, Color32 color, byte alpha)
    {
        if (alpha == 0) return;

        Color32 dst = px[index];

        if (dst.a == 0)
        {
            px[index] = new Color32(color.r, color.g, color.b, alpha);
            return;
        }

        float srcA = alpha / 255f;
        float invA = 1f - srcA;

        px[index] = new Color32(
            (byte)(color.r * srcA + dst.r * invA),
            (byte)(color.g * srcA + dst.g * invA),
            (byte)(color.b * srcA + dst.b * invA),
            alpha > dst.a ? alpha : dst.a);
    }

    // ---------- อบเป็นไฟล์ ----------

    private static Sprite Bake(string name, System.Action<Color32[]> draw)
    {
        var pixels = new Color32[Width * Height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

        draw(pixels);

        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();

        string path = $"{Folder}/{name}.png";
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
            AssetDatabase.CreateFolder("Assets/Art/Generated", "Tutorial");
    }
}
