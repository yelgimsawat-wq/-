using System;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// ฟอนต์ของเกม
    ///
    /// ใช้ Itim จาก Google Fonts สร้างโดย Cadson Demak โรงหล่อตัวอักษรไทย
    /// เป็นลายมือ เข้ากับเกมที่เล่นด้วยการวาด และมีอักขระไทยครบ
    ///
    /// สัญญาอนุญาตเป็น SIL Open Font License จึงแจกจ่ายไปพร้อมเกมได้
    /// ไฟล์สัญญาอยู่ที่ Assets/Resources/Fonts/OFL.txt ห้ามลบทิ้ง
    ///
    /// เดิมยืมฟอนต์จากเครื่องผู้เล่น (Leelawadee UI, Tahoma) ซึ่งใช้ทดสอบได้
    /// แต่ปล่อยเกมจริงไม่ได้ เพราะฟอนต์ของ Windows มีข้อจำกัดเรื่องการแจกจ่ายต่อ
    /// และเครื่องที่ไม่มีฟอนต์พวกนั้นจะเห็นตัวหนังสือเป็นสี่เหลี่ยมเปล่าทั้งจอ
    ///
    /// วางไว้ใน Resources เพื่อให้โค้ดตอนรันโหลดได้โดยไม่ต้องผูก reference
    /// ทุกจุดที่มีข้อความ ซึ่งลืมง่ายและพังเงียบ ๆ
    /// </summary>
    public static class GameFont
    {
        /// <summary>ที่อยู่สำหรับ Resources.Load ไม่มีนามสกุลไฟล์</summary>
        public const string ResourcePath = "Fonts/Itim-Regular";

        /// <summary>ที่อยู่จริงในโปรเจกต์ สำหรับสคริปต์ฝั่ง Editor</summary>
        public const string AssetPath = "Assets/Resources/Fonts/Itim-Regular.ttf";

        private static Font cached;
        private static bool warned;

        /// <summary>
        /// ฟอนต์ที่ควรใช้ คืน null ถ้าหาอะไรไม่ได้เลย
        /// เก็บไว้ใช้ซ้ำ ไม่โหลดใหม่ทุกครั้งที่เรียก
        /// </summary>
        public static Font Load()
        {
            if (cached != null) return cached;

            cached = Resources.Load<Font>(ResourcePath);
            if (cached != null) return cached;

            // ไฟล์หายหรือถูกย้าย ยังพอถูไถด้วยฟอนต์ของเครื่องได้
            // ดีกว่าปล่อยให้ตัวหนังสือกลายเป็นสี่เหลี่ยมเปล่าทั้งจอ
            cached = LoadFromSystem();

            if (cached == null && !warned)
            {
                warned = true;
                Debug.LogWarning(
                    $"[GameFont] หาฟอนต์ไม่เจอเลย ทั้ง {ResourcePath} และฟอนต์ของเครื่อง "
                    + "ตัวหนังสือจะขึ้นเป็นสี่เหลี่ยมเปล่า");
            }

            return cached;
        }

        private static Font LoadFromSystem()
        {
            string[] candidates =
            {
                "Leelawadee UI", "Leelawadee", "Tahoma", "Arial Unicode MS", "Noto Sans Thai", "Sarabun",
            };

            foreach (string name in candidates)
            {
                try
                {
                    Font font = Font.CreateDynamicFontFromOSFont(name, 24);
                    if (font != null) return font;
                }
                catch (Exception)
                {
                    // ไม่มีฟอนต์ชื่อนี้ในเครื่อง ลองตัวถัดไป
                }
            }

            return null;
        }
    }
}
