using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// NetworkTransform ที่ "เชื่อเจ้าของตัวละคร" แทนที่จะเชื่อ Server อย่างเดียว
///
/// ค่าเริ่มต้นของ NetworkTransform คือ Server เป็นคนกำหนดตำแหน่ง ผลคือเวลาเรากดเดิน
/// ตัวเราจะกระตุก เพราะต้องรอ Server ตอบกลับก่อน คลาสนี้กลับด้านให้เครื่องของเจ้าของ
/// เป็นคนส่งตำแหน่งขึ้นไปแทน ทำให้ตัวเราเดินลื่นทันที
///
/// ข้อแลกเปลี่ยน: Server ไม่ได้ตรวจตำแหน่ง ถ้าจะทำเกมแข่งขันจริงจังที่กันโกง
/// ต้องเปลี่ยนกลับไปเป็น Server-authoritative
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform2D : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
