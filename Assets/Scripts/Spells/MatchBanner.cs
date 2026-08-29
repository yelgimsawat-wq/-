using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// ป้ายประกาศผลกลางจอ วางไว้บนข้อความในแคนวาสของเมนู
    ///
    /// ทำไมต้องแยกเป็น component ของตัวเอง:
    /// MatchManager ถูกสร้างจาก prefab ตอนเข้าสนามรบ ซึ่ง prefab อ้างถึงของที่อยู่
    /// ในฉากไม่ได้ (ช่องจะว่างเสมอ) จึงต้องให้ฝั่งป้ายประกาศตัวเองไว้ แล้ว
    /// MatchManager ค่อยมาถามหาตอนทำงานจริง
    ///
    /// ใช้ตัวเดียวทั้งเกม เพราะแคนวาสเมนูตามข้ามซีนไปด้วย (DontDestroyOnLoad)
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class MatchBanner : MonoBehaviour
    {
        public static MatchBanner Instance { get; private set; }

        private Text label;

        private void Awake()
        {
            Instance = this;
            label = GetComponent<Text>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>เขียนข้อความลงป้าย ส่งค่าว่างเข้ามาเพื่อซ่อน</summary>
        public void Show(string message)
        {
            if (label == null) return;

            // เทียบก่อนเขียน ไม่งั้นสั่งให้ UI วาดใหม่ทุกเฟรมโดยไม่จำเป็น
            if (label.text != message) label.text = message;
        }
    }
}
