using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// ปุ่มสลับภาษาที่บอกภาษาปัจจุบันไว้บนตัวปุ่มเอง
    ///
    /// ต่างจากในหน้าตั้งค่าที่แยกเป็นหัวข้อ ปุ่ม และป้ายบอกค่า สามชิ้น
    /// ตรงเมนูหลักมีที่จำกัดกว่า จึงยุบเหลือปุ่มเดียวที่อ่านแล้วรู้ทั้งสองอย่าง
    /// ว่ากดแล้วได้อะไร และตอนนี้ใช้ภาษาอะไรอยู่
    ///
    /// ป้ายบนปุ่มเขียนชื่อภาษาด้วยภาษานั้นเอง ผู้เล่นที่เผลอสลับไปภาษา
    /// ที่ตัวเองอ่านไม่ออกจึงยังหาทางกดกลับได้
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LanguageToggleButton : MonoBehaviour
    {
        [Tooltip("ป้ายบนปุ่ม เว้นว่างได้ เดี๋ยวหาให้เองจากลูกของปุ่ม")]
        [SerializeField] private Text label;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();

            if (label == null) label = GetComponentInChildren<Text>();
        }

        private void OnEnable()
        {
            if (button != null) button.onClick.AddListener(Toggle);

            GameLanguage.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(Toggle);

            GameLanguage.Changed -= Refresh;
        }

        private void Toggle()
        {
            GameLanguage.Toggle();
        }

        private void Refresh()
        {
            if (label == null) return;

            label.text = Loc.Get("settings.language") + "  " + GameLanguage.CurrentName;
        }
    }
}
