using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// สร้างตัวละครให้ผู้เล่นตอนเข้าสนามรบ ไม่ใช่ตอนเข้าห้อง
///
/// ค่าเริ่มต้นของ Netcode คือสร้างตัวละครทันทีที่เชื่อมต่อสำเร็จ ซึ่งเกิดขึ้น
/// ตั้งแต่ยังอยู่ในซีนเมนู ผลคือมีแคปซูลโผล่มายืนกลางหน้าเมนู
///
/// แก้โดยเอา Player Prefab ออกจาก NetworkConfig แล้วมาสร้างเองตรงนี้แทน
/// (prefab ยังต้องอยู่ในรายการ Prefabs ของ NetworkConfig เพื่อให้ Netcode
/// รู้จักตอนซิงก์ข้ามเครื่อง แค่ไม่ให้มันสร้างอัตโนมัติ)
///
/// วางไว้บน GameObject เดียวกับ NetworkManager
/// </summary>
[RequireComponent(typeof(NetworkManager))]
public class PlayerSpawner : MonoBehaviour
{
    [Tooltip("Prefab ตัวละคร ต้องมี NetworkObject")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("Prefab ตัวจัดการรอบ ต้องมี NetworkObject มีตัวเดียวทั้งเกม")]
    [SerializeField] private GameObject matchManagerPrefab;

    [Tooltip("ชื่อซีนที่จะสร้างตัวละคร ต้องตรงกับชื่อซีนสนามรบ")]
    [SerializeField] private string gameSceneName = "Game";

    [Tooltip("ระยะห่างระหว่างจุดเกิดของผู้เล่นแต่ละคน กันเกิดทับกัน")]
    [SerializeField] private float spawnSpacing = 3f;

    [Tooltip("ความสูงของจุดเกิด ควรอยู่เหนือพื้นพอให้ตกลงมายืนได้")]
    [SerializeField] private float spawnHeight = 0f;

    private NetworkManager manager;

    // จำไว้ว่าใครมีตัวละครแล้ว กันสร้างซ้ำตอนโหลดซีนรอบสอง
    private readonly HashSet<ulong> spawned = new HashSet<ulong>();

    private void Awake()
    {
        manager = GetComponent<NetworkManager>();
    }

    private void OnEnable()
    {
        if (manager == null) return;

        manager.OnServerStarted += HandleServerStarted;
        manager.OnClientConnectedCallback += HandleClientConnected;
        manager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void OnDisable()
    {
        if (manager == null) return;

        manager.OnServerStarted -= HandleServerStarted;
        manager.OnClientConnectedCallback -= HandleClientConnected;
        manager.OnClientDisconnectCallback -= HandleClientDisconnected;

        if (manager.SceneManager != null)
            manager.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
    }

    private void HandleServerStarted()
    {
        if (!manager.IsServer) return;

        // SceneManager ของ Netcode จะมีตัวตนก็ต่อเมื่อเริ่มทำงานแล้ว
        // ผูก event ตั้งแต่ Awake ไม่ได้เพราะตอนนั้นยังเป็น null
        manager.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
    }

    /// <summary>ทุกคนโหลดซีนเสร็จพร้อมกันแล้ว ถึงเวลาสร้างตัวละคร</summary>
    private void HandleSceneLoaded(
        string sceneName,
        LoadSceneMode mode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (!manager.IsServer) return;
        if (sceneName != gameSceneName) return;

        // ต้องมีตัวจัดการรอบก่อนผู้เล่นคนแรกเกิด ไม่งั้นคนแรกจะรายงานตัวไม่ทัน
        // แล้วจำนวนคนที่ยังรอดจะนับขาดไปหนึ่ง
        SpawnMatchManager();

        foreach (ulong clientId in clientsCompleted)
            SpawnFor(clientId);
    }

    /// <summary>คนที่เข้ามาทีหลังตอนเกมเริ่มไปแล้ว ต้องได้ตัวละครด้วย</summary>
    private void HandleClientConnected(ulong clientId)
    {
        if (!manager.IsServer) return;
        if (SceneManager.GetActiveScene().name != gameSceneName) return;

        SpawnFor(clientId);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        spawned.Remove(clientId);
    }

    /// <summary>
    /// สร้างตัวจัดการรอบ ครั้งเดียวต่อหนึ่งเกม
    ///
    /// ต้องเป็น prefab ที่ spawn ผ่านเครือข่าย ไม่ใช่ component ที่แปะไว้ในฉาก
    /// เพราะมันเป็น NetworkBehaviour ซึ่งต้องมี NetworkObject คู่กันเสมอ
    /// ถ้าไม่ spawn จะสั่งงานฝั่ง Server ไม่ได้เลย
    /// </summary>
    private void SpawnMatchManager()
    {
        if (matchManagerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] ยังไม่ได้ใส่ Match Manager Prefab ระบบแพ้ชนะจะไม่ทำงาน", this);
            return;
        }

        if (MagicDrawing.MatchManager.Instance != null) return;

        GameObject instance = Instantiate(matchManagerPrefab);
        instance.GetComponent<NetworkObject>().Spawn();
    }

    private void SpawnFor(ulong clientId)
    {
        if (spawned.Contains(clientId)) return;

        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] ยังไม่ได้ใส่ Player Prefab จะไม่มีตัวละครโผล่มาเลย", this);
            return;
        }

        // ถ้ามีตัวละครอยู่แล้วก็ไม่ต้องสร้างซ้ำ เช่นตอนโหลดซีนรอบที่สอง
        if (manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
            && client.PlayerObject != null)
        {
            spawned.Add(clientId);
            return;
        }

        GameObject instance = Instantiate(playerPrefab, GetSpawnPosition(), Quaternion.identity);
        instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

        spawned.Add(clientId);
    }

    /// <summary>
    /// วางผู้เล่นเรียงกันไปทางขวาทีละช่วง กันเกิดทับกันจนดันกันกระเด็น
    /// ใช้จำนวนคนที่เกิดแล้วเป็นตัวนับ ไม่ใช้ clientId เพราะ id ไม่ได้เรียง 0,1,2
    /// </summary>
    private Vector3 GetSpawnPosition()
    {
        int index = spawned.Count;

        // สลับซ้ายขวารอบจุดกึ่งกลาง 0, +3, -3, +6, -6 ...
        int step = (index + 1) / 2;
        float sign = index % 2 == 0 ? 1f : -1f;

        return new Vector3(step * spawnSpacing * sign, spawnHeight, 0f);
    }
}
