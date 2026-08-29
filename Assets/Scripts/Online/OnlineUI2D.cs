using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// เมนูสร้างห้อง/เข้าห้องแบบเล่นออนไลน์ข้ามอินเทอร์เน็ตได้จริง
///
/// หน้าตาเป็น Canvas (uGUI) จริง ไม่ใช่ OnGUI แล้ว
/// สคริปต์ติดตั้งอัตโนมัติสร้าง Canvas และผูก reference ให้ครบ
///
/// ใช้ Sessions API ของ Unity ซึ่งจัดการ Relay ให้เอง (Relay คือเซิร์ฟเวอร์ฝากส่ง
/// ข้อมูลของ Unity ทำให้ไม่ต้องเปิดพอร์ตเราเตอร์ เพื่อนที่อยู่คนละบ้านก็เข้าได้)
///
/// จุดที่คนพลาดบ่อย: WithRelayNetwork() ทำให้ Sessions API เป็นคนสั่ง NetworkManager
/// เริ่มทำงานเอง เราจึงห้ามเรียก StartHost() / StartClient() ซ้ำ ไม่งั้นจะชนกัน
///
/// ก่อนใช้ครั้งแรกต้องผูกโปรเจกต์กับ Unity Cloud ก่อน:
/// Edit > Project Settings > Services > เชื่อมโปรเจกต์ แล้วเปิดบริการ Relay กับ Lobby
/// </summary>
public class OnlineUI2D : MonoBehaviour
{
    [Tooltip("จำนวนผู้เล่นสูงสุด ล็อกตอนสร้างห้อง เปลี่ยนทีหลังไม่ได้")]
    [SerializeField] private int maxPlayers = 4;

    [Tooltip("ชื่อซีนเกม ต้องอยู่ใน Build Settings ด้วย ไม่งั้น Netcode โหลดไม่ได้")]
    [SerializeField] private string gameSceneName = "Game";

    [Tooltip("ชื่อซีนเมนู ใช้ตอนกดออกจากห้องเพื่อกลับมาหน้าแรก")]
    [SerializeField] private string menuSceneName = "Menu";

    /// <summary>ข้อความระหว่างรอโหลดซีนเกม เก็บเป็นค่าคงที่เพราะต้องเทียบเพื่อล้างทีหลัง</summary>
    private const string LoadingGameStatus = "กำลังเข้าเกม...";

    private ISession session;
    private string joinCodeInput = string.Empty;
    private string status = "กำลังเชื่อมต่อ Unity Services...";
    private bool ready;
    private bool busy;

    private async void Start()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            // ล็อกอินแบบไม่ระบุตัวตน — พอสำหรับเทส ไม่ต้องให้ผู้เล่นสมัครอะไร
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            ready = true;
            status = "พร้อมแล้ว";
        }
        catch (Exception e)
        {
            status = "เชื่อม Unity Services ไม่ได้: " + e.Message;
            Debug.LogException(e);
        }
    }

    private async Task HostAsync()
    {
        busy = true;
        status = "กำลังสร้างห้อง...";
        try
        {
            SessionOptions options = new SessionOptions { MaxPlayers = maxPlayers }
                .WithRelayNetwork();

            session = await MultiplayerService.Instance.CreateSessionAsync(options);

            // Sessions API เป็นคนสตาร์ท NetworkManager ให้ ถ้ามันไม่ขึ้นแปลว่าล้มเหลว
            // ต้องดักไว้ ไม่งั้นจะได้ห้องที่ไม่มีใครเข้าได้
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                throw new Exception("สร้าง Session ได้ แต่ Netcode ไม่เริ่มทำงาน");

            status = "สร้างห้องสำเร็จ";
            Debug.Log($"[OnlineUI2D] สร้างห้องสำเร็จ รหัส {session.Code} รับได้ {maxPlayers} คน");
        }
        catch (Exception e)
        {
            session = null;
            status = "สร้างห้องไม่สำเร็จ: " + Explain(e);
            Debug.LogException(e);
        }
        finally
        {
            busy = false;
        }
    }

    /// <summary>
    /// แปลงข้อผิดพลาดให้เป็นข้อความที่บอกทางแก้ได้
    ///
    /// Unity คืนคำว่า "An unknown error occurred" มาเวลาบริการ Lobby หรือ Relay
    /// ยังไม่ถูกเปิดใช้งานในบัญชี ซึ่งเป็นสาเหตุที่พบบ่อยที่สุดแต่ข้อความไม่ได้บอก
    /// ผู้เล่นจะนั่งงงว่าโค้ดพังตรงไหน ทั้งที่ต้องไปกดเปิดบริการบนเว็บ
    /// </summary>
    private static string Explain(Exception e)
    {
        string message = e.Message ?? "";

        if (message.Contains("unknown error"))
        {
            return "ยังไม่ได้เปิดบริการ Lobby กับ Relay ในบัญชี Unity Cloud "
                 + "— เปิดที่ cloud.unity.com เลือกโปรเจกต์นี้ แล้วเปิดสองบริการนั้น";
        }

        return message;
    }

    private async Task JoinAsync()
    {
        string code = joinCodeInput.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            status = "ใส่รหัสห้องก่อน";
            // บันทึกไว้ด้วย ไม่งั้นเวลาผู้เล่นบอกว่ากดแล้วไม่มีอะไรเกิดขึ้น
            // จะแยกไม่ออกว่าไม่ได้ใส่รหัส หรือปุ่มเสีย
            Debug.LogWarning("[OnlineUI2D] กดเข้าห้องแต่ยังไม่ได้ใส่รหัส");
            return;
        }

        Debug.Log($"[OnlineUI2D] กำลังเข้าห้องด้วยรหัส {code}");

        busy = true;
        status = "กำลังเข้าห้อง...";
        try
        {
            // ถ้าเคยต่อค้างไว้ ต้องปิดให้สะอาดก่อน ไม่งั้นจะไปทับ Relay อันเก่า
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                throw new Exception("เข้า Session ได้ แต่ Netcode ไม่เริ่มทำงาน");

            status = "เข้าห้องสำเร็จ";
            Debug.Log($"[OnlineUI2D] เข้าห้อง {session.Code} สำเร็จ");
        }
        catch (Exception e)
        {
            session = null;
            status = "เข้าห้องไม่สำเร็จ: " + Explain(e);
            Debug.LogException(e);
        }
        finally
        {
            busy = false;
        }
    }

    /// <summary>
    /// Host สั่งโหลดซีนเกม ทุกคนในห้องจะถูกพาไปด้วยอัตโนมัติ
    ///
    /// ต้องใช้ SceneManager ของ Netcode ไม่ใช่ของ Unity ตรง ๆ
    /// ถ้าเรียก UnityEngine.SceneManagement.SceneManager.LoadScene เอง
    /// จะโหลดแค่เครื่องตัวเอง คนอื่นค้างอยู่ห้องรอ แล้วมองไม่เห็นกันเลย
    /// </summary>
    private void StartGame()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;

        if (string.IsNullOrEmpty(gameSceneName))
        {
            status = "ยังไม่ได้ตั้งชื่อซีนเกม";
            return;
        }

        SceneEventProgressStatus result =
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);

        status = result == SceneEventProgressStatus.Started
            ? LoadingGameStatus
            : $"เข้าเกมไม่สำเร็จ: {result} (ใส่ซีนใน Build Settings หรือยัง)";
    }

    private bool IsInGameScene()
    {
        return SceneManager.GetActiveScene().name == gameSceneName;
    }

    private bool IsInMenuScene()
    {
        return SceneManager.GetActiveScene().name == menuSceneName;
    }

    private async Task LeaveAsync()
    {
        busy = true;
        status = "กำลังออกจากห้อง...";

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        ISession leaving = session;
        session = null;

        if (leaving != null)
        {
            try
            {
                // Host ต้องลบห้องทิ้ง ไม่งั้นห้องร้างจะค้างอยู่บน Unity Cloud
                if (leaving.IsHost)
                    await leaving.AsHost().DeleteAsync();
                else
                    await leaving.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("ออกจาก Session ไม่สมบูรณ์: " + e.Message);
            }
        }

        status = "ออกจากห้องแล้ว";
        busy = false;

        // กลับหน้าเมนู ไม่ใช่ค้างอยู่ในสนามรบที่ไม่มีใครแล้ว
        //
        // โหลดเองตรง ๆ ไม่ผ่าน Netcode เพราะตอนนี้ตัดการเชื่อมต่อไปแล้ว
        // ตัว NetworkManager ติด DontDestroyOnLoad อยู่ ของในซีนเมนูที่โหลดใหม่
        // จะเจอว่ามีตัวเดิมอยู่แล้วแล้วลบตัวเองทิ้ง เหลือตัวเดียวตามเดิม
        if (!string.IsNullOrEmpty(menuSceneName) && !IsInMenuScene())
            SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }

    // ---------- หน้าตา (Canvas) ----------
    //
    // เปลี่ยนจาก OnGUI มาเป็น uGUI จริง เพราะ OnGUI จัดสรรหน่วยความจำใหม่ทุกเฟรม
    // ไม่สเกลตามความละเอียดจอ ทำปุ่มสวย ๆ หรือใส่ภาพไม่ได้ และรองรับการสัมผัสแย่
    //
    // reference ทั้งหมดผูกโดยสคริปต์ติดตั้งอัตโนมัติ ไม่ต้องลากเอง
    // ปล่อยว่างไว้ก็ไม่ error แค่ส่วนนั้นจะไม่ทำงาน

    [Header("กลุ่มหน้าจอ")]
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private GameObject roomPanel;

    [Tooltip("หน้าตั้งค่าไมโครโฟน")]
    [SerializeField] private GameObject micPanel;

    [Tooltip("ปุ่มเปิดหน้าตั้งค่าไมค์ อยู่ในหน้าเข้าห้อง")]
    [SerializeField] private Button openMicButton;

    [Tooltip("ปุ่มปิดหน้าตั้งค่าไมค์")]
    [SerializeField] private Button closeMicButton;

    // เปิดหน้าตั้งค่าไมค์ค้างไว้อยู่หรือเปล่า
    private bool micPanelOpen;
    [SerializeField] private GameObject compactPanel;

    [Header("หน้าตั้งชื่อและวาดตัวละคร")]
    [SerializeField] private InputField nameInput;
    [SerializeField] private MagicDrawing.ProfileDrawPad drawPad;
    [SerializeField] private Button confirmProfileButton;
    [SerializeField] private Button undoStrokeButton;
    [SerializeField] private Button clearDrawingButton;

    [Header("หน้าเข้าห้อง")]
    [SerializeField] private Button editProfileButton;
    [SerializeField] private InputField codeInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    [Header("หน้าในห้อง")]
    [SerializeField] private Text roleText;
    [SerializeField] private Text roomCodeText;
    [SerializeField] private Text playersText;
    [SerializeField] private Text waitText;
    [SerializeField] private Button copyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button leaveButton;

    [Header("แถบย่อตอนอยู่ในเกม")]
    [SerializeField] private Text compactText;
    [SerializeField] private Button compactLeaveButton;

    [Header("อื่น ๆ")]
    [SerializeField] private Text statusText;

    /// <summary>
    /// ตั้งชื่อและวาดตัวละครเสร็จหรือยัง
    /// ต้องเสร็จก่อนถึงจะเห็นหน้าสร้าง/เข้าห้อง ตามที่ออกแบบไว้ว่า
    /// ข้อมูลตัวละครต้องพร้อมก่อนต่อเน็ต ไม่ใช่ส่งกลางเกม
    /// </summary>
    private bool profileConfirmed;

    private void Awake()
    {
        ApplyThaiFont();

        if (nameInput != null) nameInput.text = MagicDrawing.PlayerProfile.Name;

        if (confirmProfileButton != null) confirmProfileButton.onClick.AddListener(OnConfirmProfile);
        if (editProfileButton != null) editProfileButton.onClick.AddListener(OnEditProfile);
        if (undoStrokeButton != null) undoStrokeButton.onClick.AddListener(OnUndoStroke);
        if (clearDrawingButton != null) clearDrawingButton.onClick.AddListener(OnClearDrawing);

        if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
        if (copyButton != null) copyButton.onClick.AddListener(OnCopyClicked);
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (openMicButton != null) openMicButton.onClick.AddListener(OnOpenMicClicked);
        if (closeMicButton != null) closeMicButton.onClick.AddListener(OnCloseMicClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
        if (compactLeaveButton != null) compactLeaveButton.onClick.AddListener(OnLeaveClicked);

        if (codeInput != null) codeInput.onValueChanged.AddListener(OnCodeChanged);
    }

    private void OnDestroy()
    {
        // ไม่ถอด listener = event ค้างชี้มาที่ object ที่ถูกทำลายแล้ว
        if (hostButton != null) hostButton.onClick.RemoveListener(OnHostClicked);
        if (joinButton != null) joinButton.onClick.RemoveListener(OnJoinClicked);
        if (copyButton != null) copyButton.onClick.RemoveListener(OnCopyClicked);
        if (startButton != null) startButton.onClick.RemoveListener(StartGame);
        if (openMicButton != null) openMicButton.onClick.RemoveListener(OnOpenMicClicked);
        if (closeMicButton != null) closeMicButton.onClick.RemoveListener(OnCloseMicClicked);
        if (leaveButton != null) leaveButton.onClick.RemoveListener(OnLeaveClicked);
        if (compactLeaveButton != null) compactLeaveButton.onClick.RemoveListener(OnLeaveClicked);

        if (codeInput != null) codeInput.onValueChanged.RemoveListener(OnCodeChanged);

        if (confirmProfileButton != null) confirmProfileButton.onClick.RemoveListener(OnConfirmProfile);
        if (editProfileButton != null) editProfileButton.onClick.RemoveListener(OnEditProfile);
        if (undoStrokeButton != null) undoStrokeButton.onClick.RemoveListener(OnUndoStroke);
        if (clearDrawingButton != null) clearDrawingButton.onClick.RemoveListener(OnClearDrawing);
    }

    /// <summary>
    /// ตรวจให้ครบทั้งชื่อและขนาดตัวละครก่อนปล่อยผ่าน
    /// บอกทีละอย่างว่าขาดอะไร ไม่ใช่บอกรวม ๆ ว่า "ยังไม่ครบ"
    /// </summary>
    private void OnConfirmProfile()
    {
        string name = MagicDrawing.PlayerProfile.Sanitize(nameInput != null ? nameInput.text : "");

        if (string.IsNullOrEmpty(name))
        {
            status = "ใส่ชื่อก่อน";
            return;
        }

        if (drawPad != null && !drawPad.Save())
        {
            status = "วาดตัวละครให้ใหญ่กว่านี้ก่อน";
            return;
        }

        MagicDrawing.PlayerProfile.Name = name;
        profileConfirmed = true;
        status = $"พร้อมแล้ว — {name}";
    }

    private void OnEditProfile()
    {
        profileConfirmed = false;
        status = "";
    }

    private void OnUndoStroke()
    {
        if (drawPad != null) drawPad.UndoLastStroke();
    }

    private void OnClearDrawing()
    {
        if (drawPad != null) drawPad.ClearAll();
    }

    private void OnHostClicked() => _ = HostAsync();
    private void OnJoinClicked() => _ = JoinAsync();
    private void OnOpenMicClicked() => micPanelOpen = true;

    private void OnCloseMicClicked() => micPanelOpen = false;

    private void OnLeaveClicked() => _ = LeaveAsync();

    private void OnCopyClicked()
    {
        if (session == null) return;
        GUIUtility.systemCopyBuffer = session.Code;
        status = "คัดลอกรหัสแล้ว";
    }

    /// <summary>รหัสห้องเป็นตัวใหญ่เสมอ แปลงให้เลยผู้เล่นจะได้ไม่ต้องกด Shift</summary>
    private void OnCodeChanged(string value)
    {
        string upper = value.ToUpperInvariant();
        joinCodeInput = upper;

        if (codeInput != null && codeInput.text != upper) codeInput.text = upper;
    }

    /// <summary>
    /// หาฟอนต์ที่มีอักขระไทยจากระบบมาใส่ให้ทุกข้อความ
    ///
    /// จำเป็นเพราะฟอนต์เริ่มต้นของ uGUI ไม่มีอักขระไทย ตัวหนังสือจะกลายเป็น
    /// สี่เหลี่ยมเปล่าทั้งจอ
    ///
    /// ตัวเลือกฟอนต์อยู่ที่ GameFont ที่เดียว ตรงนี้แค่เอาไปแปะให้ทุกข้อความ
    /// เผื่อบางตัวถูกสร้างตอนรันโดยยังไม่ได้ตั้งฟอนต์
    /// </summary>
    private void ApplyThaiFont()
    {
        Font font = MagicDrawing.GameFont.Load();
        if (font == null) return;

        foreach (Text text in GetComponentsInChildren<Text>(true))
            if (text != null) text.font = font;
    }

    private void Update()
    {
        bool connected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool inGame = connected && IsInGameScene();

        // อยู่ในสนามรบแล้วย่อเหลือแถบเล็ก ไม่บังพื้นที่เล่น
        bool inMenu = !connected;

        SetActive(compactPanel, inGame);
        SetActive(profilePanel, inMenu && !profileConfirmed);
        // หน้าตั้งค่าไมค์บังหน้าเข้าห้องไว้ชั่วคราว ไม่ให้ซ้อนกันจนกดผิด
        // และปิดเองเมื่อออกจากเมนู เช่นตอนเข้าห้องสำเร็จ
        if (!inMenu) micPanelOpen = false;

        SetActive(micPanel, inMenu && profileConfirmed && micPanelOpen);
        SetActive(joinPanel, inMenu && profileConfirmed && !micPanelOpen);
        SetActive(roomPanel, connected && !inGame);

        // ปุ่มยืนยันกดได้ก็ต่อเมื่อวาดผ่านเงื่อนไขขนาดแล้ว
        // ปิดปุ่มไว้ชัดเจนกว่าปล่อยให้กดแล้วขึ้นข้อความเตือนทุกครั้ง
        if (confirmProfileButton != null)
            confirmProfileButton.interactable = drawPad == null || drawPad.IsValid;

        // เข้าเกมสำเร็จแล้วต้องล้างข้อความรอ ไม่งั้นค้างว่า "กำลังเข้าเกม..." ตลอด
        if (inGame && status == LoadingGameStatus) status = "";

        if (statusText != null) statusText.text = status;

        if (inGame) UpdateCompactPanel();
        else if (connected) UpdateRoomPanel();
        else UpdateJoinPanel();
    }

    private void UpdateJoinPanel()
    {
        bool usable = ready && !busy;
        if (hostButton != null) hostButton.interactable = usable;
        if (joinButton != null) joinButton.interactable = usable;
        if (codeInput != null) codeInput.interactable = usable;
    }

    private void UpdateRoomPanel()
    {
        bool isHost = NetworkManager.Singleton.IsHost;
        bool usable = ready && !busy;

        if (roleText != null)
            roleText.text = isHost ? "คุณเป็นเจ้าของห้อง" : "คุณเข้าร่วมห้องแล้ว";

        if (roomCodeText != null)
            roomCodeText.text = session != null ? session.Code : "-";

        if (playersText != null)
            playersText.text =
                $"ผู้เล่นในห้อง {NetworkManager.Singleton.ConnectedClientsIds.Count} / {maxPlayers} คน";

        // คนที่ไม่ใช่เจ้าของห้องเห็นข้อความรอแทนปุ่ม จะได้ไม่งงว่าทำไมกดไม่ได้
        SetActive(startButton != null ? startButton.gameObject : null, isHost);
        SetActive(waitText != null ? waitText.gameObject : null, !isHost);

        if (startButton != null) startButton.interactable = usable;
        if (leaveButton != null) leaveButton.interactable = usable;
        if (copyButton != null) copyButton.interactable = session != null;
    }

    private void UpdateCompactPanel()
    {
        if (compactText == null) return;

        string code = session != null ? session.Code : "-";
        compactText.text = $"ห้อง {code}   ผู้เล่น {NetworkManager.Singleton.ConnectedClientsIds.Count}";
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }

    private void OnApplicationQuit()
    {
        // ปิดเกมทั้งที่ยังอยู่ในห้อง = ห้องค้างบน Cloud จนกว่าจะหมดอายุ
        if (session != null)
            _ = LeaveAsync();
    }
}
