using System.Collections.Generic;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// แปลงชุดเส้นที่ผู้เล่นวาด ให้กลายเป็นภาพตัวละคร
    ///
    /// ใช้ร่วมกันทั้งพรีวิวในเมนูและตัวละครจริงในเกม จะได้เห็นเหมือนกันเป๊ะ
    /// ไม่ใช่วาดในเมนูอย่างหนึ่งแล้วเข้าเกมได้อีกอย่าง
    ///
    /// วาดลงพิกเซลเองแทนการใช้ LineRenderer เพราะผลลัพธ์ต้องเป็น Sprite
    /// ที่เอาไปใส่ SpriteRenderer ได้ ระบบพลิกซ้ายขวา ย้อมสี และอนิเมชัน
    /// ที่มีอยู่แล้วจึงใช้ได้ทันทีโดยไม่ต้องแก้อะไร
    /// </summary>
    public static class AppearanceRenderer
    {
        /// <summary>ขนาดภาพที่อบออกมา กำลังพอดีระหว่างความคมกับหน่วยความจำ</summary>
        public const int TextureSize = 256;

        /// <summary>ความหนาเส้นเริ่มต้น เทียบกับความกว้างภาพ (เป็นรัศมี)</summary>
        public const float DefaultThicknessRatio = 0.012f;

        // ใช้บัฟเฟอร์เดิมซ้ำ ไม่จองใหม่ทุกครั้งที่วาด
        // ภาพ 256x256 คือ 65,536 ช่อง ถ้าจองใหม่ทุกจุดที่ลากเมาส์
        // ตัวเก็บขยะจะทำงานถี่จนเห็นเป็นอาการกระตุก
        private static Color32[] canvas;

        // ที่พักของเส้นทีละเส้น เก็บแค่ความเข้ม ไม่เก็บสี
        // เพราะทั้งเส้นใช้สีเดียวกันอยู่แล้ว ประหยัดหน่วยความจำสี่เท่า
        private static byte[] coverage;

        private static int bufferSize;

        /// <summary>
        /// วาดลงเท็กซ์เจอร์ที่มีอยู่แล้ว ไม่สร้างใบใหม่
        /// ใช้ตอนวาดสด ๆ ที่ต้องอัปเดตหลายสิบครั้งต่อวินาที
        /// </summary>
        public static void BakeInto(Texture2D target, IReadOnlyList<AppearanceStroke> strokes)
        {
            if (target == null) return;

            int size = target.width;
            EnsureBuffers(size);

            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < canvas.Length; i++) canvas[i] = clear;

            if (strokes != null)
            {
                foreach (AppearanceStroke stroke in strokes)
                    if (stroke.IsValid) PaintStroke(size, stroke);
            }

            target.SetPixels32(canvas);
            target.Apply();
        }

        public static Texture2D BakeTexture(IReadOnlyList<AppearanceStroke> strokes, int size = TextureSize)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            BakeInto(texture, strokes);
            return texture;
        }

        public static Sprite BakeSprite(
            IReadOnlyList<AppearanceStroke> strokes,
            float pixelsPerUnit,
            int size = TextureSize)
        {
            Texture2D texture = BakeTexture(strokes, size);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
        }

        private static void EnsureBuffers(int size)
        {
            if (canvas != null && bufferSize == size) return;

            canvas = new Color32[size * size];
            coverage = new byte[size * size];
            bufferSize = size;
        }

        /// <summary>
        /// วาดหนึ่งเส้นแล้วผสมลงภาพรวม
        ///
        /// ต้องวาดลงที่พักก่อนแล้วค่อยผสม ไม่ใช่แต้มลงภาพรวมตรง ๆ เพราะ
        /// การแต้มหนึ่งเส้นคือการวางวงกลมซ้อนกันถี่ ๆ ตลอดแนว ถ้าผสมทุกครั้ง
        /// ที่วาง ขอบเส้นจะทึบขึ้นเรื่อย ๆ จนดูหนากว่าที่ตั้งไว้และขอบแข็ง
        ///
        /// ที่พักใช้กฎ "เอาค่าที่เข้มกว่า" ซึ่งให้ขอบเนียน แล้วค่อยผสมทั้งเส้น
        /// ลงภาพรวมครั้งเดียว เส้นที่วาดทีหลังจึงทับเส้นเก่าได้ถูกต้องเมื่อคนละสี
        /// </summary>
        private static void PaintStroke(int size, AppearanceStroke stroke)
        {
            float radius = Mathf.Max(1f, size * stroke.Thickness);

            // ล้างและผสมเฉพาะบริเวณที่เส้นนี้กินพื้นที่ ไม่ต้องกวาดทั้งภาพ
            // เส้นหนึ่งเส้นมักกินไม่ถึงหนึ่งในสิบของภาพ ประหยัดไปมาก
            GetPixelBounds(stroke.Points, size, radius,
                out int minX, out int minY, out int maxX, out int maxY);

            for (int y = minY; y <= maxY; y++)
            {
                int row = y * size;
                for (int x = minX; x <= maxX; x++) coverage[row + x] = 0;
            }

            for (int i = 1; i < stroke.Points.Length; i++)
                DrawSegment(size, stroke.Points[i - 1], stroke.Points[i], radius);

            Composite(size, stroke.Color, minX, minY, maxX, maxY);
        }

        private static void GetPixelBounds(
            Vector2[] points, int size, float radius,
            out int minX, out int minY, out int maxX, out int maxY)
        {
            float lowX = float.MaxValue, lowY = float.MaxValue;
            float highX = float.MinValue, highY = float.MinValue;

            foreach (Vector2 p in points)
            {
                if (p.x < lowX) lowX = p.x;
                if (p.x > highX) highX = p.x;
                if (p.y < lowY) lowY = p.y;
                if (p.y > highY) highY = p.y;
            }

            int margin = Mathf.CeilToInt(radius) + 1;

            minX = Mathf.Clamp(Mathf.FloorToInt(lowX * size) - margin, 0, size - 1);
            minY = Mathf.Clamp(Mathf.FloorToInt(lowY * size) - margin, 0, size - 1);
            maxX = Mathf.Clamp(Mathf.CeilToInt(highX * size) + margin, 0, size - 1);
            maxY = Mathf.Clamp(Mathf.CeilToInt(highY * size) + margin, 0, size - 1);
        }

        /// <summary>ผสมเส้นที่อยู่ในที่พักลงภาพรวม แบบวางทับตามความเข้ม</summary>
        private static void Composite(int size, Color color, int minX, int minY, int maxX, int maxY)
        {
            byte r = (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
            byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
            byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);

            for (int y = minY; y <= maxY; y++)
            {
                int row = y * size;
                for (int x = minX; x <= maxX; x++)
                {
                    int index = row + x;

                    int src = coverage[index];
                    if (src == 0) continue;

                    Color32 dst = canvas[index];

                    if (src == 255 || dst.a == 0)
                    {
                        // ทึบเต็มหรือข้างล่างว่าง ทับไปเลยไม่ต้องคำนวณ
                        canvas[index] = new Color32(r, g, b, (byte)Mathf.Max(src, (int)dst.a));
                        continue;
                    }

                    // สูตรวางทับมาตรฐาน ปลายทาง = ของใหม่ + ของเดิมที่เหลือรอด
                    int keep = dst.a * (255 - src) / 255;
                    int outA = src + keep;
                    if (outA <= 0) continue;

                    canvas[index] = new Color32(
                        (byte)((r * src + dst.r * keep) / outA),
                        (byte)((g * src + dst.g * keep) / outA),
                        (byte)((b * src + dst.b * keep) / outA),
                        (byte)outA);
                }
            }
        }

        /// <summary>
        /// แต้มวงกลมถี่ ๆ ตลอดแนวจากจุดหนึ่งไปอีกจุด
        /// จำนวนก้าวคิดจากความยาวช่วง เส้นสั้นจึงไม่เสียเวลาแต้มเกินจำเป็น
        /// </summary>
        private static void DrawSegment(int size, Vector2 from, Vector2 to, float radius)
        {
            Vector2 a = from * size;
            Vector2 b = to * size;

            float distance = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance));

            for (int i = 0; i <= steps; i++)
                Stamp(size, Vector2.Lerp(a, b, (float)i / steps), radius);
        }

        private static void Stamp(int size, Vector2 center, float radius)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(center.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(center.y + radius));

            for (int y = minY; y <= maxY; y++)
            {
                int row = y * size;
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - center.x;
                    float dy = y + 0.5f - center.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > radius) continue;

                    // ไล่ขอบหนึ่งพิกเซล ให้เส้นไม่เป็นบันได
                    byte alpha = (byte)(Mathf.Clamp01(radius - d) * 255f);

                    int index = row + x;
                    // เอาค่าที่เข้มกว่า ไม่งั้นจุดที่วงกลมซ้อนกันจะทึบขึ้นเรื่อย ๆ
                    if (alpha > coverage[index]) coverage[index] = alpha;
                }
            }
        }
    }
}
