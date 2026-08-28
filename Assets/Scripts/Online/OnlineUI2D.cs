using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

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

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16, 16, 320, 260), GUI.skin.box);

        bool connected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        GUI.enabled = ready && !busy;

        if (!connected)
        {
            GUILayout.Label("เล่นออนไลน์");

            if (GUILayout.Button("สร้างห้อง (เป็น Host)"))
                _ = HostAsync();

            GUILayout.Space(8);
            GUILayout.Label("รหัสห้อง");
            joinCodeInput = GUILayout.TextField(joinCodeInput, 10);

            if (GUILayout.Button("เข้าห้อง"))
                _ = JoinAsync();
        }
        else
        {
            GUILayout.Label(NetworkManager.Singleton.IsHost ? "คุณเป็น Host" : "คุณเป็นผู้เล่น");

            if (session != null)
            {
                GUILayout.Label("รหัสห้อง: " + session.Code);
                GUI.enabled = true;
                if (GUILayout.Button("คัดลอกรหัส"))
                    GUIUtility.systemCopyBuffer = session.Code;
                GUI.enabled = ready && !busy;
            }

            GUILayout.Label("ผู้เล่นในห้อง: " + NetworkManager.Singleton.ConnectedClientsIds.Count);

            GUILayout.Space(8);
            if (GUILayout.Button("ออกจากห้อง"))
                _ = LeaveAsync();
        }

        GUI.enabled = true;
        GUILayout.Space(8);
        GUILayout.Label(status);

        GUILayout.EndArea();
    }

    private void OnApplicationQuit()
    {
        // ปิดเกมทั้งที่ยังอยู่ในห้อง = ห้องค้างบน Cloud จนกว่าจะหมดอายุ
        if (session != null)
            _ = LeaveAsync();
    }
}
