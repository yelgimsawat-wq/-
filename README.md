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
   - `Spell Caster` (ระบบวงเวท)
   - `Spell Drawing` (ระบบวงเวท)
3. ลากลง `Assets/` เพื่อทำเป็น Prefab แล้วลบตัวในฉากทิ้ง

> ต้องใช้ `Client Network Transform 2D` เท่านั้น **อย่าใส่ `Network Transform` ตัวปกติเพิ่ม** เพราะจะแย่งกันคุมตำแหน่ง

### 3. วาง NetworkManager

1. สร้าง GameObject เปล่า ตั้งชื่อ `NetworkManager`
2. ใส่ component `Network Manager`
3. ใน Inspector ช่อง **Network Transport** เลือก `Unity Transport`
4. ช่อง **Player Prefab** ลาก Prefab จากข้อ 2 ใส่

### 4. วางเมนูออนไลน์

สร้าง GameObject เปล่า ตั้งชื่อ `OnlineUI` แล้วใส่ component `Online UI 2D`

### 5. สร้าง Prefab วงเวท

1. สร้าง GameObject 2D ใส่ `Sprite Renderer` แล้วใส่ภาพวงเวท (ไฟล์ควรเป็น PNG พื้นหลังโปร่งใส)
2. ใส่ component `Magic Circle`
3. ลากลง `Assets/` ทำเป็น Prefab แล้วลบตัวในฉากทิ้ง
4. กลับไปที่ Player Prefab → ช่อง **Element Visuals** ของ `Spell Caster` ใส่ 4 ช่อง (น้ำ/ไฟ/ดิน/ลม) แล้วลาก Prefab วงเวทใส่แต่ละธาตุ

> ยังไม่มีอาร์ต? ใส่แค่ **Fallback Circle Prefab** ช่องเดียวก็เล่นได้ ทุกธาตุจะใช้วงเดียวกันแต่เปลี่ยนสีตามธาตุให้อัตโนมัติ

---

## ระบบวาดวงเวท

ร่ายเวทด้วยการ **ลากเมาส์หรือนิ้ววาดรูปทรง** บนหน้าจอ ปล่อยมือแล้วระบบจะตัดสินธาตุให้

| วาด | ได้เวท | เงื่อนไข |
|---|---|---|
| วงกลม | น้ำ | ความแม่นยำเกิน 70% |
| สามเหลี่ยม | ไฟ | ความแม่นยำเกิน 70% |
| สี่เหลี่ยม | ดิน | ความแม่นยำเกิน 70% |
| อะไรก็ตามที่ไม่เข้าพวก | ลม | ต่ำกว่า 70% ทั้งหมด |

ใช้อัลกอริทึม **$1 Unistroke Recognizer** เขียนไว้ใน `Assets/Scripts/Spells/` ไม่ต้องติดตั้งไลบรารีเพิ่ม

### ไฟล์ในระบบ

| ไฟล์ | หน้าที่ |
|---|---|
| `DollarOneRecognizer.cs` | อัลกอริทึมเทียบรูปทรง (resample → หมุน → ย่อขยาย → ย้ายจุดกลาง) |
| `SpellShapeLibrary.cs` | สร้างแม่แบบรูปทรง และกฎแปลงรูปทรงเป็นธาตุ |
| `SpellElement.cs` | ธาตุทั้ง 4 พร้อมชื่อไทยและสีประจำธาตุ |
| `SpellDrawing.cs` | รับการลากเมาส์/นิ้ว เก็บจุด และเรียกตรวจรูปทรง |
| `SpellCaster.cs` | ส่งผลข้ามเน็ตด้วย RPC ให้ทุกเครื่องเห็นตรงกัน |
| `MagicCircle.cs` | วงเวทที่โผล่หน้าตัวละคร จางเข้า-ออก หมุน ขยาย |
| `SpellStrokeView.cs` | เส้นที่วาดค้างบนแผนที่แล้วจางหาย |

### ค่าที่ปรับได้ใน Inspector

ที่ `Spell Drawing` บน Player Prefab:

| ช่อง | ความหมาย |
|---|---|
| **Min Point Distance** | ระยะห่างขั้นต่ำระหว่างจุด ยิ่งมากยิ่งประหยัดข้อมูลแต่เส้นหยาบ |
| **Minimum Score** | เกณฑ์ความแม่นยำ ลดลงถ้ารู้สึกว่าวาดถูกแล้วแต่ยังได้เวทลม |
| **Min Stroke Length** | เส้นสั้นกว่านี้ถือว่าเผลอจิ้ม ไม่นับเป็นการร่าย |
| **Log Recognition** | เปิดไว้จะพิมพ์ผลลง Console ว่าวาดได้รูปอะไร แม่นกี่เปอร์เซ็นต์ — ใช้จูนเกณฑ์ |

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
| วาดแล้วได้เวทลมตลอด | ลด **Minimum Score** ลง แล้วดู Console ว่าคะแนนจริงได้เท่าไร |
| วาดแล้วไม่มีอะไรเกิดขึ้น | ยังไม่ได้ใส่ Prefab วงเวทใน `Spell Caster` หรือเส้นสั้นกว่า **Min Stroke Length** |
| เห็นวงเวทแค่ฝั่งเดียว | `Spell Caster` ต้องอยู่บน Player Prefab ที่มี `Network Object` และต้องอยู่ในห้องแล้ว |
| วงเวทโดนฉากบัง | เพิ่ม **Sorting Order** ใน `Magic Circle` หรือระบุ **Sorting Layer Name** ให้เป็นเลเยอร์หน้าสุด |
| วงเวทมีกรอบสี่เหลี่ยมดำ | ไฟล์ภาพไม่โปร่งใส หรือปิด **Use Additive Blending** ไว้ |
