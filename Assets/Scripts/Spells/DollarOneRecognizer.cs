using UnityEngine;
using System.Collections.Generic;

namespace MagicDrawing
{
    /// <summary>รูปทรงหนึ่งแบบที่เอาไว้เทียบกับเส้นที่ผู้เล่นวาด</summary>
    public class UnistrokeTemplate
    {
        public readonly string Name;
        public readonly Vector2[] Points;

        public UnistrokeTemplate(string name, Vector2[] rawPoints)
        {
            Name = name;
            // เก็บแบบที่ normalize แล้ว จะได้ไม่ต้องคำนวณซ้ำทุกครั้งที่ผู้เล่นวาด
            Points = DollarOneRecognizer.Normalize(rawPoints);
        }
    }

    public struct RecognitionResult
    {
        public string Name;
        public float Score;   // 0..1 ยิ่งใกล้ 1 ยิ่งเหมือน
        public bool HasMatch;
    }

    /// <summary>
    /// $1 Unistroke Recognizer อัลกอริทึมแยกแยะรูปทรงที่วาดรวดเดียวจบ
    /// (Wobbrock, Wilson, Li ปี 2007)
    ///
    /// หลักการ 4 ขั้น:
    /// 1. Resample  เกลี่ยจำนวนจุดให้เท่ากันเสมอ (64 จุด) เพราะคนวาดเร็วช้าไม่เท่ากัน
    ///              จุดที่เก็บได้จึงถี่ห่างไม่เท่ากัน ต้องทำให้เทียบกันได้ก่อน
    /// 2. Rotate    หมุนให้มุมจากจุดศูนย์กลางไปยังจุดแรกเป็นศูนย์ วาดเอียงแค่ไหนก็ยังตรงกัน
    /// 3. Scale     ยืดให้พอดีกรอบสี่เหลี่ยมมาตรฐาน วาดใหญ่หรือเล็กจึงให้ผลเท่ากัน
    /// 4. Translate ย้ายจุดศูนย์กลางไปที่ (0,0) วาดตรงไหนของจอก็ได้
    ///
    /// แล้วค่อยวัดระยะห่างเฉลี่ยระหว่างจุดของเรากับของแม่แบบ โดยลองหมุนหามุมที่ตรงที่สุด
    ///
    /// ข้อจำกัดที่ควรรู้: ขั้น Scale ยืดแกน x กับ y แยกกัน วงรีแบน ๆ จึงถูกมองเป็นวงกลม
    /// และเพราะมีการหมุน สี่เหลี่ยมจัตุรัสกับข้าวหลามตัดจึงเหมือนกัน
    /// เกมนี้แยกแค่ วงกลม/สามเหลี่ยม/สี่เหลี่ยม จึงไม่กระทบ
    /// </summary>
    public static class DollarOneRecognizer
    {
        /// <summary>จำนวนจุดหลัง resample ค่ามาตรฐานของอัลกอริทึมคือ 64</summary>
        public const int ResampleCount = 64;

        private const float SquareSize = 250f;

        // ระยะไกลสุดที่เป็นไปได้ในกรอบมาตรฐาน ใช้แปลงระยะห่างเป็นคะแนน 0..1
        private static readonly float HalfDiagonal =
            0.5f * Mathf.Sqrt(SquareSize * SquareSize + SquareSize * SquareSize);

        private static readonly float AngleRange = 45f * Mathf.Deg2Rad;
        private static readonly float AnglePrecision = 2f * Mathf.Deg2Rad;

        // อัตราส่วนทองคำ ใช้ค้นหามุมที่ดีที่สุดโดยไม่ต้องลองทีละองศา
        private static readonly float Phi = 0.5f * (-1f + Mathf.Sqrt(5f));

        /// <summary>
        /// หาว่าเส้นที่วาดใกล้เคียงแม่แบบไหนที่สุด
        /// ต้องมีอย่างน้อย 2 จุด ไม่งั้นคืน HasMatch เป็น false
        /// </summary>
        public static RecognitionResult Recognize(Vector2[] points, UnistrokeTemplate[] templates)
        {
            var result = new RecognitionResult { Name = null, Score = 0f, HasMatch = false };

            if (points == null || points.Length < 2 || templates == null || templates.Length == 0)
                return result;

            Vector2[] candidate = Normalize(points);
            if (candidate == null) return result;

            float best = float.MaxValue;
            UnistrokeTemplate bestTemplate = null;

            foreach (UnistrokeTemplate template in templates)
            {
                if (template == null || template.Points == null) continue;

                float distance = DistanceAtBestAngle(candidate, template.Points);
                if (distance < best)
                {
                    best = distance;
                    bestTemplate = template;
                }
            }

            if (bestTemplate == null) return result;

            result.Name = bestTemplate.Name;
            result.Score = Mathf.Clamp01(1f - best / HalfDiagonal);
            result.HasMatch = true;
            return result;
        }

        /// <summary>ทำ 4 ขั้นตอนมาตรฐานของ $1 ให้กับชุดจุดหนึ่งชุด</summary>
        public static Vector2[] Normalize(Vector2[] points)
        {
            if (points == null || points.Length < 2) return null;

            Vector2[] working = Resample(points, ResampleCount);
            if (working == null) return null;

            float indicativeAngle = IndicativeAngle(working);
            working = RotateBy(working, -indicativeAngle);
            working = ScaleToSquare(working, SquareSize);
            working = TranslateToOrigin(working);
            return working;
        }

        /// <summary>
        /// เกลี่ยจุดใหม่ให้ได้จำนวนเท่ากับ n และห่างเท่า ๆ กันตลอดเส้น
        /// เดินไปตามเส้นเดิม พอสะสมระยะครบช่วงหนึ่งก็หย่อนจุดใหม่ลงไป
        ///
        /// เปิดให้เรียกจากข้างนอกได้ เพราะฝั่งเครือข่ายใช้บีบจำนวนจุดให้คงที่
        /// ก่อนส่งข้าม RPC จะได้คุมขนาดข้อมูลได้แน่นอน
        /// คืน null ถ้าเส้นสั้นเกินไปจนคำนวณระยะไม่ได้
        /// </summary>
        public static Vector2[] Resample(Vector2[] points, int n)
        {
            float interval = PathLength(points) / (n - 1);
            if (interval <= 0f || float.IsNaN(interval)) return null;

            float accumulated = 0f;
            var output = new Vector2[n];
            int count = 0;
            output[count++] = points[0];

            // ทำสำเนาไว้แก้ เพราะต้องแทรกจุดกลางทางระหว่างเดิน
            var src = new List<Vector2>(points);

            for (int i = 1; i < src.Count; i++)
            {
                float segment = Vector2.Distance(src[i - 1], src[i]);

                if (accumulated + segment >= interval)
                {
                    // จุดใหม่ตกอยู่ตรงไหนของช่วงนี้
                    float t = (interval - accumulated) / segment;
                    Vector2 inserted = Vector2.LerpUnclamped(src[i - 1], src[i], t);

                    if (count < n) output[count++] = inserted;

                    // แทรกกลับเข้าไป เพื่อวัดช่วงถัดไปต่อจากจุดนี้
                    src.Insert(i, inserted);
                    accumulated = 0f;
                }
                else
                {
                    accumulated += segment;
                }
            }

            // ปัดเศษทศนิยมอาจทำให้ขาดจุดสุดท้าย เติมด้วยปลายเส้นเดิม
            while (count < n) output[count++] = src[src.Count - 1];

            return output;
        }

        private static float PathLength(Vector2[] points)
        {
            float total = 0f;
            for (int i = 1; i < points.Length; i++)
                total += Vector2.Distance(points[i - 1], points[i]);
            return total;
        }

        private static Vector2 Centroid(Vector2[] points)
        {
            Vector2 sum = Vector2.zero;
            foreach (Vector2 p in points) sum += p;
            return sum / points.Length;
        }

        /// <summary>มุมจากจุดศูนย์กลางไปยังจุดแรกที่วาด ใช้เป็นหลักในการหมุนให้ตรงกัน</summary>
        private static float IndicativeAngle(Vector2[] points)
        {
            Vector2 c = Centroid(points);
            return Mathf.Atan2(c.y - points[0].y, c.x - points[0].x);
        }

        private static Vector2[] RotateBy(Vector2[] points, float radians)
        {
            Vector2 c = Centroid(points);
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            var output = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                float dx = points[i].x - c.x;
                float dy = points[i].y - c.y;
                output[i] = new Vector2(
                    dx * cos - dy * sin + c.x,
                    dx * sin + dy * cos + c.y
                );
            }
            return output;
        }

        private static Vector2[] ScaleToSquare(Vector2[] points, float size)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (Vector2 p in points)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            float width = maxX - minX;
            float height = maxY - minY;

            var output = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                // เส้นตรงแนวดิ่งจะมี width เป็น 0 ต้องกันหารด้วยศูนย์
                float x = width > Mathf.Epsilon ? points[i].x * (size / width) : points[i].x;
                float y = height > Mathf.Epsilon ? points[i].y * (size / height) : points[i].y;
                output[i] = new Vector2(x, y);
            }
            return output;
        }

        private static Vector2[] TranslateToOrigin(Vector2[] points)
        {
            Vector2 c = Centroid(points);
            var output = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
                output[i] = points[i] - c;
            return output;
        }

        /// <summary>
        /// ลองหมุนหามุมที่ทำให้สองเส้นทับกันดีที่สุด ในช่วง -45 ถึง +45 องศา
        /// ใช้การค้นหาแบบอัตราส่วนทองคำ เพื่อไม่ต้องลองทีละองศาให้เปลืองเวลา
        /// </summary>
        private static float DistanceAtBestAngle(Vector2[] points, Vector2[] template)
        {
            float a = -AngleRange;
            float b = AngleRange;

            float x1 = Phi * a + (1f - Phi) * b;
            float f1 = DistanceAtAngle(points, template, x1);
            float x2 = (1f - Phi) * a + Phi * b;
            float f2 = DistanceAtAngle(points, template, x2);

            while (Mathf.Abs(b - a) > AnglePrecision)
            {
                if (f1 < f2)
                {
                    b = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = Phi * a + (1f - Phi) * b;
                    f1 = DistanceAtAngle(points, template, x1);
                }
                else
                {
                    a = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = (1f - Phi) * a + Phi * b;
                    f2 = DistanceAtAngle(points, template, x2);
                }
            }

            return Mathf.Min(f1, f2);
        }

        private static float DistanceAtAngle(Vector2[] points, Vector2[] template, float radians)
        {
            Vector2[] rotated = RotateBy(points, radians);
            return PathDistance(rotated, template);
        }

        /// <summary>ระยะห่างเฉลี่ยระหว่างจุดคู่ที่ตำแหน่งเดียวกันของสองเส้น</summary>
        private static float PathDistance(Vector2[] a, Vector2[] b)
        {
            int count = Mathf.Min(a.Length, b.Length);
            if (count == 0) return float.MaxValue;

            float sum = 0f;
            for (int i = 0; i < count; i++)
                sum += Vector2.Distance(a[i], b[i]);

            return sum / count;
        }
    }
}
