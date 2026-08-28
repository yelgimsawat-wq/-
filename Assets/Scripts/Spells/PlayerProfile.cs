using System;
using System.Collections.Generic;
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
        public const int MaxStrokes = 24;
        public const int MaxPointsPerStroke = 48;
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
        /// แปลงชุดเส้นเป็นข้อความสั้นที่สุดเท่าที่ทำได้
        ///
        /// เดิมเก็บเป็นตัวเลขทศนิยมคั่นด้วยจุลภาค กินราว 12 ตัวอักษรต่อจุด
        /// ทำให้วาดได้แค่ 10 เส้น เส้นละ 20 จุด ก่อนจะทะลุช่องส่งข้ามเน็ต
        /// ซึ่งน้อยเกินกว่าจะวาดตัวละครให้ดูดีได้
        ///
        /// เปลี่ยนมาเก็บพิกัดเป็นไบต์ (0-255) แล้วแปลงเป็น Base64
        /// เหลือราว 2.7 ตัวอักษรต่อจุด ประหยัดลงกว่าสี่เท่า
        /// วาดได้ 24 เส้น เส้นละ 48 จุด
        ///
        /// ความละเอียด 1/255 ของกรอบ คิดเป็นครึ่งพิกเซลบนภาพ 128 พิกเซล
        /// ตาแยกไม่ออกอยู่แล้ว
        ///
        /// รูปแบบ: [จำนวนเส้น][จำนวนจุด][x][y][x][y]...[จำนวนจุด][x][y]...
        /// </summary>
        public static string Encode(IReadOnlyList<Vector2[]> strokes)
        {
            if (strokes == null || strokes.Count == 0) return "";

            var bytes = new List<byte>();

            int strokeCount = Mathf.Min(strokes.Count, MaxStrokes);
            var usable = new List<Vector2[]>(strokeCount);

            for (int i = 0; i < strokeCount; i++)
                if (strokes[i] != null && strokes[i].Length >= 2) usable.Add(strokes[i]);

            if (usable.Count == 0) return "";

            bytes.Add((byte)usable.Count);

            foreach (Vector2[] stroke in usable)
            {
                int pointCount = Mathf.Min(stroke.Length, MaxPointsPerStroke);
                bytes.Add((byte)pointCount);

                for (int p = 0; p < pointCount; p++)
                {
                    bytes.Add(ToByte(stroke[p].x));
                    bytes.Add(ToByte(stroke[p].y));
                }
            }

            return Convert.ToBase64String(bytes.ToArray());
        }

        public static List<Vector2[]> Decode(string encoded)
        {
            var result = new List<Vector2[]>();
            if (string.IsNullOrEmpty(encoded)) return result;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(encoded);
            }
            catch (FormatException)
            {
                // ข้อมูลเสียหรือมาจากเวอร์ชันเก่าคนละรูปแบบ ทิ้งไปดีกว่าทำเกมพัง
                return result;
            }

            int index = 0;
            if (bytes.Length < 1) return result;

            int strokeCount = bytes[index++];

            for (int s = 0; s < strokeCount; s++)
            {
                if (index >= bytes.Length) break;

                int pointCount = bytes[index++];
                if (pointCount < 2) { index += pointCount * 2; continue; }

                // ข้อมูลขาดกลางทาง หยุดตรงนี้แทนที่จะอ่านทะลุขอบ
                if (index + pointCount * 2 > bytes.Length) break;

                var points = new Vector2[pointCount];
                for (int p = 0; p < pointCount; p++)
                {
                    points[p] = new Vector2(FromByte(bytes[index]), FromByte(bytes[index + 1]));
                    index += 2;
                }

                result.Add(points);
            }

            return result;
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        private static float FromByte(byte value)
        {
            return value / 255f;
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
