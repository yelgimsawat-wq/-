using System.Collections.Generic;
using MagicDrawing;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// กล้องตามตัวละครของเราเอง และตามคนที่ยังรอดเมื่อเราตกรอบแล้ว
///
/// ทำไมไม่ผูกกล้องเป็นลูกของตัวละครไปเลย (แบบที่เคยทำ):
/// พอแยกซีนห้องรอกับซีนเล่นเกม การโหลดซีนใหม่จะทำลายกล้องของซีนเก่าทิ้ง
/// แต่ตัวละครเป็น NetworkObject ที่ย้ายข้ามซีนไปด้วย ผลคือตัวละครยังอยู่
/// แต่กล้องหายไปแล้ว จอดำสนิทโดยไม่มี error
///
/// วิธีนี้กลับด้าน: กล้องอยู่ในซีนของมัน แล้วคอยมองหาตัวละครที่ควรตาม
/// ซึ่งทำให้รองรับการดูคนอื่นตอนตกรอบได้ฟรี ๆ เพราะแค่เปลี่ยนเป้าหมาย
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    [Tooltip("ความหนืดในการตาม ยิ่งมากยิ่งติดตัวละคร ยิ่งน้อยยิ่งลอยตามช้า ๆ")]
    [SerializeField] private float followSpeed = 8f;

    [Tooltip("ระยะเยื้องจากตัวละคร")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    [Tooltip("ตามหาตัวละครใหม่ทุกกี่วินาที ตอนที่ยังหาไม่เจอ")]
    [SerializeField] private float searchInterval = 0.5f;

    [Tooltip("ปุ่มสลับคนที่ดูตอนตกรอบแล้ว")]
    [SerializeField] private Key switchKey = Key.Tab;

    private Transform target;
    private float nextSearchTime;
    private int spectateIndex;

    /// <summary>ตอนนี้กำลังดูคนอื่นอยู่หรือเปล่า ใช้ให้ UI เอาไปบอกผู้เล่น</summary>
    public static bool IsSpectating { get; private set; }

    private void LateUpdate()
    {
        UpdateTarget();
        if (target == null) return;

        Vector3 desired = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            // ห้ามแตะแกน z ไม่งั้นกล้อง 2D จะเลื่อนเข้าไปในฉากจนมองไม่เห็นอะไร
            transform.position.z);

        transform.position = Vector3.Lerp(
            transform.position, desired, followSpeed * Time.deltaTime);
    }

    private void UpdateTarget()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
        {
            IsSpectating = false;
            return;
        }

        NetworkObject localPlayer = manager.LocalClient?.PlayerObject;
        PlayerHealth localHealth = localPlayer != null
            ? localPlayer.GetComponent<PlayerHealth>()
            : null;

        // ยังไม่ตาย (หรือยังไม่มีตัวละคร) ก็ตามตัวเราเองตามปกติ
        if (localHealth != null && !localHealth.IsEliminated)
        {
            IsSpectating = false;
            spectateIndex = 0;
            SnapOrFollow(localPlayer.transform);
            return;
        }

        if (localPlayer == null)
        {
            IsSpectating = false;
            SearchForLocalPlayer(manager);
            return;
        }

        FollowSurvivor(manager);
    }

    /// <summary>
    /// ตกรอบแล้ว ไปตามดูคนที่ยังรอด
    /// กด Tab สลับไปคนถัดไปได้ ถ้าคนที่ดูอยู่ตายไป จะเลื่อนไปคนอื่นเอง
    /// </summary>
    private void FollowSurvivor(NetworkManager manager)
    {
        List<Transform> survivors = CollectSurvivors(manager);

        if (survivors.Count == 0)
        {
            // ไม่เหลือใครแล้ว ค้างกล้องไว้ที่เดิม ดีกว่ากระโดดไปจุดศูนย์
            IsSpectating = false;
            return;
        }

        IsSpectating = true;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[switchKey].wasPressedThisFrame)
            spectateIndex++;

        // วนกลับมาต้นแถวเสมอ และกันดัชนีเกินตอนมีคนตายไประหว่างดู
        spectateIndex = ((spectateIndex % survivors.Count) + survivors.Count) % survivors.Count;

        SnapOrFollow(survivors[spectateIndex]);
    }

    private static List<Transform> CollectSurvivors(NetworkManager manager)
    {
        var survivors = new List<Transform>();

        foreach (NetworkClient client in manager.ConnectedClientsList)
        {
            if (client?.PlayerObject == null) continue;

            var health = client.PlayerObject.GetComponent<PlayerHealth>();
            if (health == null || health.IsEliminated) continue;

            survivors.Add(client.PlayerObject.transform);
        }

        return survivors;
    }

    private void SearchForLocalPlayer(NetworkManager manager)
    {
        // ค้นเป็นระยะ ไม่ค้นทุกเฟรม เพราะช่วงก่อนเข้าห้องจะหาไม่เจออยู่แล้ว
        if (Time.time < nextSearchTime) return;
        nextSearchTime = Time.time + searchInterval;

        NetworkObject player = manager.LocalClient?.PlayerObject;
        if (player != null) SnapOrFollow(player.transform);
    }

    private void SnapOrFollow(Transform next)
    {
        bool changed = target != next;
        target = next;

        // เปลี่ยนเป้าหมายแล้วกระโดดไปเลย ไม่ต้องให้เห็นกล้องไถลข้ามแมพ
        if (!changed) return;

        transform.position = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z);
    }
}
