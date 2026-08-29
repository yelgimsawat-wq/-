using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// มาตรวัดเสียงพูด เป็นหลอดแนวตั้งที่สูงขึ้นตามความดัง
    ///
    /// ย้ายจาก OnGUI ที่โชว์เป็นเปอร์เซ็นต์ มาเป็นหลอดจริงบน Canvas
    /// เพราะตัวเลขเปอร์เซ็นต์อ่านยากระหว่างเล่น ต้องละสายตาจากเกมมาอ่าน
    /// แต่หลอดสีดูปราดเดียวรู้ว่าดังพอหรือยัง
    ///
    /// สีไล่จากล่างขึ้นบน เขียว -> เหลือง -> แดง ตอนดังสุด
    /// ผู้เล่นจึงเล็งความดังได้โดยไม่ต้องจำว่ากี่เปอร์เซ็นต์ถึงจะแรงสุด
    /// </summary>
    public class VoiceMeter : MonoBehaviour
    {
        public static VoiceMeter Instance { get; private set; }

        [Tooltip("หลอดที่จะสูงขึ้นตามเสียง ต้องตั้ง Image Type เป็น Filled แนวตั้ง")]
        [SerializeField] private Image fill;

        [Tooltip("ข้อความบอกสถานะ เช่นตอนไม่มีไมค์ ปล่อยว่างได้")]
        [SerializeField] private Text statusLabel;

        [Tooltip("เส้นบอกระดับที่ต้องตะโกนให้ถึงเพื่อยิงเวท ปล่อยว่างได้")]
        [SerializeField] private RectTransform thresholdMarker;

        [Header("สีตามความดัง")]
        [SerializeField] private Color quietColor = new Color(0.35f, 0.85f, 0.40f);
        [SerializeField] private Color mediumColor = new Color(1f, 0.85f, 0.25f);
        [SerializeField] private Color loudColor = new Color(1f, 0.30f, 0.25f);

        [Tooltip("ดังเกินค่านี้ถือว่าเข้าโซนแดง")]
        [Range(0f, 1f)]
        [SerializeField] private float loudThreshold = 0.75f;

        [Header("ความนุ่มนวล")]
        [Tooltip("ยิ่งมากหลอดยิ่งวิ่งไว ยิ่งน้อยยิ่งนุ่มแต่ตามช้า")]
        [SerializeField] private float followSpeed = 14f;

        private float shownLevel;
        private float fireThreshold;
        private bool thresholdVisible;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// อัปเดตหลอด เรียกทุกเฟรมจากเจ้าของไมค์
        /// hasMic เป็น false เมื่อไม่มีไมค์ จะซ่อนหลอดแล้วขึ้นข้อความบอกแทน
        /// </summary>
        public void SetLevel(float level, bool hasMic)
        {
            if (fill == null) return;

            if (!hasMic)
            {
                fill.fillAmount = 0f;
                if (statusLabel != null) statusLabel.text = "ไม่มีไมค์";
                return;
            }

            if (statusLabel != null && statusLabel.text.Length > 0) statusLabel.text = "";

            // เกลี่ยก่อนแสดง ไม่งั้นหลอดจะกระตุกตามคลื่นเสียงทุกเฟรมจนดูรำคาญ
            float target = Mathf.Clamp01(level);
            shownLevel = Mathf.Lerp(shownLevel, target, 1f - Mathf.Exp(-Time.deltaTime * followSpeed));

            fill.fillAmount = shownLevel;

            // ถึงเกณฑ์ยิงแล้วให้แดงเต็มทันที ไม่ต้องรอไล่สีถึงยอด
            // ผู้เล่นจะได้รู้ชัดว่าพอแล้ว ไม่ต้องเดาว่าอีกนิดหรือเปล่า
            bool reached = thresholdVisible && shownLevel >= fireThreshold;
            fill.color = reached ? loudColor : ColorFor(shownLevel);
        }

        /// <summary>
        /// ตั้งเส้นบอกระดับที่ต้องตะโกนให้ถึง
        ///
        /// จำเป็นมาก เพราะถ้าไม่มีเส้นนี้ผู้เล่นจะตะโกนแบบเดาไปเรื่อย ๆ
        /// ว่าดังพอหรือยัง ไม่มีทางรู้เลยว่าอีกนิดเดียวหรืออีกไกล
        /// </summary>
        public void SetThreshold(float level, bool visible)
        {
            fireThreshold = Mathf.Clamp01(level);
            thresholdVisible = visible;

            if (thresholdMarker == null) return;

            thresholdMarker.gameObject.SetActive(visible);
            if (!visible) return;

            // ยึดขอบล่างของหลอดเป็นศูนย์ แล้วเลื่อนขึ้นตามสัดส่วน
            // ใช้ anchor แทนตำแหน่งเป็นพิกเซล หลอดจะได้ยืดหดตามขนาดจอได้
            thresholdMarker.anchorMin = new Vector2(0f, fireThreshold);
            thresholdMarker.anchorMax = new Vector2(1f, fireThreshold);
            thresholdMarker.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// ไล่สีสองช่วง เขียวไปเหลืองครึ่งแรก แล้วเหลืองไปแดงจนถึงจุดที่ถือว่าดังสุด
        /// แยกสองช่วงเพราะไล่สีเดียวจากเขียวไปแดงจะได้สีน้ำตาลขุ่นตรงกลาง
        /// </summary>
        private Color ColorFor(float level)
        {
            float mid = loudThreshold * 0.5f;

            if (level <= mid)
                return Color.Lerp(quietColor, mediumColor, mid > 0f ? level / mid : 0f);

            float span = Mathf.Max(0.0001f, loudThreshold - mid);
            return Color.Lerp(mediumColor, loudColor, Mathf.Clamp01((level - mid) / span));
        }
    }
}
