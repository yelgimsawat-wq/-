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
/// - Assets/Art/Generated/WhiteSquare.png   สี่เหลี่ยมขาวสำหรับทำพื้น
/// - Assets/Art/Generated/SpellOrb.png      ลูกกลมเรืองแสงสำหรับทำลูกเวท
/// - Assets/Prefabs/MagicCircle.prefab      Prefab วงเวท
/// - Assets/Prefabs/SpellProjectile.prefab  Prefab ลูกเวทที่พุ่งไปข้างหน้า พร้อมหาง
/// - Assets/Prefabs/Player.prefab           Prefab ผู้เล่น ใส่ครบทั้งเดินและร่ายเวท
/// - Ground ในฉาก พื้นให้ยืน เพราะตัวละครเปิดแรงโน้มถ่วง
/// - NetworkManager ในฉาก พร้อมผูก Player Prefab และ Unity Transport
/// - OnlineUI ในฉาก เมนูสร้าง/เข้าห้อง
/// - EventSystem ในฉาก เพื่อให้ระบบวาดรู้ว่าเมื่อไรนิ้วอยู่บนปุ่ม UI
/// </summary>
public static class MagicGameSetup
{
    private const string ArtFolder = "Assets/Art/Generated";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string CircleTexturePath = ArtFolder + "/MagicCircle.png";
    private const string SquareTexturePath = ArtFolder + "/WhiteSquare.png";
    private const string OrbTexturePath = ArtFolder + "/SpellOrb.png";
    private const string CapsuleTexturePath = ArtFolder + "/PlayerCapsule.png";

    // ขนาดตัวละครเป็นหน่วยโลก ใช้ร่วมกันทั้งภาพและ collider จะได้ตรงกันเป๊ะ
    private const float PlayerWidth = 1f;
    private const float PlayerHeight = 1.5f;
    private const string ProjectilePrefabPath = PrefabFolder + "/SpellProjectile.prefab";
    private const string CirclePrefabPath = PrefabFolder + "/MagicCircle.prefab";
    private const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";

    private const string SceneFolder = "Assets/Scenes";
    // ใช้ชื่อ Menu ตามที่ผู้ใช้ตั้งไว้เอง จะได้ไม่มีซีนซ้ำซ้อนสองชุด
    private const string LobbyScenePath = SceneFolder + "/Menu.unity";
    private const string GameSceneName = "Game";
    private const string GameScenePath = SceneFolder + "/" + GameSceneName + ".unity";

    private const int CircleTextureSize = 256;

    [MenuItem("Tools/เกมวาดวงเวท/ติดตั้งฉากอัตโนมัติ", priority = 0)]
    public static void SetupEverything()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder(ArtFolder);
        EnsureFolder(PrefabFolder);
        EnsureFolder(SceneFolder);

        Sprite circleSprite = CreateMagicCircleSprite();
        Sprite squareSprite = CreateSquareSprite();
        Sprite orbSprite = CreateOrbSprite();
        Sprite capsuleSprite = CreateCapsuleSprite();

        MagicCircle circlePrefab = CreateMagicCirclePrefab(circleSprite);
        SpellProjectile projectilePrefab = CreateProjectilePrefab(orbSprite);
        GameObject playerPrefab = CreatePlayerPrefab(circlePrefab, projectilePrefab, capsuleSprite);

        // สร้างซีนเกมก่อน แล้วค่อยซีนห้องรอ เพื่อให้จบด้วยการเปิดซีนห้องรอค้างไว้
        // ซึ่งเป็นซีนที่ผู้เล่นต้องกด Play จากตรงนั้น
        BuildGameScene(squareSprite);
        BuildLobbyScene(playerPrefab);

        RegisterScenesInBuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MagicGameSetup] ติดตั้งเสร็จแล้ว — แยกเป็น 2 ซีนแล้ว\n"
            + $"  {LobbyScenePath}  ห้องรอ - กด Play จากซีนนี้\n"
            + $"  {GameScenePath}  สนามรบ\n\n"
            + "Host กด 'สร้างห้อง' -> รอเพื่อนเข้า -> กด 'เริ่มเกม' ทุกคนจะถูกพาไปสนามรบพร้อมกัน\n"
            + "A/D = เดิน | ลากเมาส์ = เขียนคาถา | Space = ยืนยัน | เล็ง | Space = ยิง | Esc = ยกเลิก\n"
            + "วาดข้าง ๆ ตัว = ยิง | วาดทับตัวเอง = กางโล่\n"
            + "วงกลม=น้ำ สามเหลี่ยม=ไฟ สี่เหลี่ยม=ดิน ขีดตรง 4 ขีด=ลม"
        );
    }

    // ---------- ซีน ----------

    /// <summary>
    /// สนามรบ มีแค่พื้น กล้อง และแสง
    /// ไม่มี NetworkManager เพราะตัวนั้นอยู่ในซีนห้องรอและติดตามข้ามซีนมาเอง
    /// ถ้าใส่ไว้สองซีนจะกลายเป็นมีสองตัวชนกันตอนโหลด
    /// </summary>
    private static void BuildGameScene(Sprite squareSprite)
    {
        Scene scene = NewSceneFromTemplate();

        ConfigureCamera();
        CreateGround(squareSprite);
        CreateEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, GameScenePath);
    }

    /// <summary>
    /// ห้องรอ เป็นซีนที่เปิดตอนเริ่มเกม
    /// NetworkManager กับเมนูอยู่บน GameObject เดียวกัน เพราะ Netcode สั่ง
    /// DontDestroyOnLoad ให้ NetworkManager ตอนเริ่มทำงาน เมนูจึงติดตามไปด้วย
    /// ทำให้ยังกดออกจากห้องและดูรหัสห้องได้แม้อยู่ในสนามรบแล้ว
    /// </summary>
    private static void BuildLobbyScene(GameObject playerPrefab)
    {
        Scene scene = NewSceneFromTemplate();

        ConfigureCamera();
        CreateEventSystem();

        var go = new GameObject("NetworkManager");

        var manager = go.AddComponent<NetworkManager>();
        var transport = go.AddComponent<UnityTransport>();

        if (manager.NetworkConfig == null)
            manager.NetworkConfig = new NetworkConfig();

        manager.NetworkConfig.NetworkTransport = transport;
        // ต้องเปิด ไม่งั้น Host สั่งโหลดซีนแล้วคนอื่นไม่ตามไปด้วย
        manager.NetworkConfig.EnableSceneManagement = true;

        // ปล่อยว่างไว้โดยตั้งใจ ถ้าใส่ตรงนี้ Netcode จะสร้างตัวละครทันทีที่เชื่อมต่อ
        // ซึ่งเกิดตั้งแต่ยังอยู่หน้าเมนู แล้วจะมีแคปซูลโผล่มายืนกลางเมนู
        // ให้ PlayerSpawner เป็นคนสร้างตอนเข้าสนามรบแทน
        // (prefab ยังลงทะเบียนอยู่ใน DefaultNetworkPrefabs.asset อยู่แล้ว)
        manager.NetworkConfig.PlayerPrefab = null;

        var spawner = go.AddComponent<PlayerSpawner>();
        var spawnerSo = new SerializedObject(spawner);
        spawnerSo.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
        spawnerSo.FindProperty("gameSceneName").stringValue = GameSceneName;
        spawnerSo.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<OnlineUI2D>();

        // ไว้บนตัวเดียวกับ NetworkManager เพราะมันตามข้ามซีนไปด้วย
        // เสียงจึงตั้งค่าครั้งเดียวใช้ได้ทั้งห้องรอและสนามรบ
        go.AddComponent<SpellAudioLibrary>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, LobbyScenePath);
    }

    /// <summary>
    /// ใส่ทั้งสองซีนลง Build Settings โดยให้ห้องรออยู่ลำดับแรก
    /// Netcode โหลดได้เฉพาะซีนที่อยู่ในรายการนี้ ถ้าลืมใส่จะกด "เริ่มเกม" แล้วเงียบ
    /// </summary>
    private static void RegisterScenesInBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(LobbyScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };

        // เก็บซีนอื่นที่ผู้ใช้ใส่ไว้เองต่อท้าย ไม่ไปลบของเขาทิ้ง
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (existing.path == LobbyScenePath || existing.path == GameScenePath) continue;
            scenes.Add(existing);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
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

    /// <summary>
    /// ลูกกลมเรืองแสง ขาวล้วน สำหรับทำลูกเวท
    /// สว่างตรงกลางแล้วจางออกไปที่ขอบ เวลาย้อมสีธาตุจะดูเหมือนพลังงานจริง
    /// </summary>
    private static Sprite CreateOrbSprite()
    {
        const int size = 128;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float radius = Mathf.Sqrt(dx * dx + dy * dy) / center;

                // ยกกำลังทำให้แกนกลางทึบและขอบจางเร็ว ดูเป็นลูกไฟมากกว่าวงกลมแบน
                float alpha = Mathf.Clamp01(1f - radius);
                alpha = Mathf.Pow(alpha, 1.8f);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        File.WriteAllBytes(OrbTexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(OrbTexturePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(OrbTexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        // 256 ต่อหน่วย ทำให้ลูกเวทกว้างประมาณครึ่งหน่วย พอดีกับตัวละคร
        importer.spritePixelsPerUnit = 256f;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(OrbTexturePath);
    }

    /// <summary>
    /// แคปซูลตั้งสำหรับเป็นตัวละคร
    ///
    /// วาดจากระยะถึง "แกนกลาง" ของแคปซูล ซึ่งเป็นเส้นตรงแนวตั้งที่หดหัวท้าย
    /// เข้ามาข้างละรัศมี จุดไหนอยู่ห่างจากแกนไม่เกินรัศมีก็คือเนื้อของแคปซูล
    /// วิธีนี้ได้ปลายมนทั้งบนล่างโดยไม่ต้องวาดวงกลมสองวงมาต่อกับสี่เหลี่ยม
    ///
    /// ตั้ง Pixels Per Unit ให้ตรงกับความกว้างจริง ภาพกับ CapsuleCollider2D
    /// จึงมีขนาดเท่ากันพอดี ไม่ต้องมานั่งจูนทีหลัง
    /// </summary>
    private static Sprite CreateCapsuleSprite()
    {
        const int width = 128;
        int height = Mathf.RoundToInt(width * (PlayerHeight / PlayerWidth));

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];

        float radius = width * 0.5f;
        float centerX = radius;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // จุดบนแกนกลางที่ใกล้พิกเซลนี้ที่สุด
                float axisY = Mathf.Clamp(y, radius, height - radius);
                float dx = x - centerX;
                float dy = y - axisY;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // ไล่ขอบ 1.5 พิกเซล ให้ขอบเนียนไม่เป็นบันได
                float alpha = Mathf.Clamp01((radius - distance) / 1.5f);

                // ไล่เฉดจากบนลงล่างนิดหน่อย ให้ดูมีมิติไม่แบนสนิท
                float shade = Mathf.Lerp(1f, 0.72f, 1f - (float)y / height);

                pixels[y * width + x] = new Color(shade, shade, shade, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        File.WriteAllBytes(CapsuleTexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(CapsuleTexturePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(CapsuleTexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = width / PlayerWidth;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(CapsuleTexturePath);
    }

    /// <summary>
    /// สี่เหลี่ยมขาวล้วนสำหรับทำพื้น
    /// ตั้ง Pixels Per Unit เท่ากับขนาดภาพพอดี sprite จึงกว้าง 1 หน่วยเป๊ะ
    /// ทำให้ BoxCollider2D ที่วัดขนาดจากภาพตรงกับที่ตาเห็นเสมอแม้จะสเกลทีหลัง
    /// </summary>
    private static Sprite CreateSquareSprite()
    {
        const int size = 64;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(pixels);
        texture.Apply();

        File.WriteAllBytes(SquareTexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(SquareTexturePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(SquareTexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = size;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SquareTexturePath);
    }

    /// <summary>
    /// พื้นให้ยืน
    ///
    /// ค่าเริ่มต้นของเกมตั้งแรงโน้มถ่วงเป็น 0 จึงยังไม่จำเป็นต้องมีพื้นก็เล่นได้
    /// แต่สร้างไว้ให้เลย เผื่ออยากเปลี่ยนเป็นแนวมีพื้นให้ยืน แค่ตั้ง
    /// Gravity Scale ใน Network Player 2D เป็น 3 ก็ใช้ได้ทันที
    /// </summary>
    private static void CreateGround(Sprite squareSprite)
    {
        var go = new GameObject("Ground");

        go.transform.position = new Vector3(0f, -4f, 0f);
        go.transform.localScale = new Vector3(40f, 1.5f, 1f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = new Color(0.20f, 0.22f, 0.28f);
        renderer.sortingOrder = -10;

        // ไม่ระบุขนาดเอง ปล่อยให้วัดจาก sprite แล้วสเกลของ transform คูณให้เอง
        go.AddComponent<BoxCollider2D>();

        EditorUtility.SetDirty(go);
    }

    // ---------- Prefab ----------

    /// <summary>
    /// ลูกเวทที่พุ่งไปข้างหน้า พร้อมหางเรืองแสง
    /// TrailRenderer ต้องมีวัสดุของตัวเอง ไม่งั้นจะขึ้นเป็นสีชมพูบอกว่าหาวัสดุไม่เจอ
    /// </summary>
    private static SpellProjectile CreateProjectilePrefab(Sprite orbSprite)
    {
        var root = new GameObject("SpellProjectile");

        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = orbSprite;
        renderer.sortingOrder = 450;

        var trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.22f;
        trail.startWidth = 0.35f;
        trail.endWidth = 0f;
        trail.numCapVertices = 4;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.sortingOrder = 440;
        // ไม่ให้หางลากตามตอนที่ prefab ถูกย้ายตำแหน่งตอนเกิด
        trail.autodestruct = false;

        root.AddComponent<SpellProjectile>();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
        Object.DestroyImmediate(root);

        return saved.GetComponent<SpellProjectile>();
    }

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

    private static GameObject CreatePlayerPrefab(
        MagicCircle circlePrefab,
        SpellProjectile projectilePrefab,
        Sprite capsuleSprite)
    {
        var root = new GameObject("Player");

        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = capsuleSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 10;

        var body = root.AddComponent<Rigidbody2D>();
        body.freezeRotation = true;
        // ค่าพวกนี้ NetworkPlayer2D ตั้งทับให้เองตอนเกิดอยู่แล้ว ใส่ไว้ให้ตรงกัน
        // เพื่อไม่ให้คนที่เปิดดู Inspector สับสนว่าทำไมค่าไม่ตรงกับตอนเล่น
        body.gravityScale = 3f;
        body.linearDamping = 0f;

        // ขนาดตรงกับภาพเป๊ะ เพราะทั้งคู่คำนวณจาก PlayerWidth/PlayerHeight ชุดเดียวกัน
        // แคปซูลเหมาะกับตัวละครแนวข้างมากกว่าวงกลม เพราะไม่กลิ้งและไม่ติดขอบพื้น
        var collider = root.AddComponent<CapsuleCollider2D>();
        collider.direction = CapsuleDirection2D.Vertical;
        collider.size = new Vector2(PlayerWidth, PlayerHeight);

        root.AddComponent<NetworkObject>();
        root.AddComponent<ClientNetworkTransform2D>();
        root.AddComponent<NetworkPlayer2D>();

        var caster = root.AddComponent<SpellCaster>();
        root.AddComponent<SpellDrawing>();
        root.AddComponent<PlayerHealth>();
        root.AddComponent<SpellPower>();

        WireSpellCaster(caster, circlePrefab, projectilePrefab);
        AddVoiceChat(root);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        Object.DestroyImmediate(root);

        return saved;
    }

    /// <summary>
    /// ผูกช่องของ SpellCaster ที่เป็น private ผ่าน SerializedObject
    /// เข้าถึงตรง ๆ ไม่ได้เพราะเป็น [SerializeField] private ซึ่งถูกต้องแล้ว
    /// (ไม่ควรเปิดเป็น public แค่เพื่อให้สคริปต์ติดตั้งเขียนได้)
    /// </summary>
    /// <summary>
    /// ประกอบระบบเสียงพูดลงบนตัวละคร
    ///
    /// ทั้งบล็อกถูกครอบด้วย #if METAVC_NGO ถ้าลบแพ็กเกจ MetaVoiceChat ออก
    /// สคริปต์ติดตั้งจะยังคอมไพล์ผ่านและทำงานได้ตามปกติ แค่ไม่มีเสียงพูด
    ///
    /// ต้องอยู่บนตัวละครไม่ใช่ในฉาก เพราะเสียงต้องผูกกับเจ้าของแต่ละคน
    /// NGONetProvider เป็น NetworkBehaviour ที่ใช้ IsOwner แยกว่าใครพูดใครฟัง
    /// </summary>
    private static void AddVoiceChat(GameObject root)
    {
#if METAVC_NGO
        var metaVc = root.AddComponent<MetaVoiceChat.MetaVc>();
        var micInput = root.AddComponent<MetaVoiceChat.Input.Mic.VcMicAudioInput>();

        var voiceSource = root.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        // เสียงพูดดังจากตำแหน่งตัวละคร ใครอยู่ไกลก็ได้ยินเบาลง
        voiceSource.spatialBlend = 1f;
        voiceSource.rolloffMode = AudioRolloffMode.Linear;
        voiceSource.maxDistance = 30f;

        var output = root.AddComponent<MetaVoiceChat.Output.AudioSource.VcAudioSourceOutput>();
        output.audioSource = voiceSource;

        // ผูกอ้างอิงไปกลับให้ครบ ทั้งสองฝั่งต้องรู้จักกันไม่งั้นเสียงไม่เดิน
        metaVc.audioInput = micInput;
        metaVc.audioOutput = output;
        micInput.metaVc = metaVc;
        output.metaVc = metaVc;

        root.AddComponent<MetaVoiceChat.NetProviders.NGO.NGONetProvider>();

        // ตัวเชื่อมของเรา ต้องมาหลัง MetaVc เพราะมันไปอ่าน component นั้น
        root.AddComponent<MagicDrawing.VoiceChatPowerBridge>();
#endif
    }

    private static void WireSpellCaster(
        SpellCaster caster,
        MagicCircle circlePrefab,
        SpellProjectile projectilePrefab)
    {
        var so = new SerializedObject(caster);

        so.FindProperty("fallbackCirclePrefab").objectReferenceValue = circlePrefab;
        so.FindProperty("fallbackProjectilePrefab").objectReferenceValue = projectilePrefab;

        SerializedProperty visuals = so.FindProperty("elementVisuals");
        visuals.arraySize = 4;

        for (int i = 0; i < 4; i++)
        {
            SerializedProperty entry = visuals.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("element").enumValueIndex = i;
            entry.FindPropertyRelative("circlePrefab").objectReferenceValue = circlePrefab;
            entry.FindPropertyRelative("projectilePrefab").objectReferenceValue = projectilePrefab;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- ของในฉาก ----------

    private static void CreateEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        // โปรเจกต์ตั้ง Input System แบบใหม่ ต้องใช้โมดูลของแพ็กเกจนั้น
        // ถ้าใส่ StandaloneInputModule ตัวเก่าจะขึ้น error ตอนกด Play
        go.AddComponent<InputSystemUIInputModule>();
    }

    /// <summary>
    /// เปิดซีนเปล่าที่มีกล้องและแสงรวมของ URP 2D มาให้แล้ว
    ///
    /// ใช้ template ที่ Unity สร้างไว้ตอนตั้งโปรเจกต์แทนการประกอบเอง
    /// เพราะ Light2D อยู่ในแอสเซมบลีที่สคริปต์ฝั่ง Editor อ้างถึงตรง ๆ ไม่ได้
    /// และถ้าฉากไม่มีแสงรวม sprite ที่ใช้วัสดุแบบรับแสงจะดำสนิททั้งฉาก
    ///
    /// SaveScene ไปที่ path ใหม่ทีหลัง ตัว template จึงไม่ถูกแก้
    /// </summary>
    private static Scene NewSceneFromTemplate()
    {
        const string templatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";

        if (File.Exists(templatePath))
            return EditorSceneManager.OpenScene(templatePath, OpenSceneMode.Single);

        Debug.LogWarning(
            $"[MagicGameSetup] ไม่พบ {templatePath} จึงสร้างซีนเปล่าแทน\n"
            + "ฉากจะไม่มีแสงรวม 2D ถ้าจอมืดให้เพิ่ม Light 2D แบบ Global เองด้วย");

        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    /// <summary>
    /// ตั้งค่ากล้องที่ติดมากับ template และให้มันตามตัวละครเอง
    /// ไม่สร้างกล้องใหม่ เพราะกล้องของ template ตั้งค่าให้เข้ากับ URP 2D มาแล้ว
    /// </summary>
    private static void ConfigureCamera()
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            camera = go.AddComponent<Camera>();
        }

        camera.orthographic = true;
        camera.orthographicSize = 6f;

        // กล้อง 2D ต้องถอยออกมาจากระนาบ z = 0 ไม่งั้นมองไม่เห็นอะไรเลย
        Vector3 position = camera.transform.position;
        if (position.z >= -1f)
            camera.transform.position = new Vector3(position.x, position.y, -10f);

        // กล้องเป็นฝ่ายตามหาตัวละคร ไม่ใช่ตัวละครลากกล้องมาผูก
        // เพราะการโหลดซีนใหม่จะทำลายกล้องเก่าแต่ตัวละครย้ายข้ามซีนไปด้วย
        if (camera.GetComponent<CameraFollow2D>() == null)
            camera.gameObject.AddComponent<CameraFollow2D>();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
