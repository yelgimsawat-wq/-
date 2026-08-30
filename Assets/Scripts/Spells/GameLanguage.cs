using System;
using UnityEngine;

namespace MagicDrawing
{
    public enum Language : byte
    {
        Thai = 0,
        English = 1,
    }

    /// <summary>
    /// ภาษาที่ผู้เล่นเลือก เก็บในเครื่องตัวเอง
    ///
    /// เป็นค่าของแต่ละเครื่อง ไม่ใช่ของห้อง เพื่อนเลือกอังกฤษเราเลือกไทยได้
    /// เพราะข้อความทุกอย่างแปลตอนแสดงผล ไม่ได้ส่งข้อความสำเร็จรูปข้ามเน็ต
    /// </summary>
    public static class GameLanguage
    {
        private const string Key = "MagicDrawing.Language";

        /// <summary>แจ้งทุกป้ายให้แปลข้อความใหม่เมื่อผู้เล่นสลับภาษา</summary>
        public static event Action Changed;

        private static bool loaded;
        private static Language current;

        public static Language Current
        {
            get
            {
                if (!loaded)
                {
                    current = (Language)PlayerPrefs.GetInt(Key, (int)Language.Thai);
                    loaded = true;
                }
                return current;
            }
            set
            {
                if (loaded && current == value) return;

                current = value;
                loaded = true;

                PlayerPrefs.SetInt(Key, (int)value);
                PlayerPrefs.Save();

                Changed?.Invoke();
            }
        }

        /// <summary>สลับไปอีกภาษา ใช้กับปุ่มสลับภาษาปุ่มเดียว</summary>
        public static void Toggle()
        {
            Current = Current == Language.Thai ? Language.English : Language.Thai;
        }

        /// <summary>ชื่อภาษาที่กำลังใช้ เขียนด้วยภาษานั้นเอง จะได้อ่านออกเสมอ</summary>
        public static string CurrentName => Current == Language.Thai ? "ไทย" : "English";
    }
}
