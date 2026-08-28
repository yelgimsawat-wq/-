using System.Collections.Generic;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// คลังแม่แบบรูปทรง และกฎแปลงรูปทรงเป็นธาตุ
    ///
    /// สร้างแม่แบบด้วยการคำนวณเอาตอนเริ่มเกม ไม่ต้องเก็บพิกัดดิบเป็นพันบรรทัด
    /// แก้รูปทรงหรือเพิ่มรูปใหม่ทำได้ที่เดียวตรงนี้
    ///
    /// เรื่องที่คนมักพลาดกับ $1: มันไม่ได้มองข้ามทิศทางการวาดและจุดเริ่มต้น
    /// วงกลมที่ลากตามเข็มกับทวนเข็มให้ลำดับจุดคนละแบบ และสี่เหลี่ยมที่เริ่มลากจาก
    /// มุมบนซ้ายกับเริ่มจากกลางขอบล่างก็ไม่เหมือนกัน เราจึงสร้างแม่แบบของแต่ละ
    /// รูปทรงไว้หลายเวอร์ชัน (2 ทิศทาง x 4 จุดเริ่ม) ให้ผู้เล่นวาดยังไงก็ติด
    /// </summary>
    public static class SpellShapeLibrary
    {
        public const string ShapeCircle = "Circle";
        public const string ShapeTriangle = "Triangle";
        public const string ShapeRectangle = "Rectangle";

        /// <summary>ความละเอียดของเส้นแม่แบบก่อนส่งให้ $1 ไป resample อีกที</summary>
        private const int SamplesPerTemplate = 128;

        /// <summary>จุดเริ่มต้นกี่ตำแหน่งรอบเส้นรอบรูป</summary>
        private const int StartOffsets = 4;

        private static UnistrokeTemplate[] cached;

        /// <summary>แม่แบบทั้งหมด สร้างครั้งเดียวแล้วใช้ซ้ำ</summary>
        public static UnistrokeTemplate[] Templates
        {
            get
            {
                if (cached == null) cached = BuildTemplates();
                return cached;
            }
        }

        /// <summary>
        /// แปลงผลการตรวจรูปทรงเป็นธาตุ
        /// ตามข้อกำหนด: ต่ำกว่าเกณฑ์ความแม่นยำ หรือไม่เข้าพวก ให้เป็นลมทั้งหมด
        /// </summary>
        public static SpellElement ToElement(RecognitionResult result, float minimumScore)
        {
            if (!result.HasMatch || result.Score < minimumScore)
                return SpellElement.Wind;

            switch (result.Name)
            {
                case ShapeCircle:    return SpellElement.Water;
                case ShapeTriangle:  return SpellElement.Fire;
                case ShapeRectangle: return SpellElement.Earth;
                default:             return SpellElement.Wind;
            }
        }

        private static UnistrokeTemplate[] BuildTemplates()
        {
            var templates = new List<UnistrokeTemplate>();

            AddVariants(templates, ShapeCircle, BuildCircle());
            AddVariants(templates, ShapeTriangle, BuildPolygon(3));
            AddVariants(templates, ShapeRectangle, BuildRectangle());

            return templates.ToArray();
        }

        /// <summary>
        /// จากเส้นปิดหนึ่งเส้น แตกออกเป็นหลายแม่แบบ ครอบทั้งทิศตามเข็ม/ทวนเข็ม
        /// และจุดเริ่มต้นหลายตำแหน่ง
        /// </summary>
        private static void AddVariants(List<UnistrokeTemplate> output, string name, Vector2[] closedPath)
        {
            int length = closedPath.Length;

            for (int offsetIndex = 0; offsetIndex < StartOffsets; offsetIndex++)
            {
                int start = (length * offsetIndex) / StartOffsets;

                var forward = new Vector2[length];
                var backward = new Vector2[length];

                for (int i = 0; i < length; i++)
                {
                    forward[i] = closedPath[(start + i) % length];
                    // ทวนเข็ม: เดินถอยหลังจากจุดเริ่มเดียวกัน
                    backward[i] = closedPath[((start - i) % length + length) % length];
                }

                output.Add(new UnistrokeTemplate(name, forward));
                output.Add(new UnistrokeTemplate(name, backward));
            }
        }

        private static Vector2[] BuildCircle()
        {
            var points = new Vector2[SamplesPerTemplate];
            for (int i = 0; i < SamplesPerTemplate; i++)
            {
                float t = (float)i / SamplesPerTemplate * Mathf.PI * 2f;
                points[i] = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
            }
            return points;
        }

        /// <summary>รูปหลายเหลี่ยมด้านเท่า วางมุมแรกไว้ด้านบน</summary>
        private static Vector2[] BuildPolygon(int sides)
        {
            var corners = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float t = Mathf.PI * 0.5f + (float)i / sides * Mathf.PI * 2f;
                corners[i] = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
            }
            return SampleClosedPolygon(corners);
        }

        private static Vector2[] BuildRectangle()
        {
            var corners = new[]
            {
                new Vector2(-1f,  1f),
                new Vector2( 1f,  1f),
                new Vector2( 1f, -1f),
                new Vector2(-1f, -1f),
            };
            return SampleClosedPolygon(corners);
        }

        /// <summary>
        /// เดินไปตามขอบรูปหลายเหลี่ยมแล้วหย่อนจุดให้ห่างเท่า ๆ กัน
        /// แจกจำนวนจุดตามความยาวด้าน ด้านยาวจึงได้จุดมากกว่าด้านสั้น
        /// ไม่งั้นรูปที่ด้านไม่เท่ากันจะมีจุดกระจุกอยู่ด้านสั้น
        /// </summary>
        private static Vector2[] SampleClosedPolygon(Vector2[] corners)
        {
            int sides = corners.Length;

            float perimeter = 0f;
            var sideLengths = new float[sides];
            for (int i = 0; i < sides; i++)
            {
                sideLengths[i] = Vector2.Distance(corners[i], corners[(i + 1) % sides]);
                perimeter += sideLengths[i];
            }

            var points = new List<Vector2>(SamplesPerTemplate);
            for (int i = 0; i < sides; i++)
            {
                Vector2 from = corners[i];
                Vector2 to = corners[(i + 1) % sides];

                int count = Mathf.Max(2, Mathf.RoundToInt(SamplesPerTemplate * (sideLengths[i] / perimeter)));

                // ไม่ใส่จุดปลาย เพราะจะไปซ้ำกับจุดเริ่มของด้านถัดไป
                for (int s = 0; s < count; s++)
                    points.Add(Vector2.Lerp(from, to, (float)s / count));
            }

            return points.ToArray();
        }
    }
}
