using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// เลือกภาษาด้วยธงสองผืน กดผืนไหนได้ภาษานั้น
    ///
    /// ทำไมถึงไม่ใช้ปุ่มสลับที่มีข้อความบอก: คนที่ต้องใช้ตัวเลือกนี้มากที่สุด
    /// คือคนที่อ่านภาษาที่เกมกำลังแสดงอยู่ไม่ออก ถ้าให้เขาอ่านคำว่า "ภาษา"
    /// ก่อนถึงจะเปลี่ยนได้ ก็วนกลับไปที่ปัญหาเดิม ธงจึงต้องสื่อได้ด้วยตัวเอง
    ///
    /// และต้องเห็นธงทั้งสองผืนพร้อมกัน ไม่ใช่ผืนเดียวที่สลับไปมา
    /// เพราะธงผืนเดียวตอบไม่ได้ว่ามันแปลว่า "ตอนนี้ใช้ภาษานี้"
    /// หรือ "กดแล้วจะเปลี่ยนเป็นภาษานี้" ซึ่งความหมายตรงข้ามกัน
    /// </summary>
    public class LanguagePicker : MonoBehaviour
    {
        [Header("ปุ่มธง")]
        [SerializeField] private Button thaiButton;
        [SerializeField] private Button englishButton;

        [Header("ภาพธง ใช้หรี่แสงผืนที่ไม่ได้เลือก")]
        [SerializeField] private Image thaiFlag;
        [SerializeField] private Image englishFlag;

        [Header("กรอบเน้นผืนที่เลือกอยู่")]
        [SerializeField] private Graphic thaiHighlight;
        [SerializeField] private Graphic englishHighlight;

        // ผืนที่ไม่ได้เลือกหรี่ลงพอให้เห็นความต่าง แต่ยังเห็นว่าเป็นธงอะไร
        // ถ้าหรี่จนมืดจะกลายเป็นปุ่มที่ดูกดไม่ได้ ซึ่งไม่จริง
        private static readonly Color Selected = Color.white;
        private static readonly Color Unselected = new Color(1f, 1f, 1f, 0.45f);

        private void OnEnable()
        {
            if (thaiButton != null) thaiButton.onClick.AddListener(PickThai);
            if (englishButton != null) englishButton.onClick.AddListener(PickEnglish);

            GameLanguage.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (thaiButton != null) thaiButton.onClick.RemoveListener(PickThai);
            if (englishButton != null) englishButton.onClick.RemoveListener(PickEnglish);

            GameLanguage.Changed -= Refresh;
        }

        private void PickThai()
        {
            GameLanguage.Current = Language.Thai;
        }

        private void PickEnglish()
        {
            GameLanguage.Current = Language.English;
        }

        private void Refresh()
        {
            bool thai = GameLanguage.Current == Language.Thai;

            if (thaiFlag != null) thaiFlag.color = thai ? Selected : Unselected;
            if (englishFlag != null) englishFlag.color = thai ? Unselected : Selected;

            if (thaiHighlight != null) thaiHighlight.enabled = thai;
            if (englishHighlight != null) englishHighlight.enabled = !thai;
        }
    }
}
