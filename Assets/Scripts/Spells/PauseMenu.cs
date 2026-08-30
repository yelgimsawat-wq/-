using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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

        [Header("เปิดวิธีเล่นอัตโนมัติ")]
        [Tooltip("ชื่อฉากเมนู ฉากอื่นถือว่าเป็นสนามรบ")]
        [SerializeField] private string menuSceneName = "Menu";

        [Tooltip("เข้าสนามรบแล้วกางวิธีเล่นให้เลย ปิดช่องนี้ถ้าไม่ต้องการ")]
        [SerializeField] private bool showTutorialOnEnterBattle = true;

        private OnlineUI2D onlineUi;
        private LocalizedText backLabel;

        private enum Page { Main, Settings, Tutorial }
        private Page page = Page.Main;

        /// <summary>
        /// ตอนนี้กางวิธีเล่นให้อัตโนมัติอยู่หรือเปล่า
        ///
        /// ต่างกันตรงปุ่มล่างสุด ปกติมันคือปุ่มย้อนกลับไปหน้าเมนู
        /// แต่ตอนกางให้เอง ผู้เล่นยังไม่ได้เปิดเมนู การพากลับไปหน้าเมนู
        /// จึงเป็นการพาไปที่ที่เขาไม่ได้ขอไป ปุ่มควรปิดทิ้งแล้วเริ่มเล่นเลย
        /// </summary>
        private bool introMode;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            Instance = this;
            onlineUi = GetComponentInParent<OnlineUI2D>();

            // หน้าตั้งค่าถูกปิดไว้ Awake ของมันจึงไม่ทำงานตอนเริ่มเกม
            // ต้องเอาระดับเสียงที่บันทึกไว้มาใช้จากตรงนี้แทน
            GameSettingsPanel.ApplySavedVolume();

            if (backButton != null) backLabel = backButton.GetComponentInChildren<LocalizedText>(true);

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
            if (backButton != null) backButton.onClick.AddListener(Back);
            if (leaveButton != null) leaveButton.onClick.AddListener(Leave);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveAllListeners();
            if (settingsButton != null) settingsButton.onClick.RemoveAllListeners();
            if (tutorialButton != null) tutorialButton.onClick.RemoveAllListeners();
            if (backButton != null) backButton.onClick.RemoveAllListeners();
            if (leaveButton != null) leaveButton.onClick.RemoveAllListeners();

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>เข้าสนามรบแล้วกางวิธีเล่นให้เลย ไม่ต้องให้ผู้เล่นไปหาเอง</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!showTutorialOnEnterBattle) return;

            // ฉากเมนูไม่ใช่สนามรบ ไม่ต้องกาง
            if (scene.name == menuSceneName) return;

            OpenTutorial();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[openKey].wasPressedThisFrame) return;

            if (IsOpen)
            {
                // อยู่หน้าลูกก็ถอยกลับหน้าหลักก่อน ไม่ปิดทิ้งทันที
                // ผู้เล่นที่เปิดวิธีเล่นอยู่แล้วกด Esc มักตั้งใจจะย้อนกลับ ไม่ใช่ปิดหมด
                if (page != Page.Main) Back();
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

            introMode = false;
            root.SetActive(true);
            Show(Page.Main);
            RefreshRoomLabel();
        }

        /// <summary>กางวิธีเล่นทันที ปุ่มล่างสุดจะกลายเป็นปุ่มเริ่มเล่น</summary>
        public void OpenTutorial()
        {
            if (root == null) return;

            introMode = true;
            root.SetActive(true);
            Show(Page.Tutorial);
            RefreshRoomLabel();
        }

        public void Close()
        {
            introMode = false;
            RefreshBackLabel();

            if (root != null) root.SetActive(false);
        }

        /// <summary>ปุ่มล่างสุด ความหมายต่างกันระหว่างกางเอง กับผู้เล่นเปิดเมนูเอง</summary>
        private void Back()
        {
            if (introMode) Close();
            else Show(Page.Main);
        }

        private void Show(Page target)
        {
            page = target;

            if (mainPage != null) mainPage.SetActive(target == Page.Main);
            if (settingsPage != null) settingsPage.SetActive(target == Page.Settings);
            if (tutorialPage != null) tutorialPage.SetActive(target == Page.Tutorial);

            // ปุ่มย้อนกลับมีประโยชน์เฉพาะตอนอยู่หน้าลูก
            if (backButton != null) backButton.gameObject.SetActive(target != Page.Main);

            RefreshBackLabel();
        }

        private void RefreshBackLabel()
        {
            if (backLabel == null) return;

            backLabel.SetKey(introMode ? "tut.startPlaying" : "pause.back");
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
