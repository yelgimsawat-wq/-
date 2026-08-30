using System;
using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// วิธีเล่นแบบมีภาพประกอบ เปิดจากเมนู Esc
    ///
    /// แยกเป็นหน้า ๆ แทนการยัดทุกอย่างลงจอเดียว เพราะเนื้อหามีห้าเรื่อง
    /// ถ้าใส่รวมกันจะกลายเป็นกำแพงตัวหนังสือที่ไม่มีใครอ่าน
    ///
    /// ข้อความอยู่ในตารางแปล ภาพเป็นสัญลักษณ์ล้วนไม่มีตัวหนังสือ
    /// จึงใช้ได้ทั้งสองภาษาโดยไม่ต้องทำภาพสองชุด
    /// </summary>
    public class TutorialPanel : MonoBehaviour
    {
        [Serializable]
        public class Page
        {
            [Tooltip("ภาพประกอบ ปล่อยว่างได้ถ้าหน้านั้นไม่มีภาพ")]
            public Sprite Picture;

            [Tooltip("กุญแจหัวข้อในตารางแปล")]
            public string TitleKey;

            [Tooltip("กุญแจเนื้อหาในตารางแปล")]
            public string BodyKey;
        }

        [Header("ของที่ต้องผูก")]
        [SerializeField] private Image picture;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Text pageLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button prevButton;

        [Header("เนื้อหา")]
        [SerializeField] private Page[] pages;

        private int index;

        private void OnEnable()
        {
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (prevButton != null) prevButton.onClick.AddListener(Prev);

            GameLanguage.Changed += Refresh;

            // เปิดทีไรเริ่มหน้าแรกเสมอ ไม่ค้างหน้าที่ดูค้างไว้เมื่อรอบก่อน
            index = 0;
            Refresh();
        }

        private void OnDisable()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
            if (prevButton != null) prevButton.onClick.RemoveListener(Prev);

            GameLanguage.Changed -= Refresh;
        }

        private void Next()
        {
            if (pages == null || pages.Length == 0) return;

            index = Mathf.Min(index + 1, pages.Length - 1);
            Refresh();
        }

        private void Prev()
        {
            index = Mathf.Max(index - 1, 0);
            Refresh();
        }

        private void Refresh()
        {
            if (pages == null || pages.Length == 0) return;

            index = Mathf.Clamp(index, 0, pages.Length - 1);
            Page page = pages[index];

            if (picture != null)
            {
                picture.sprite = page.Picture;
                // ไม่มีภาพก็ซ่อนช่องไปเลย ไม่ต้องเหลือกรอบว่าง ๆ ค้างไว้
                picture.enabled = page.Picture != null;
            }

            if (titleLabel != null) titleLabel.text = Loc.Get(page.TitleKey);
            if (bodyLabel != null) bodyLabel.text = Loc.Get(page.BodyKey);
            if (pageLabel != null) pageLabel.text = $"{index + 1} / {pages.Length}";

            // ปิดปุ่มที่กดไปก็ไม่มีอะไรเกิดขึ้น ดีกว่าปล่อยให้กดแล้วเงียบ
            if (prevButton != null) prevButton.interactable = index > 0;
            if (nextButton != null) nextButton.interactable = index < pages.Length - 1;
        }
    }
}
