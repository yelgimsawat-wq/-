using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// เมนูสร้างห้อง/เข้าห้องแบบเล่นออนไลน์ข้ามอินเทอร์เน็ตได้จริง
///
/// วิธีใช้: แปะสคริปต์นี้กับ GameObject เปล่าในฉาก แล้วกด Play — ปุ่มจะขึ้นเอง
/// ไม่ต้องลาก reference อะไรเลย เพราะวาดด้วย OnGUI
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
        }
        catch (Exception e)
        {
            session = null;
            status = "สร้างห้องไม่สำเร็จ: " + e.Message;
            Debug.LogException(e);
        }
        finally
        {
            busy = false;
        }
    }

    private async Task JoinAsync()
    {
        string code = joinCodeInput.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            status = "ใส่รหัสห้องก่อน";
            return;
        }

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
        }
        catch (Exception e)
        {
            session = null;
            status = "เข้าห้องไม่สำเร็จ: " + e.Message;
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
            ? "กำลังเข้าเกม..."
            : $"เข้าเกมไม่สำเร็จ: {result} (ใส่ซีนใน Build Settings หรือยัง)";
    }

    private bool IsInGameScene()
    {
        return SceneManager.GetActiveScene().name == gameSceneName;
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
    }

    // ---------- หน้าตา ----------
    //
    // ทำด้วย IMGUI ทั้งหมดโดยตั้งใจ ไม่ใช้ Canvas เพราะ Canvas ต้องสร้าง
    // GameObject หลายชั้นและลาก reference ผูกกันเยอะ ซึ่งพังง่ายเวลาลบของผิด
    // แบบนี้อยู่ในไฟล์เดียว แก้สีแก้ขนาดได้ที่เดียว และไม่มีอะไรให้ลบหาย

    private static readonly Color PanelColor = new Color(0.09f, 0.10f, 0.16f, 0.96f);
    private static readonly Color AccentColor = new Color(0.42f, 0.72f, 1f);

    private GUIStyle titleStyle;
    private GUIStyle headingStyle;
    private GUIStyle bodyStyle;
    private GUIStyle codeStyle;
    private GUIStyle buttonStyle;
    private GUIStyle inputStyle;
    private Texture2D panelTexture;

    private void BuildStyles()
    {
        if (titleStyle != null) return;

        panelTexture = new Texture2D(1, 1);
        panelTexture.SetPixel(0, 0, PanelColor);
        panelTexture.Apply();

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        titleStyle.normal.textColor = AccentColor;

        headingStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter,
        };
        headingStyle.normal.textColor = new Color(0.75f, 0.78f, 0.85f);

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
        };
        bodyStyle.normal.textColor = new Color(0.85f, 0.87f, 0.92f);

        // รหัสห้องต้องอ่านง่ายที่สุดในจอ เพราะต้องอ่านให้เพื่อนฟังทางโทรศัพท์
        codeStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        codeStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17,
            fixedHeight = 46f,
        };

        inputStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 24,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 46f,
        };
    }

    private void OnGUI()
    {
        BuildStyles();

        bool connected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // อยู่ในสนามรบแล้วให้ย่อเหลือแถบเล็ก ๆ มุมจอ ไม่บังพื้นที่เล่น
        if (connected && IsInGameScene())
        {
            DrawCompactBar();
            return;
        }

        const float width = 420f;
        const float height = 380f;
        var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.DrawTexture(panel, panelTexture);

        GUILayout.BeginArea(new Rect(panel.x + 28f, panel.y + 24f, panel.width - 56f, panel.height - 48f));

        GUILayout.Label("วงเวทออนไลน์", titleStyle);
        GUILayout.Space(4);

        GUI.enabled = ready && !busy;

        if (!connected) DrawJoinPanel();
        else DrawRoomPanel();

        GUI.enabled = true;

        GUILayout.FlexibleSpace();
        GUILayout.Label(string.IsNullOrEmpty(status) ? " " : status, bodyStyle);

        GUILayout.EndArea();
    }

    private void DrawJoinPanel()
    {
        GUILayout.Label("สร้างห้องแล้วส่งรหัสให้เพื่อน หรือใส่รหัสที่ได้รับ", headingStyle);
        GUILayout.Space(14);

        if (GUILayout.Button("สร้างห้องใหม่", buttonStyle))
            _ = HostAsync();

        GUILayout.Space(18);
        GUILayout.Label("— หรือ —", headingStyle);
        GUILayout.Space(10);

        // บังคับเป็นตัวใหญ่ทั้งหมด รหัสห้องเป็นตัวใหญ่เสมอ ผู้เล่นจะได้ไม่ต้องกด Shift
        joinCodeInput = GUILayout.TextField(joinCodeInput, 10, inputStyle).ToUpperInvariant();

        GUILayout.Space(8);
        if (GUILayout.Button("เข้าห้องด้วยรหัส", buttonStyle))
            _ = JoinAsync();
    }

    private void DrawRoomPanel()
    {
        bool isHost = NetworkManager.Singleton.IsHost;

        GUILayout.Label(isHost ? "คุณเป็นเจ้าของห้อง" : "คุณเข้าร่วมห้องแล้ว", headingStyle);
        GUILayout.Space(10);

        if (session != null)
        {
            GUILayout.Label("รหัสห้อง", headingStyle);
            GUILayout.Label(session.Code, codeStyle, GUILayout.Height(58f));

            GUI.enabled = true;
            if (GUILayout.Button("คัดลอกรหัส", buttonStyle))
            {
                GUIUtility.systemCopyBuffer = session.Code;
                status = "คัดลอกรหัสแล้ว";
            }
            GUI.enabled = ready && !busy;
        }

        GUILayout.Space(10);
        GUILayout.Label(
            $"ผู้เล่นในห้อง {NetworkManager.Singleton.ConnectedClientsIds.Count} / {maxPlayers} คน",
            bodyStyle);

        GUILayout.Space(12);

        // เฉพาะ Host เท่านั้นที่สั่งเริ่มเกมได้ ถ้าให้ทุกคนกดได้จะแย่งกันโหลดซีน
        if (isHost)
        {
            if (GUILayout.Button("เริ่มเกม", buttonStyle))
                StartGame();
        }
        else
        {
            GUILayout.Label("รอเจ้าของห้องกดเริ่มเกม...", bodyStyle);
        }

        GUILayout.Space(8);
        if (GUILayout.Button("ออกจากห้อง", buttonStyle))
            _ = LeaveAsync();
    }

    /// <summary>
    /// แถบเล็กมุมซ้ายบนตอนอยู่ในสนามรบ
    /// ยังต้องเห็นรหัสห้องและออกจากห้องได้ แต่ไม่ควรกินพื้นที่เล่น
    /// </summary>
    private void DrawCompactBar()
    {
        var bar = new Rect(12f, 12f, 210f, 74f);
        GUI.DrawTexture(bar, panelTexture);

        GUILayout.BeginArea(new Rect(bar.x + 10f, bar.y + 8f, bar.width - 20f, bar.height - 16f));

        string code = session != null ? session.Code : "-";
        GUILayout.Label($"ห้อง {code}   ผู้เล่น {NetworkManager.Singleton.ConnectedClientsIds.Count}", bodyStyle);

        if (GUILayout.Button("ออกจากห้อง"))
            _ = LeaveAsync();

        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        // Texture ที่สร้างด้วยโค้ดไม่ถูกเก็บอัตโนมัติ ต้องทำลายเอง
        if (panelTexture != null) Destroy(panelTexture);
    }

    private void OnApplicationQuit()
    {
        // ปิดเกมทั้งที่ยังอยู่ในห้อง = ห้องค้างบน Cloud จนกว่าจะหมดอายุ
        if (session != null)
            _ = LeaveAsync();
    }
}
