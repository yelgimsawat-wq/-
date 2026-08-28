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
        private static string NameKey => "MagicDrawing.PlayerName" + InstanceSuffix;
        private static string AppearanceKey => "MagicDrawing.Appearance" + InstanceSuffix;

        /// <summary>
        /// ตัวแยกที่เก็บของผู้เล่นเสมือนแต่ละคนตอนทดสอบใน Editor
        ///
        /// PlayerPrefs เป็นที่เก็บร่วมกันทั้งเครื่อง (บน Windows คือรีจิสทรีชุดเดียว
        /// ต่อชื่อบริษัทและชื่อเกม) ตอนทดสอบด้วย Multiplayer Play Mode ทุกหน้าต่าง
        /// จึงอ่านเขียนที่เดียวกัน ผลคือวาดตัวละครคนละตัวแต่เข้าเกมแล้วหน้าตาเหมือนกันหมด
        /// เพราะคนที่วาดทีหลังไปทับของคนแรก
        ///
        /// ผู้เล่นเสมือนรันจากโฟลเดอร์คนละที่ (Library/VP/mppm...) จึงใช้ที่อยู่นั้น
        /// มาแยกกุญแจได้ ไม่ต้องพึ่งแพ็กเกจของ Multiplayer Play Mode
        ///
        /// ใช้เฉพาะใน Editor เท่านั้น เกมที่ build แล้วใช้กุญแจปกติ ไม่งั้นถ้าผู้เล่น
        /// ย้ายโฟลเดอร์เกม ตัวละครที่เคยวาดไว้จะหายไปเฉย ๆ
        /// </summary>
        private static string InstanceSuffix
        {
#if UNITY_EDITOR
            get { return "." + Application.dataPath.GetHashCode().ToString("X8"); }
#else
            get { return string.Empty; }
#endif
        }

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

        public static void SaveAppearance(IReadOnlyList<AppearanceStroke> strokes)
        {
            PlayerPrefs.SetString(AppearanceKey, Encode(strokes));
            PlayerPrefs.Save();
        }

        public static List<AppearanceStroke> LoadAppearance()
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

        // ---------- การเข้ารหัส ----------
        //
        // เก็บพิกัดเป็นไบต์ (0-255) แล้วแปลงเป็น Base64 กินราว 2.7 ตัวอักษรต่อจุด
        // ถ้าเก็บเป็นข้อความทศนิยมจะกิน 12 ตัวอักษรต่อจุด วาดได้ไม่ถึงครึ่ง
        //
        // ความละเอียด 1/255 ของกรอบ คิดเป็นครึ่งพิกเซลบนภาพ 256 พิกเซล ตาแยกไม่ออก
        //
        // รูปแบบรุ่น 2:
        //   [0xFE][2][จำนวนเส้น] แล้วต่อด้วยแต่ละเส้น
        //   [r][g][b][ความหนา][จำนวนจุด][x][y][x][y]...
        //
        // รูปแบบรุ่น 1 (ของเดิม ไม่มีสีและความหนา):
        //   [จำนวนเส้น] แล้วต่อด้วยแต่ละเส้น [จำนวนจุด][x][y]...
        //
        // แยกสองรุ่นออกจากกันด้วยไบต์แรก รุ่นเก่าเก็บจำนวนเส้นซึ่งไม่เกิน 24
        // จึงใช้ 0xFE เป็นเครื่องหมายรุ่นใหม่ได้โดยไม่ชนกัน
        // ตัวละครที่วาดไว้ก่อนหน้านี้จึงยังเปิดได้ ไม่หายไปเฉย ๆ

        private const byte VersionMarker = 0xFE;
        private const byte CurrentVersion = 2;

        public static string Encode(IReadOnlyList<AppearanceStroke> strokes)
        {
            if (strokes == null || strokes.Count == 0) return "";

            var usable = new List<AppearanceStroke>(Mathf.Min(strokes.Count, MaxStrokes));
            for (int i = 0; i < strokes.Count && usable.Count < MaxStrokes; i++)
                if (strokes[i].IsValid) usable.Add(strokes[i]);

            if (usable.Count == 0) return "";

            var bytes = new List<byte>
            {
                VersionMarker,
                CurrentVersion,
                (byte)usable.Count,
            };

            foreach (AppearanceStroke stroke in usable)
            {
                Color32 color = stroke.Color;
                bytes.Add(color.r);
                bytes.Add(color.g);
                bytes.Add(color.b);
                bytes.Add(stroke.ThicknessToByte());

                int pointCount = Mathf.Min(stroke.Points.Length, MaxPointsPerStroke);
                bytes.Add((byte)pointCount);

                for (int p = 0; p < pointCount; p++)
                {
                    bytes.Add(ToByte(stroke.Points[p].x));
                    bytes.Add(ToByte(stroke.Points[p].y));
                }
            }

            return Convert.ToBase64String(bytes.ToArray());
        }

        public static List<AppearanceStroke> Decode(string encoded)
        {
            var result = new List<AppearanceStroke>();
            if (string.IsNullOrEmpty(encoded)) return result;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(encoded);
            }
            catch (FormatException)
            {
                // ข้อมูลเสีย ทิ้งไปดีกว่าทำเกมพัง
                return result;
            }

            if (bytes.Length < 1) return result;

            return bytes[0] == VersionMarker
                ? DecodeVersion2(bytes)
                : DecodeLegacy(bytes);
        }

        private static List<AppearanceStroke> DecodeVersion2(byte[] bytes)
        {
            var result = new List<AppearanceStroke>();
            if (bytes.Length < 3) return result;

            // ไบต์ที่ 1 คือหมายเลขรุ่น เผื่อมีรุ่น 3 ในอนาคตจะได้แยกออก
            // ตอนนี้อ่านได้แค่รุ่น 2 รุ่นที่ไม่รู้จักถือว่าอ่านไม่ได้
            if (bytes[1] != CurrentVersion) return result;

            int index = 2;
            int strokeCount = bytes[index++];

            for (int s = 0; s < strokeCount; s++)
            {
                // ต้องมีอย่างน้อย สี 3 + ความหนา 1 + จำนวนจุด 1
                if (index + 5 > bytes.Length) break;

                var color = new Color32(bytes[index], bytes[index + 1], bytes[index + 2], 255);
                float thickness = AppearanceStroke.ThicknessFromByte(bytes[index + 3]);
                int pointCount = bytes[index + 4];
                index += 5;

                if (pointCount < 2 || index + pointCount * 2 > bytes.Length) break;

                Vector2[] points = ReadPoints(bytes, ref index, pointCount);
                result.Add(new AppearanceStroke(points, color, thickness));
            }

            return result;
        }

        /// <summary>อ่านของที่บันทึกไว้ก่อนจะมีสีและความหนา ใช้ค่าเริ่มต้นแทน</summary>
        private static List<AppearanceStroke> DecodeLegacy(byte[] bytes)
        {
            var result = new List<AppearanceStroke>();

            int index = 0;
            int strokeCount = bytes[index++];

            for (int s = 0; s < strokeCount; s++)
            {
                if (index >= bytes.Length) break;

                int pointCount = bytes[index++];
                if (pointCount < 2) { index += pointCount * 2; continue; }
                if (index + pointCount * 2 > bytes.Length) break;

                Vector2[] points = ReadPoints(bytes, ref index, pointCount);
                result.Add(new AppearanceStroke(
                    points, Color.white, AppearanceRenderer.DefaultThicknessRatio));
            }

            return result;
        }

        private static Vector2[] ReadPoints(byte[] bytes, ref int index, int count)
        {
            var points = new Vector2[count];
            for (int p = 0; p < count; p++)
            {
                points[p] = new Vector2(FromByte(bytes[index]), FromByte(bytes[index + 1]));
                index += 2;
            }
            return points;
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
        public static float MeasureSize(IReadOnlyList<AppearanceStroke> strokes)
        {
            if (strokes == null || strokes.Count == 0) return 0f;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool any = false;

            foreach (AppearanceStroke stroke in strokes)
            {
                if (stroke.Points == null) continue;

                foreach (Vector2 p in stroke.Points)
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

        public static bool IsBigEnough(IReadOnlyList<AppearanceStroke> strokes)
        {
            return MeasureSize(strokes) >= MinimumSizeRatio;
        }
    }
}
