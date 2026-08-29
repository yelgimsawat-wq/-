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
    /// ข้อความเก็บไว้ตรงนี้ ไม่ใช่ใน SpellDrawing เพราะ SpellDrawing อยู่บน
    /// prefab ตัวละครที่ถูก spawn ตอนเข้าเกม ซึ่งอ้างถึงของในฉากไม่ได้
    /// และการแก้ข้อความบน prefab ก็หายากกว่ามาสำหรับคนที่จะมาปรับคำ
    ///
    /// {0} ในข้อความคือช่องเสียบค่า อย่าลบทิ้งถ้ายังอยากเห็นตัวเลขหรือชื่อธาตุ
    /// </summary>
    public class SpellHintLabel : MonoBehaviour
    {
        public static SpellHintLabel Instance { get; private set; }

        [Tooltip("ข้อความที่จะแสดง ปล่อยว่าง = หาในตัวเองให้")]
        [SerializeField] private Text label;

        [Header("ข้อความแต่ละสถานะ แก้ได้ตามใจ")]
        [Tooltip("ตอนยังไม่ได้เขียนอะไร")]
        [TextArea(2, 4)]
        [SerializeField] private string idleHint =
            "A/D เดิน  |  W กระโดด  |  ลากเมาส์เขียนคาถา\n" +
            "วงกลม=น้ำ  สามเหลี่ยม=ไฟ  สี่เหลี่ยม=ดิน  ขีด 4 ขีด=ลม";

        [Tooltip("ตอนกำลังเขียนคาถา  {0} = จำนวนขีด")]
        [TextArea(2, 4)]
        [SerializeField] private string composingHint =
            "เขียนคาถา {0} ขีด  |  Space = ยืนยัน  |  Esc = ล้าง";

        [Tooltip("ตอนเล็งก่อนยิง  {0} = ชื่อธาตุ")]
        [TextArea(2, 4)]
        [SerializeField] private string aimingHint =
            "เวท{0} พร้อมแล้ว  |  เลื่อนเมาส์เล็ง  |  ตะโกนให้ถึงเส้นขาวเพื่อยิง  |  Esc = ยกเลิก";

        private void Awake()
        {
            Instance = this;
            if (label == null) label = GetComponent<Text>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ShowIdle() => Write(idleHint);

        public void ShowComposing(int strokeCount) => Write(Format(composingHint, strokeCount));

        public void ShowAiming(string elementName) => Write(Format(aimingHint, elementName));

        public void Hide() => Write("");

        /// <summary>
        /// เสียบค่าลงในข้อความ กันพังถ้าคนแก้ข้อความแล้วเผลอลบ {0} หรือใส่ {1} เกิน
        /// ถ้ารูปแบบผิดจะคืนข้อความดิบแทนที่จะโยน error กลางเกม
        /// </summary>
        private static string Format(string template, object value)
        {
            if (string.IsNullOrEmpty(template)) return "";

            try
            {
                return string.Format(template, value);
            }
            catch (System.FormatException)
            {
                return template;
            }
        }

        private void Write(string message)
        {
            if (label == null) return;

            // เทียบก่อนเขียน ไม่งั้นสั่ง UI ให้วาดใหม่ทุกเฟรมโดยไม่จำเป็น
            if (label.text != message) label.text = message;
        }
    }
}
