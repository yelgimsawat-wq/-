using System.IO;
using MagicDrawing;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// ติดตั้งฉากเกมวาดวงเวทให้อัตโนมัติ
///
/// แทนการลาก component และผูกช่องต่าง ๆ ใน Inspector ทีละอัน ซึ่งพลาดง่าย
/// และอธิบายเป็นตัวหนังสือแล้วยาว สคริปต์นี้สร้างทุกอย่างให้ครบในคลิกเดียว
///
/// สั่งซ้ำได้ ของเดิมจะถูกเขียนทับ ไม่สร้างซ้อนกัน
///
/// สิ่งที่สร้าง:
/// - Assets/Art/Generated/MagicCircle.png   ภาพวงเวทวาดด้วยโค้ด (พื้นหลังโปร่งใส)
/// - Assets/Prefabs/MagicCircle.prefab      Prefab วงเวท
/// - Assets/Prefabs/Player.prefab           Prefab ผู้เล่น ใส่ครบทั้งเดินและร่ายเวท
/// - NetworkManager ในฉาก พร้อมผูก Player Prefab และ Unity Transport
/// - OnlineUI ในฉาก เมนูสร้าง/เข้าห้อง
/// - EventSystem ในฉาก เพื่อให้ระบบวาดรู้ว่าเมื่อไรนิ้วอยู่บนปุ่ม UI
/// </summary>
public static class MagicGameSetup
{
    private const string ArtFolder = "Assets/Art/Generated";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string CircleTexturePath = ArtFolder + "/MagicCircle.png";
    private const string CirclePrefabPath = PrefabFolder + "/MagicCircle.prefab";
    private const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";

    private const int CircleTextureSize = 256;

    [MenuItem("Tools/เกมวาดวงเวท/ติดตั้งฉากอัตโนมัติ", priority = 0)]
    public static void SetupEverything()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder(ArtFolder);
        EnsureFolder(PrefabFolder);

        Sprite circleSprite = CreateMagicCircleSprite();
        MagicCircle circlePrefab = CreateMagicCirclePrefab(circleSprite);
        GameObject playerPrefab = CreatePlayerPrefab(circlePrefab);

        SetupNetworkManager(playerPrefab);
        SetupOnlineUI();
        SetupEventSystem();
        SetupCamera();

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MagicGameSetup] ติดตั้งเสร็จแล้ว\n"
            + "กด Play ได้เลย จากนั้นกดปุ่ม 'สร้างห้อง' มุมซ้ายบน แล้วลากเมาส์วาดรูปทรงเพื่อร่ายเวท\n"
            + "วงกลม=น้ำ  สามเหลี่ยม=ไฟ  สี่เหลี่ยม=ดิน  วาดมั่ว=ลม"
        );
    }

    // ---------- ภาพวงเวท ----------

    /// <summary>
    /// วาดวงเวทด้วยโค้ดแล้วเซฟเป็น PNG พื้นหลังโปร่งใส
    /// ทำแบบนี้เพราะยังไม่มีอาร์ตจริง และได้ภาพที่ตรงข้อกำหนดข้อ 3.3 แน่นอน
    /// (พื้นหลัง alpha เป็น 0 ไม่มีกรอบสี่เหลี่ยมดำ)
    /// เมื่อมีอาร์ตจริงแล้วเปลี่ยน Sprite ใน Prefab ได้เลย ไม่ต้องแก้โค้ด
    /// </summary>
    private static Sprite CreateMagicCircleSprite()
    {
        var texture = new Texture2D(CircleTextureSize, CircleTextureSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[CircleTextureSize * CircleTextureSize];

        float center = (CircleTextureSize - 1) * 0.5f;
        float maxRadius = center;

        for (int y = 0; y < CircleTextureSize; y++)
        {
            for (int x = 0; x < CircleTextureSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float radius = Mathf.Sqrt(dx * dx + dy * dy) / maxRadius;
                float angle = Mathf.Atan2(dy, dx);

                // วงแหวนสามชั้น ความสว่างค่อย ๆ ลดลงเมื่อห่างจากเส้นวง
                float alpha = 0f;
                alpha += Ring(radius, 0.95f, 0.020f);
                alpha += Ring(radius, 0.88f, 0.010f);
                alpha += Ring(radius, 0.58f, 0.014f);

                // ขีดรอบวงเหมือนอักขระเวท 12 ขีด
                float ticks = Mathf.Abs(Mathf.Cos(angle * 6f));
                if (radius > 0.62f && radius < 0.85f)
                    alpha += Mathf.SmoothStep(0f, 1f, Mathf.Pow(ticks, 24f)) * 0.85f;

                // เรืองแสงจาง ๆ ตรงกลาง ทำให้ไม่โบ๋
                alpha += Mathf.Max(0f, 1f - radius) * 0.10f;

                alpha = Mathf.Clamp01(alpha);

                // สีขาวล้วน เพื่อให้ย้อมเป็นสีธาตุได้สวยทุกสี
                pixels[y * CircleTextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        File.WriteAllBytes(CircleTexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(CircleTexturePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(CircleTexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        // 256 พิกเซลต่อ 1 หน่วยโลก ทำให้วงเวทกว้างประมาณ 1 หน่วยพอดี
        importer.spritePixelsPerUnit = 256f;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(CircleTexturePath);
    }

    /// <summary>ความสว่างของวงแหวนหนึ่งวง จางลงตามระยะห่างจากเส้น</summary>
    private static float Ring(float radius, float target, float thickness)
    {
        float distance = Mathf.Abs(radius - target);
        return Mathf.Clamp01(1f - distance / thickness);
    }

    // ---------- Prefab ----------

    private static MagicCircle CreateMagicCirclePrefab(Sprite sprite)
    {
        var root = new GameObject("MagicCircle");

        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 500;

        root.AddComponent<MagicCircle>();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CirclePrefabPath);
        Object.DestroyImmediate(root);

        return saved.GetComponent<MagicCircle>();
    }

    private static GameObject CreatePlayerPrefab(MagicCircle circlePrefab)
    {
        var root = new GameObject("Player");

        // ใช้ภาพวงกลมที่ติดมากับ Unity เป็นตัวละครชั่วคราว
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        renderer.color = Color.white;
        renderer.sortingOrder = 10;

        var body = root.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        // เกมมองจากด้านบน ไม่ต้องให้ตัวลื่นไถลหลังปล่อยปุ่ม
        body.linearDamping = 8f;

        var collider = root.AddComponent<CircleCollider2D>();
        collider.radius = 0.5f;

        root.AddComponent<NetworkObject>();
        root.AddComponent<ClientNetworkTransform2D>();
        root.AddComponent<NetworkPlayer2D>();

        var caster = root.AddComponent<SpellCaster>();
        root.AddComponent<SpellDrawing>();

        WireSpellCaster(caster, circlePrefab);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        Object.DestroyImmediate(root);

        return saved;
    }

    /// <summary>
    /// ผูกช่องของ SpellCaster ที่เป็น private ผ่าน SerializedObject
    /// เข้าถึงตรง ๆ ไม่ได้เพราะเป็น [SerializeField] private ซึ่งถูกต้องแล้ว
    /// (ไม่ควรเปิดเป็น public แค่เพื่อให้สคริปต์ติดตั้งเขียนได้)
    /// </summary>
    private static void WireSpellCaster(SpellCaster caster, MagicCircle circlePrefab)
    {
        var so = new SerializedObject(caster);

        so.FindProperty("fallbackCirclePrefab").objectReferenceValue = circlePrefab;

        SerializedProperty visuals = so.FindProperty("elementVisuals");
        visuals.arraySize = 4;

        for (int i = 0; i < 4; i++)
        {
            SerializedProperty entry = visuals.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("element").enumValueIndex = i;
            entry.FindPropertyRelative("circlePrefab").objectReferenceValue = circlePrefab;
            entry.FindPropertyRelative("castEffectPrefab").objectReferenceValue = null;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- ของในฉาก ----------

    private static void SetupNetworkManager(GameObject playerPrefab)
    {
        GameObject go = ReplaceSceneObject("NetworkManager");

        var manager = go.AddComponent<NetworkManager>();
        var transport = go.AddComponent<UnityTransport>();

        if (manager.NetworkConfig == null)
            manager.NetworkConfig = new NetworkConfig();

        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.PlayerPrefab = playerPrefab;

        EditorUtility.SetDirty(go);
    }

    private static void SetupOnlineUI()
    {
        GameObject go = ReplaceSceneObject("OnlineUI");
        go.AddComponent<OnlineUI2D>();
        EditorUtility.SetDirty(go);
    }

    private static void SetupEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        // โปรเจกต์ตั้ง Input System แบบใหม่ ต้องใช้โมดูลของแพ็กเกจนั้น
        // ถ้าใส่ StandaloneInputModule ตัวเก่าจะขึ้น error ตอนกด Play
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        camera.orthographic = true;
        camera.orthographicSize = 6f;

        // กล้อง 2D ต้องถอยออกมาจากระนาบ z = 0 ไม่งั้นมองไม่เห็นอะไรเลย
        Vector3 position = camera.transform.position;
        if (position.z >= -1f) camera.transform.position = new Vector3(position.x, position.y, -10f);

        EditorUtility.SetDirty(camera.gameObject);
    }

    /// <summary>ลบของเดิมชื่อเดียวกันในฉากทิ้งก่อน เพื่อให้สั่งซ้ำได้โดยไม่ซ้อนกัน</summary>
    private static GameObject ReplaceSceneObject(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) Object.DestroyImmediate(existing);

        return new GameObject(name);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
