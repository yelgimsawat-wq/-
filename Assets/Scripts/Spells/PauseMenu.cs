using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MagicDrawing
{
    /// <summary>
    /// เมนูกลางจอที่เปิดด้วย Esc ระหว่างเล่น
    ///
    /// รวมทุกอย่างที่เคยกระจายอยู่มุมจอไว้ที่เดียว ทั้งรหัสห้อง ปุ่มออก ตั้งค่า
    /// และวิธีเล่น แถบลอยมุมจอบังพื้นที่เล่นและกดโดนโดยไม่ตั้งใจได้ง่าย
    ///
    /// เรื่องที่ต้องระวัง: Esc เป็นปุ่มยกเลิกคาถาอยู่แล้ว
    /// ถ้ากำลังร่ายอยู่ Esc จะยกเลิกคาถาก่อน ไม่เปิดเมนู
    /// ผู้เล่นจึงไม่เผลอเปิดเมนูกลางการต่อสู้เพราะจะยกเลิกคาถา
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }

        /// <summary>เมนูเปิดอยู่ไหม ให้ระบบอื่นเช็คได้โดยไม่ต้องหา instance เอง</summary>
        public static bool IsOpenNow => Instance != null && Instance.IsOpen;

        [Header("หน้าต่าง")]
        [Tooltip("ตัวครอบทั้งหมด ปิดตัวนี้ = ปิดเมนู")]
        [SerializeField] private GameObject root;

        [SerializeField] private GameObject mainPage;
        [SerializeField] private GameObject settingsPage;
        [SerializeField] private GameObject tutorialPage;

        [Header("ปุ่ม")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button tutorialButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button backButton;

        [Header("ข้อมูลห้อง")]
        [SerializeField] private Text roomLabel;

        [Header("ปุ่มเปิด")]
        [SerializeField] private Key openKey = Key.Escape;

        private OnlineUI2D onlineUi;

        private enum Page { Main, Settings, Tutorial }
        private Page page = Page.Main;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            Instance = this;
            onlineUi = GetComponentInParent<OnlineUI2D>();

            // หน้าตั้งค่าถูกปิดไว้ Awake ของมันจึงไม่ทำงานตอนเริ่มเกม
            // ต้องเอาระดับเสียงที่บันทึกไว้มาใช้จากตรงนี้แทน
            GameSettingsPanel.ApplySavedVolume();

            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(Close);
            if (settingsButton != null) settingsButton.onClick.AddListener(() => Show(Page.Settings));
            if (tutorialButton != null) tutorialButton.onClick.AddListener(() => Show(Page.Tutorial));
            if (backButton != null) backButton.onClick.AddListener(() => Show(Page.Main));
            if (leaveButton != null) leaveButton.onClick.AddListener(Leave);
        }

        private void OnDisable()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveAllListeners();
            if (settingsButton != null) settingsButton.onClick.RemoveAllListeners();
            if (tutorialButton != null) tutorialButton.onClick.RemoveAllListeners();
            if (backButton != null) backButton.onClick.RemoveAllListeners();
            if (leaveButton != null) leaveButton.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[openKey].wasPressedThisFrame) return;

            if (IsOpen)
            {
                // อยู่หน้าลูกก็ถอยกลับหน้าหลักก่อน ไม่ปิดทิ้งทันที
                // ผู้เล่นที่เปิดวิธีเล่นอยู่แล้วกด Esc มักตั้งใจจะย้อนกลับ ไม่ใช่ปิดหมด
                if (page != Page.Main) Show(Page.Main);
                else Close();

                return;
            }

            // กำลังร่ายคาถาอยู่ Esc มีหน้าที่ยกเลิกคาถา ไม่ใช่เปิดเมนู
            // ปล่อยให้ SpellDrawing จัดการไป
            if (SpellDrawing.LocalOwner != null && SpellDrawing.LocalOwner.IsCasting) return;

            Open();
        }

        public void Open()
        {
            if (root == null) return;

            root.SetActive(true);
            Show(Page.Main);
            RefreshRoomLabel();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        private void Show(Page target)
        {
            page = target;

            if (mainPage != null) mainPage.SetActive(target == Page.Main);
            if (settingsPage != null) settingsPage.SetActive(target == Page.Settings);
            if (tutorialPage != null) tutorialPage.SetActive(target == Page.Tutorial);

            // ปุ่มย้อนกลับมีประโยชน์เฉพาะตอนอยู่หน้าลูก
            if (backButton != null) backButton.gameObject.SetActive(target != Page.Main);
        }

        private void RefreshRoomLabel()
        {
            if (roomLabel == null) return;

            string code = onlineUi != null ? onlineUi.RoomCode : "-";
            roomLabel.text = GameLanguage.Current == Language.Thai
                ? $"รหัสห้อง  {code}"
                : $"Room code  {code}";
        }

        private void Leave()
        {
            Close();
            if (onlineUi != null) onlineUi.RequestLeave();
        }
    }
}
