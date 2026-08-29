using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// ไมโครโฟนที่ผู้เล่นเลือกไว้ เก็บในเครื่องตัวเอง
    ///
    /// จำเป็นเพราะเครื่องที่มีไมค์หลายตัว (เช่น USB กับไมค์ในตัวเครื่อง)
    /// ตัวที่ Windows ตั้งเป็นค่าเริ่มต้นอาจไม่ใช่ตัวที่ผู้เล่นพูดใส่จริง
    /// ต้องให้เลือกเองได้ ไม่ใช่เดาให้
    ///
    /// เก็บเป็น "ชื่ออุปกรณ์" ไม่ใช่ลำดับในรายการ เพราะลำดับเปลี่ยนได้
    /// ทุกครั้งที่เสียบหรือถอดอุปกรณ์ ถ้าเก็บลำดับไว้แล้วเสียบหูฟังเพิ่ม
    /// จะกลายเป็นเลือกไมค์คนละตัวโดยไม่รู้ตัว
    /// </summary>
    public static class MicSettings
    {
        private const string DeviceKey = "MagicDrawing.MicDevice";

        /// <summary>ค่านี้แปลว่าให้ Windows เลือกตัวที่ตั้งเป็นค่าเริ่มต้นให้</summary>
        public const string SystemDefaultLabel = "ค่าเริ่มต้นของระบบ";

        /// <summary>
        /// ไมค์ที่จะใช้ คืน null เมื่อให้ใช้ค่าเริ่มต้นของระบบ
        /// (Unity ตีความ null ว่าใช้ตัวที่ระบบตั้งไว้)
        /// </summary>
        public static string SelectedDevice
        {
            get
            {
                string saved = PlayerPrefs.GetString(DeviceKey, "");
                if (string.IsNullOrEmpty(saved)) return null;

                // อุปกรณ์ที่เคยเลือกอาจถูกถอดไปแล้ว ต้องเช็คก่อนใช้
                // ไม่งั้นจะพยายามเปิดไมค์ที่ไม่มีอยู่แล้วเงียบสนิท
                foreach (string device in Microphone.devices)
                    if (device == saved) return saved;

                return null;
            }
        }

        /// <summary>ชื่อที่เอาไปโชว์ ไม่ใช่ค่าที่เอาไปเปิดไมค์</summary>
        public static string SelectedLabel
        {
            get
            {
                string device = SelectedDevice;
                return string.IsNullOrEmpty(device) ? SystemDefaultLabel : device;
            }
        }

        /// <summary>ตั้งไมค์ที่จะใช้ ส่งค่าว่างหรือ null เพื่อกลับไปใช้ค่าเริ่มต้นของระบบ</summary>
        public static void Select(string device)
        {
            PlayerPrefs.SetString(DeviceKey, string.IsNullOrEmpty(device) ? "" : device);
            PlayerPrefs.Save();
        }
    }
}
