using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// เลือกภาษาจากรายการที่กางลงมา หน้าตาเดียวกับช่องเลือกไมโครโฟน
    ///
    /// แต่ละบรรทัดมีธงนำหน้าชื่อภาษา คนที่อ่านตัวหนังสือบนจอไม่ออก
    /// จึงยังเลือกกลับไปภาษาตัวเองได้จากธง ไม่ต้องพึ่งข้อความอย่างเดียว
    ///
    /// ชื่อภาษาเขียนด้วยภาษานั้นเอง ไม่แปลตามภาษาที่กำลังใช้อยู่
    /// "English" ต้องขึ้นว่า English เสมอ ไม่ใช่ "อังกฤษ" ตอนที่เกมเป็นไทย
    /// เพราะคนที่กำลังหาภาษาอังกฤษอยู่ก็คือคนที่อ่านคำว่า "อังกฤษ" ไม่ออก
    /// </summary>
    [RequireComponent(typeof(Dropdown))]
    public class LanguageDropdown : MonoBehaviour
    {
        [SerializeField] private Sprite thaiFlag;
        [SerializeField] private Sprite englishFlag;

        private Dropdown dropdown;

        private void Awake()
        {
            dropdown = GetComponent<Dropdown>();
        }

        private void OnEnable()
        {
            if (dropdown == null) dropdown = GetComponent<Dropdown>();

            FillOptions();

            dropdown.onValueChanged.AddListener(OnPicked);
            GameLanguage.Changed += Sync;
        }

        private void OnDisable()
        {
            if (dropdown != null) dropdown.onValueChanged.RemoveListener(OnPicked);

            GameLanguage.Changed -= Sync;
        }

        private void FillOptions()
        {
            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData("ไทย", thaiFlag));
            dropdown.options.Add(new Dropdown.OptionData("English", englishFlag));

            Sync();
        }

        private void OnPicked(int index)
        {
            GameLanguage.Current = index == 0 ? Language.Thai : Language.English;
        }

        private void Sync()
        {
            int index = GameLanguage.Current == Language.Thai ? 0 : 1;

            // ต้องไม่ยิงเหตุการณ์ซ้ำ ไม่งั้นจะวนกลับมาเรียก OnPicked อีกรอบ
            dropdown.SetValueWithoutNotify(index);
            dropdown.RefreshShownValue();
        }
    }
}
