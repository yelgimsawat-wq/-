using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// บทช่วยสอนด้านล่างจอ บอกว่าตอนนี้กดอะไรได้บ้าง
    ///
    /// ย้ายจาก OnGUI มาเป็น Canvas เพื่อให้แก้ข้อความ ขนาด สี และตำแหน่งได้เอง
    /// จาก Inspector โดยไม่ต้องแตะโค้ด
    ///
    /// ข้อความจริงอยู่ในตารางแปล (Loc) ไม่ได้อยู่ตรงนี้ เพราะต้องสลับไทย/อังกฤษได้
    /// แก้คำได้ที่ Loc.cs ที่เดียว ครบทั้งสองภาษา
    /// </summary>
    public class SpellHintLabel : MonoBehaviour
    {
        public static SpellHintLabel Instance { get; private set; }

        [Tooltip("ข้อความที่จะแสดง ปล่อยว่าง = หาในตัวเองให้")]
        [SerializeField] private Text label;

        private void Awake()
        {
            Instance = this;
            if (label == null) label = GetComponent<Text>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ShowIdle() => Write(Loc.Get("hint.idle"));

        public void ShowComposing(int strokeCount) => Write(Loc.Get("hint.composing", strokeCount));

        public void ShowAiming(string elementName) => Write(Loc.Get("hint.aiming", elementName));

        public void Hide() => Write("");

        private void Write(string message)
        {
            if (label == null) return;

            // เทียบก่อนเขียน ไม่งั้นสั่ง UI ให้วาดใหม่ทุกเฟรมโดยไม่จำเป็น
            if (label.text != message) label.text = message;
        }
    }
}
