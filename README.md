# เกม 2D ออนไลน์ (Unity 6)

โปรเจกต์ Unity 2D (URP) ที่ตั้งค่าระบบเล่นออนไลน์ด้วย **Netcode for GameObjects** + **Unity Sessions/Relay** ไว้แล้ว

**Unity ที่ต้องใช้:** 6000.3.6f1

---

## เปิดโปรเจกต์

Unity Hub → Add → เลือกโฟลเดอร์นี้ (โฟลเดอร์ที่มี `Assets/`, `Packages/`, `ProjectSettings/`)

ครั้งแรกจะช้าหน่อยเพราะ Unity ต้องดาวน์โหลด package ออนไลน์และสร้าง `Library/` ใหม่

---

## ของที่ติดตั้งไว้ให้แล้ว

| Package | ใช้ทำอะไร |
|---|---|
| `com.unity.netcode.gameobjects` | ระบบมัลติเพลเยอร์หลัก (ซิงก์ตำแหน่ง/ข้อมูลระหว่างเครื่อง) |
| `com.unity.services.multiplayer` | Sessions + Relay — ทำให้เล่นข้ามอินเทอร์เน็ตได้โดยไม่ต้องเปิดพอร์ตเราเตอร์ |
| `com.unity.transport` | ชั้นรับส่งข้อมูลที่ Netcode เรียกใช้ |
| `com.unity.multiplayer.playmode` | เปิดหน้าต่างผู้เล่นหลายคนใน Editor เดียว ใช้เทสได้โดยไม่ต้อง Build |

## สคริปต์ใน `Assets/Scripts/Online/`

| ไฟล์ | หน้าที่ |
|---|---|
| `OnlineUI2D.cs` | เมนูสร้างห้อง/เข้าห้องด้วยรหัส วาดด้วย OnGUI ไม่ต้องลาก reference |
| `NetworkPlayer2D.cs` | ตัวละคร 2D เดินด้วย WASD หรือลูกศร รับปุ่มเฉพาะตัวที่เราเป็นเจ้าของ |
| `ClientNetworkTransform2D.cs` | ให้เจ้าของตัวละครเป็นคนส่งตำแหน่ง ทำให้เดินลื่นไม่กระตุก |

---

## ตั้งค่าก่อนเล่นครั้งแรก

### 1. ผูกโปรเจกต์กับ Unity Cloud

Relay เป็นบริการบนคลาวด์ ถ้าไม่ผูกจะสร้างห้องไม่ได้

1. `Edit > Project Settings > Services`
2. กดเชื่อมโปรเจกต์กับ Unity account (สร้างใหม่หรือเลือกอันเดิม)
3. เปิดบริการ **Relay** และ **Lobby**

### 2. สร้าง Player Prefab

1. สร้าง GameObject 2D อะไรก็ได้ในฉาก เช่น `GameObject > 2D Object > Sprites > Square`
2. ใส่ component พวกนี้:
   - `Rigidbody 2D`
   - `Box Collider 2D`
   - `Network Object`
   - `Client Network Transform 2D`
   - `Network Player 2D`
3. ลากลง `Assets/` เพื่อทำเป็น Prefab แล้วลบตัวในฉากทิ้ง

> ต้องใช้ `Client Network Transform 2D` เท่านั้น **อย่าใส่ `Network Transform` ตัวปกติเพิ่ม** เพราะจะแย่งกันคุมตำแหน่ง

### 3. วาง NetworkManager

1. สร้าง GameObject เปล่า ตั้งชื่อ `NetworkManager`
2. ใส่ component `Network Manager`
3. ใน Inspector ช่อง **Network Transport** เลือก `Unity Transport`
4. ช่อง **Player Prefab** ลาก Prefab จากข้อ 2 ใส่

### 4. วางเมนูออนไลน์

สร้าง GameObject เปล่า ตั้งชื่อ `OnlineUI` แล้วใส่ component `Online UI 2D`

---

## วิธีเทสว่าเล่นออนไลน์ได้จริง

**แบบง่ายสุด — Multiplayer Play Mode**

1. `Window > Multiplayer > Multiplayer Play Mode`
2. ติ๊กเปิด Player 2
3. กด Play — จะมี 2 หน้าต่าง
4. หน้าต่างแรกกด "สร้างห้อง" แล้วกด "คัดลอกรหัส"
5. หน้าต่างที่สองวางรหัสลงช่อง แล้วกด "เข้าห้อง"
6. ลองกด WASD ทั้งสองหน้าต่าง — ต้องเห็นตัวละครอีกฝั่งขยับตาม

**เล่นกับเพื่อนคนละบ้าน:** Build เป็น .exe ส่งให้เพื่อน แล้วบอกรหัสห้อง Relay จัดการเรื่องเน็ตให้เอง

---

## ปัญหาที่เจอบ่อย

| อาการ | สาเหตุ |
|---|---|
| กดสร้างห้องแล้วขึ้น error เรื่อง project id | ยังไม่ได้ผูก Unity Cloud (ข้อ 1) |
| กดเดินแล้วตัวละครทุกตัวขยับพร้อมกัน | ลืมเช็ค `IsOwner` หรือใส่สคริปต์ผิดตัว |
| ตัวละครคนอื่นกระตุก | ใส่ `Network Transform` ตัวปกติทับ `Client Network Transform 2D` |
| กด Play แล้ว error เรื่อง `Input.GetAxis` | โปรเจกต์ตั้ง Input System แบบใหม่ ต้องใช้ `Keyboard.current` |
| เข้าห้องไม่ได้ทั้งที่รหัสถูก | Host ปิดเกมไปแล้ว หรือห้องเต็ม (ค่าเริ่มต้น 4 คน) |
