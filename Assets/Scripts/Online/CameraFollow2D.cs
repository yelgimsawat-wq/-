using Unity.Netcode;
using UnityEngine;

/// <summary>
/// กล้องตามตัวละครของเราเอง
///
/// ทำไมไม่ผูกกล้องเป็นลูกของตัวละครไปเลย (แบบที่เคยทำ):
/// พอแยกซีนห้องรอกับซีนเล่นเกม การโหลดซีนใหม่จะทำลายกล้องของซีนเก่าทิ้ง
/// แต่ตัวละครเป็น NetworkObject ที่ย้ายข้ามซีนไปด้วย ผลคือตัวละครยังอยู่
/// แต่กล้องหายไปแล้ว จอดำสนิทโดยไม่มี error
///
/// วิธีนี้กลับด้าน: กล้องอยู่ในซีนของมัน แล้วคอยมองหาตัวละครของเราเอง
/// ตัวละครจะเกิดตอนไหน เปลี่ยนซีนกี่รอบ กล้องก็หาเจอเสมอ
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    [Tooltip("ความหนืดในการตาม ยิ่งมากยิ่งติดตัวละคร ยิ่งน้อยยิ่งลอยตามช้า ๆ")]
    [SerializeField] private float followSpeed = 8f;

    [Tooltip("ระยะเยื้องจากตัวละคร")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    [Tooltip("ตามหาตัวละครใหม่ทุกกี่วินาที ตอนที่ยังหาไม่เจอ")]
    [SerializeField] private float searchInterval = 0.5f;

    private Transform target;
    private float nextSearchTime;

    private void LateUpdate()
    {
        if (target == null)
        {
            TryFindLocalPlayer();
            if (target == null) return;
        }

        Vector3 desired = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            // ห้ามแตะแกน z ไม่งั้นกล้อง 2D จะเลื่อนเข้าไปในฉากจนมองไม่เห็นอะไร
            transform.position.z);

        transform.position = Vector3.Lerp(
            transform.position, desired, followSpeed * Time.deltaTime);
    }

    private void TryFindLocalPlayer()
    {
        // ค้นเป็นระยะ ไม่ค้นทุกเฟรม เพราะช่วงก่อนเข้าห้องจะหาไม่เจออยู่แล้ว
        if (Time.time < nextSearchTime) return;
        nextSearchTime = Time.time + searchInterval;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening) return;

        NetworkObject player = manager.LocalClient?.PlayerObject;
        if (player == null) return;

        target = player.transform;

        // กระโดดไปที่ตัวละครทันทีในเฟรมแรก ไม่ต้องให้เห็นกล้องไถลมาจากที่เดิม
        transform.position = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z);
    }
}
