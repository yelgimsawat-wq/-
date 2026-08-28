using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MagicDrawing
{
    /// <summary>
    /// ซิงก์ชื่อและรูปตัวละครที่ผู้เล่นวาดไว้ ให้ทุกเครื่องเห็นตรงกัน
    ///
    /// ใช้ NetworkVariable แบบให้เจ้าของเขียนได้ ไม่ใช่ RPC เพราะ
    /// NetworkVariable ส่งค่าให้คนที่เข้ามาทีหลังอัตโนมัติ
    /// ถ้าใช้ RPC จะต้องเก็บสำเนาไว้ที่ Server แล้วส่งซ้ำเองทุกครั้งที่มีคนเข้าใหม่
    /// ซึ่งลืมง่ายและกลายเป็นคนเข้าทีหลังเห็นตัวละครเปล่า
    ///
    /// ส่งเป็น "ชุดเส้น" ไม่ใช่ไฟล์ภาพ รูปขนาด 128x128 เป็น PNG ราว 5-10 KB
    /// แต่ชุดเส้นเดียวกันใช้ไม่ถึง 3 KB และแต่ละเครื่องเอาไปวาดเป็นภาพเอง
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerAppearance : NetworkBehaviour
    {
        [Tooltip("ตัวที่จะเอารูปที่วาดไปใส่ ปล่อยว่าง = หาในลูกให้เอง")]
        [SerializeField] private SpriteRenderer targetRenderer;

        [Tooltip("สีเส้นตัวละคร")]
        [SerializeField] private Color inkColor = Color.white;

        [Tooltip("ขนาดตัวละครเป็นหน่วยโลก ควรตรงกับความสูงของ collider")]
        [SerializeField] private float worldSize = 1.5f;

        private readonly NetworkVariable<FixedString64Bytes> displayName =
            new NetworkVariable<FixedString64Bytes>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        // 4096 ไบต์พอสำหรับ 10 เส้น เส้นละ 20 จุด ซึ่งเป็นเพดานที่ PlayerProfile กำหนด
        private readonly NetworkVariable<FixedString4096Bytes> appearance =
            new NetworkVariable<FixedString4096Bytes>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        private Sprite generatedSprite;
        private Sprite originalSprite;

        /// <summary>ชื่อที่จะเอาไปแสดงเหนือหัว ว่างแปลว่ายังไม่ได้ตั้ง</summary>
        public string DisplayName => displayName.Value.ToString();

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<SpriteRenderer>();
            if (targetRenderer != null) originalSprite = targetRenderer.sprite;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            displayName.OnValueChanged += OnNameChanged;
            appearance.OnValueChanged += OnAppearanceChanged;

            if (IsOwner) PublishProfile();

            // คนที่เข้ามาทีหลังได้ค่าตอน spawn อยู่แล้ว แต่ต้องสั่งวาดเองรอบแรก
            // เพราะ OnValueChanged ไม่ยิงสำหรับค่าที่มีอยู่ก่อนแล้ว
            ApplyAppearance();
        }

        public override void OnNetworkDespawn()
        {
            displayName.OnValueChanged -= OnNameChanged;
            appearance.OnValueChanged -= OnAppearanceChanged;

            base.OnNetworkDespawn();
        }

        private void OnDestroy()
        {
            // Sprite ที่สร้างเองพร้อม Texture ข้างในต้องทำลายเอง ไม่งั้นรั่ว
            DestroyGenerated();
        }

        /// <summary>ส่งชื่อและรูปที่ตั้งไว้ในเมนูขึ้นไปให้คนอื่นเห็น</summary>
        private void PublishProfile()
        {
            string name = PlayerProfile.Name;
            if (string.IsNullOrEmpty(name))
            {
                // ไม่ได้ตั้งชื่อก็ยังต้องมีอะไรให้เรียก ไม่งั้นป้ายเหนือหัวว่างเปล่า
                name = $"ผู้เล่น {OwnerClientId}";
            }

            displayName.Value = new FixedString64Bytes(PlayerProfile.Sanitize(name));

            string encoded = PlayerProfile.EncodedAppearance;
            if (!string.IsNullOrEmpty(encoded))
            {
                // กันข้อมูลยาวเกินช่อง ถ้าเกินก็ไม่ส่งดีกว่าส่งแล้วขาดกลางทาง
                if (encoded.Length < 4000) appearance.Value = new FixedString4096Bytes(encoded);
                else Debug.LogWarning("[PlayerAppearance] รูปตัวละครใหญ่เกินไป จะใช้รูปเริ่มต้นแทน");
            }
        }

        private void OnNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
        {
            // ป้ายชื่อวาดโดย PlayerHealth ทุกเฟรมอยู่แล้ว ไม่ต้องทำอะไรตรงนี้
        }

        private void OnAppearanceChanged(FixedString4096Bytes oldValue, FixedString4096Bytes newValue)
        {
            ApplyAppearance();
        }

        private void ApplyAppearance()
        {
            if (targetRenderer == null) return;

            string encoded = appearance.Value.ToString();

            if (string.IsNullOrEmpty(encoded))
            {
                // ไม่ได้วาดอะไรไว้ ใช้รูปเริ่มต้นที่ติดมากับ prefab
                targetRenderer.sprite = originalSprite;
                return;
            }

            DestroyGenerated();

            var strokes = PlayerProfile.Decode(encoded);

            // pixelsPerUnit คิดจากขนาดที่อยากได้ในโลก ภาพจึงกว้างเท่าที่ตั้งไว้เสมอ
            // ไม่ว่าจะอบด้วยความละเอียดเท่าไร
            float pixelsPerUnit = AppearanceRenderer.TextureSize / Mathf.Max(0.01f, worldSize);

            generatedSprite = AppearanceRenderer.BakeSprite(strokes, inkColor, pixelsPerUnit);
            targetRenderer.sprite = generatedSprite;
        }

        private void DestroyGenerated()
        {
            if (generatedSprite == null) return;

            if (generatedSprite.texture != null) Destroy(generatedSprite.texture);
            Destroy(generatedSprite);
            generatedSprite = null;
        }
    }
}
