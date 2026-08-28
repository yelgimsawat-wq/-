using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// ชื่อและรูปตัวละครที่ผู้เล่นตั้งไว้ เก็บในเครื่องตัวเอง
    ///
    /// เก็บด้วย PlayerPrefs เพราะเป็นข้อมูลของเครื่องนี้เท่านั้น ปิดเกมแล้วเปิดใหม่
    /// ยังได้ตัวละครเดิม ไม่ต้องวาดใหม่ทุกครั้ง
    ///
    /// รูปตัวละครเก็บเป็น "ชุดเส้น" ไม่ใช่ไฟล์ภาพ เพราะ
    /// 1. เบากว่ามาก ส่งข้ามเน็ตได้ในคำสั่งเดียว
    /// 2. เข้ากับธีมเกมที่เป็นเกมวาดอยู่แล้ว
    /// 3. ขยายเป็นภาพขนาดไหนก็คมเสมอ ไม่แตกเหมือนภาพที่บันทึกไว้ตายตัว
    ///
    /// พิกัดทุกจุดเป็น 0..1 เทียบกับกรอบสี่เหลี่ยมจัตุรัส ไม่ใช่พิกเซล
    /// จึงไม่ผูกกับขนาดจอที่วาดตอนนั้น
    /// </summary>
    public static class PlayerProfile
    {
        private const string NameKey = "MagicDrawing.PlayerName";
        private const string AppearanceKey = "MagicDrawing.Appearance";

        /// <summary>เพดานที่ต้องไม่เกิน ไม่งั้นส่งข้ามเน็ตแล้วทะลุขนาดข้อความ</summary>
        public const int MaxStrokes = 10;
        public const int MaxPointsPerStroke = 20;
        public const int MaxNameLength = 12;

        public static string Name
        {
            get
            {
                string stored = PlayerPrefs.GetString(NameKey, "");
                return string.IsNullOrWhiteSpace(stored) ? "" : stored;
            }
            set
            {
                string clean = Sanitize(value);
                PlayerPrefs.SetString(NameKey, clean);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// ตัดอักขระที่ทำให้ชื่อเพี้ยนตอนแสดงผลหรือตอนส่งข้ามเน็ต
        /// และตัดความยาวไม่ให้ล้นป้ายเหนือหัว
        /// </summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            var builder = new StringBuilder();
            foreach (char c in raw.Trim())
            {
                // ตัดอักขระควบคุมทิ้ง พวกนี้มองไม่เห็นแต่ทำให้ข้อความเพี้ยนได้
                if (char.IsControl(c)) continue;

                builder.Append(c);
                if (builder.Length >= MaxNameLength) break;
            }

            return builder.ToString();
        }

        // ---------- รูปตัวละคร ----------

        public static bool HasAppearance => LoadAppearance().Count > 0;

        public static void SaveAppearance(IReadOnlyList<Vector2[]> strokes)
        {
            PlayerPrefs.SetString(AppearanceKey, Encode(strokes));
            PlayerPrefs.Save();
        }

        public static List<Vector2[]> LoadAppearance()
        {
            return Decode(PlayerPrefs.GetString(AppearanceKey, ""));
        }

        /// <summary>รูปแบบข้อความที่พร้อมส่งข้ามเน็ต ไม่ต้อง decode แล้ว encode ใหม่</summary>
        public static string EncodedAppearance => PlayerPrefs.GetString(AppearanceKey, "");

        public static void ClearAppearance()
        {
            PlayerPrefs.DeleteKey(AppearanceKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// แปลงชุดเส้นเป็นข้อความสั้น ๆ
        ///
        /// รูปแบบ: ขีดคั่นด้วย ; จุดคั่นด้วย | และ x,y คั่นด้วย ,
        /// ปัดเหลือทศนิยม 3 ตำแหน่งพอ เพราะพิกัดเป็น 0..1 อยู่แล้ว
        /// ละเอียดกว่านั้นตาก็มองไม่ออกแต่ข้อความยาวขึ้นเท่าตัว
        ///
        /// ไม่ใช้ JSON เพราะยาวกว่าสามเท่าโดยไม่ได้อะไรเพิ่ม
        /// </summary>
        public static string Encode(IReadOnlyList<Vector2[]> strokes)
        {
            if (strokes == null || strokes.Count == 0) return "";

            var builder = new StringBuilder();
            int strokeCount = Mathf.Min(strokes.Count, MaxStrokes);

            for (int s = 0; s < strokeCount; s++)
            {
                Vector2[] stroke = strokes[s];
                if (stroke == null || stroke.Length < 2) continue;

                if (builder.Length > 0) builder.Append(';');

                int pointCount = Mathf.Min(stroke.Length, MaxPointsPerStroke);
                for (int p = 0; p < pointCount; p++)
                {
                    if (p > 0) builder.Append('|');
                    builder.Append(stroke[p].x.ToString("F3", CultureInfo.InvariantCulture));
                    builder.Append(',');
                    builder.Append(stroke[p].y.ToString("F3", CultureInfo.InvariantCulture));
                }
            }

            return builder.ToString();
        }

        public static List<Vector2[]> Decode(string encoded)
        {
            var result = new List<Vector2[]>();
            if (string.IsNullOrEmpty(encoded)) return result;

            foreach (string strokeText in encoded.Split(';'))
            {
                string[] pointTexts = strokeText.Split('|');
                if (pointTexts.Length < 2) continue;

                var points = new List<Vector2>(pointTexts.Length);
                foreach (string pointText in pointTexts)
                {
                    string[] parts = pointText.Split(',');
                    if (parts.Length != 2) continue;

                    if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                        && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    {
                        points.Add(new Vector2(x, y));
                    }
                }

                if (points.Count >= 2) result.Add(points.ToArray());
            }

            return result;
        }

        // ---------- กฎเรื่องขนาด ----------

        /// <summary>
        /// รูปต้องใหญ่พอ ไม่งั้นวาดจุดเล็ก ๆ จุดเดียวแล้วกลายเป็นตัวละครที่แทบมองไม่เห็น
        /// ซึ่งได้เปรียบคนอื่นเพราะเล็งยาก
        ///
        /// วัดจากด้านที่ยาวกว่าของกรอบที่ครอบรูป เทียบกับกรอบวาดทั้งหมด
        /// </summary>
        public const float MinimumSizeRatio = 0.5f;

        /// <summary>ขนาดของรูปเทียบกับกรอบวาด 0 = ไม่มีอะไรเลย, 1 = เต็มกรอบ</summary>
        public static float MeasureSize(IReadOnlyList<Vector2[]> strokes)
        {
            if (strokes == null || strokes.Count == 0) return 0f;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool any = false;

            foreach (Vector2[] stroke in strokes)
            {
                if (stroke == null) continue;

                foreach (Vector2 p in stroke)
                {
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.y > maxY) maxY = p.y;
                    any = true;
                }
            }

            if (!any) return 0f;

            return Mathf.Max(maxX - minX, maxY - minY);
        }

        public static bool IsBigEnough(IReadOnlyList<Vector2[]> strokes)
        {
            return MeasureSize(strokes) >= MinimumSizeRatio;
        }
    }
}
