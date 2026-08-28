using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// ปุ่มเลือกสีและแถบเลื่อนขนาดปากกาของกระดานวาดตัวละคร
    ///
    /// ทำเป็น component แยกแทนการผูก event ตรง ๆ จาก Inspector เพราะ
    /// UnityEvent ส่งค่าแบบ Color ไม่ได้ (รองรับแค่ int, float, string, bool
    /// และ Object เท่านั้น) ถ้าจะผูกปุ่มสีตรง ๆ ต้องทำเมธอดแยกทีละสี
    /// ซึ่งเพิ่มสีใหม่ทีต้องแก้โค้ดทุกครั้ง
    ///
    /// เก็บปุ่มกับสีเป็นสองรายการคู่กัน ลำดับตรงกัน ทำให้สคริปต์ติดตั้ง
    /// สร้างปุ่มกี่สีก็ได้โดยไม่ต้องแก้ไฟล์นี้
    /// </summary>
    public class PenControls : MonoBehaviour
    {
        [SerializeField] private ProfileDrawPad pad;

        [Header("ขนาด")]
        [SerializeField] private Slider sizeSlider;

        [Tooltip("จุดตัวอย่างที่โตตามขนาดปากกา ปล่อยว่างได้")]
        [SerializeField] private RectTransform sizeDot;

        [Tooltip("ขนาดจุดตัวอย่างตอนปากกาบางสุดและหนาสุด หน่วยพิกเซล")]
        [SerializeField] private float sizeDotMin = 4f;
        [SerializeField] private float sizeDotMax = 34f;

        [Header("สี")]
        [Tooltip("ปุ่มสี ลำดับต้องตรงกับ swatchColors")]
        [SerializeField] private Button[] swatchButtons;

        [SerializeField] private Color[] swatchColors;

        [Tooltip("กรอบที่จะโผล่รอบสีที่เลือกอยู่ ลำดับตรงกับปุ่ม ปล่อยว่างได้")]
        [SerializeField] private Image[] swatchHighlights;

        private void Start()
        {
            if (pad == null) return;

            if (sizeSlider != null)
            {
                sizeSlider.minValue = AppearanceStroke.MinThickness;
                sizeSlider.maxValue = AppearanceStroke.MaxThickness;

                // ตั้งค่าเริ่มต้นให้ตรงกับปากกาจริง ไม่ใช่ค่าที่ค้างอยู่ใน prefab
                sizeSlider.SetValueWithoutNotify(pad.PenThickness);
                sizeSlider.onValueChanged.AddListener(HandleSizeChanged);
            }

            HookSwatches();
            RefreshSizeDot(pad.PenThickness);
            RefreshHighlights();
        }

        private void OnDestroy()
        {
            if (sizeSlider != null) sizeSlider.onValueChanged.RemoveListener(HandleSizeChanged);

            if (swatchButtons == null) return;
            foreach (Button button in swatchButtons)
                if (button != null) button.onClick.RemoveAllListeners();
        }

        private void HookSwatches()
        {
            if (swatchButtons == null || swatchColors == null) return;

            int count = Mathf.Min(swatchButtons.Length, swatchColors.Length);
            for (int i = 0; i < count; i++)
            {
                Button button = swatchButtons[i];
                if (button == null) continue;

                // เก็บ index ไว้ในตัวแปรของรอบนี้ ไม่งั้นทุกปุ่มจะอ้างค่าสุดท้าย
                int index = i;
                button.onClick.AddListener(() => SelectColor(index));
            }
        }

        private void SelectColor(int index)
        {
            if (pad == null || swatchColors == null) return;
            if (index < 0 || index >= swatchColors.Length) return;

            pad.SetPenColor(swatchColors[index]);
            RefreshHighlights();
        }

        private void HandleSizeChanged(float value)
        {
            if (pad == null) return;

            pad.SetPenThickness(value);
            RefreshSizeDot(value);
        }

        /// <summary>ให้จุดตัวอย่างโตตามขนาดปากกา จะได้เห็นก่อนลากว่าจะหนาแค่ไหน</summary>
        private void RefreshSizeDot(float thickness)
        {
            if (sizeDot == null) return;

            float t = Mathf.InverseLerp(
                AppearanceStroke.MinThickness, AppearanceStroke.MaxThickness, thickness);

            float diameter = Mathf.Lerp(sizeDotMin, sizeDotMax, t);
            sizeDot.sizeDelta = new Vector2(diameter, diameter);
        }

        /// <summary>โชว์กรอบรอบสีที่เลือกอยู่ ผู้เล่นจะได้รู้ว่าตอนนี้ถืออะไรอยู่</summary>
        private void RefreshHighlights()
        {
            if (swatchHighlights == null || swatchColors == null || pad == null) return;

            int count = Mathf.Min(swatchHighlights.Length, swatchColors.Length);
            for (int i = 0; i < count; i++)
            {
                if (swatchHighlights[i] == null) continue;

                // เทียบเฉพาะ RGB เพราะสีปากกาไม่ใช้ค่าโปร่งใส
                Color a = swatchColors[i];
                Color b = pad.PenColor;
                bool selected = Mathf.Approximately(a.r, b.r)
                    && Mathf.Approximately(a.g, b.g)
                    && Mathf.Approximately(a.b, b.b);

                swatchHighlights[i].enabled = selected;
            }
        }
    }
}
