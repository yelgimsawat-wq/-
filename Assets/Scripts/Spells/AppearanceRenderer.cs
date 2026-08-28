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

        /// <summary>
        /// ความหนาเส้นเทียบกับความกว้างภาพ (เป็นรัศมี ไม่ใช่เส้นผ่าศูนย์กลาง)
        ///
        /// เดิมตั้งไว้ 0.045 ซึ่งทำให้เส้นกว้าง 9% ของภาพ พอแสดงบนกระดานจริง
        /// เส้นจะกว้างเกือบ 40 พิกเซล เคอร์เซอร์อยู่กลางก้อนอ้วน ๆ
        /// ทำให้รู้สึกว่าวาดไม่ตรงกับตำแหน่งเมาส์
        /// </summary>
        public const float DefaultThicknessRatio = 0.012f;

        // ใช้บัฟเฟอร์เดิมซ้ำ ไม่จองใหม่ทุกครั้งที่วาด
        // ภาพ 256x256 คือ 65,536 ช่อง ถ้าจองใหม่ทุกจุดที่ลากเมาส์
        // ตัวเก็บขยะจะทำงานถี่จนเห็นเป็นอาการกระตุก
        private static Color32[] scratch;
        private static int scratchSize;

        /// <summary>
        /// วาดลงเท็กซ์เจอร์ที่มีอยู่แล้ว ไม่สร้างใบใหม่
        /// ใช้ตอนวาดสด ๆ ที่ต้องอัปเดตหลายสิบครั้งต่อวินาที
        /// </summary>
        public static void BakeInto(
            Texture2D target,
            IReadOnlyList<Vector2[]> strokes,
            Color color,
            float thicknessRatio = DefaultThicknessRatio)
        {
            if (target == null) return;

            int size = target.width;
            if (scratch == null || scratchSize != size)
            {
                scratch = new Color32[size * size];
                scratchSize = size;
            }

            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < scratch.Length; i++) scratch[i] = clear;

            PaintStrokes(scratch, size, strokes, color, thicknessRatio);

            target.SetPixels32(scratch);
            target.Apply();
        }

        private static void PaintStrokes(
            Color32[] pixels, int size,
            IReadOnlyList<Vector2[]> strokes, Color color, float thicknessRatio)
        {
            if (strokes == null) return;

            float radius = Mathf.Max(1f, size * thicknessRatio);
            var brush = (Color32)color;

            foreach (Vector2[] stroke in strokes)
            {
                if (stroke == null || stroke.Length < 2) continue;

                for (int i = 1; i < stroke.Length; i++)
                    DrawSegment(pixels, size, stroke[i - 1], stroke[i], radius, brush);
            }
        }

        /// <summary>
        /// อบชุดเส้นเป็น Texture2D
        /// พิกัดที่รับเข้ามาเป็น 0..1 โดย (0,0) อยู่มุมล่างซ้าย
        /// </summary>
        public static Texture2D BakeTexture(
            IReadOnlyList<Vector2[]> strokes,
            Color color,
            int size = TextureSize,
            float thicknessRatio = DefaultThicknessRatio)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            BakeInto(texture, strokes, color, thicknessRatio);
            return texture;
        }

        public static Sprite BakeSprite(
            IReadOnlyList<Vector2[]> strokes,
            Color color,
            float pixelsPerUnit,
            int size = TextureSize)
        {
            Texture2D texture = BakeTexture(strokes, color, size);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
        }

        /// <summary>
        /// แต้มเส้นตรงหนึ่งช่วงลงบนพิกเซล
        ///
        /// เดินไปตามเส้นแล้วแต้มวงกลมทีละจุด แทนการใช้อัลกอริทึมวาดเส้นบาง
        /// เพราะเราต้องการเส้นหนาที่ปลายมนและต่อกันเนียน ซึ่งวิธีนี้ได้ฟรี
        /// จำนวนก้าวคิดจากความยาวช่วง เส้นสั้นจึงไม่เสียเวลาแต้มเกินจำเป็น
        /// </summary>
        private static void DrawSegment(
            Color32[] pixels, int size, Vector2 from, Vector2 to, float radius, Color32 color)
        {
            Vector2 a = from * size;
            Vector2 b = to * size;

            float distance = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance));

            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, (float)i / steps);
                Stamp(pixels, size, p, radius, color);
            }
        }

        private static void Stamp(Color32[] pixels, int size, Vector2 center, float radius, Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(center.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(center.y + radius));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - center.x;
                    float dy = y + 0.5f - center.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > radius) continue;

                    // ไล่ขอบหนึ่งพิกเซล ให้เส้นไม่เป็นบันได
                    float alpha = Mathf.Clamp01(radius - d);

                    int index = y * size + x;
                    // ทับเฉพาะตอนที่เข้มกว่าของเดิม ไม่งั้นจุดที่เส้นตัดกันจะจางลง
                    byte newAlpha = (byte)(alpha * 255f);
                    if (newAlpha <= pixels[index].a) continue;

                    pixels[index] = new Color32(color.r, color.g, color.b, newAlpha);
                }
            }
        }
    }
}
