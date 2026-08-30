using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// ผูกข้อความบนจอเข้ากับกุญแจในตารางแปล
    ///
    /// ใส่ไว้บนป้ายที่ข้อความไม่เปลี่ยนระหว่างเล่น เช่นหัวข้อและปุ่ม
    /// พอผู้เล่นสลับภาษา ทุกป้ายที่มีตัวนี้จะแปลใหม่เองทันที
    /// ไม่ต้องเดินไล่หาป้ายทีละอันจากโค้ด
    ///
    /// ป้ายที่ข้อความเปลี่ยนตลอดเวลา (เช่นคำใบ้ตอนเล่น) ไม่ต้องใช้ตัวนี้
    /// เพราะเจ้าของข้อความเขียนทับทุกเฟรมอยู่แล้ว
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("กุญแจในตารางแปล เช่น pause.resume")]
        [SerializeField] private string key;

        private Text label;

        private void Awake()
        {
            label = GetComponent<Text>();
        }

        private void OnEnable()
        {
            GameLanguage.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameLanguage.Changed -= Refresh;
        }

        /// <summary>เปลี่ยนกุญแจตอนรัน เผื่ออยากใช้ป้ายเดียวกันหลายที่</summary>
        public void SetKey(string newKey)
        {
            key = newKey;
            Refresh();
        }

        private void Refresh()
        {
            // SetKey อาจถูกเรียกก่อน Awake ตอนนั้น label ยังว่างอยู่
            // ถ้าไม่หาเองตรงนี้ ข้อความจะไม่เปลี่ยนโดยไม่มีอะไรฟ้อง
            if (label == null) label = GetComponent<Text>();

            if (label == null || string.IsNullOrEmpty(key)) return;

            string text = Loc.Get(key);
            if (label.text != text) label.text = text;
        }
    }
}
