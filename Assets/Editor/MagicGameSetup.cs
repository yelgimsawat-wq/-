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
using UnityEngine.UI;

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
    /// <summary>กรอบปุ่มลายมือมีสามแบบ เวียนใช้ให้ปุ่มแต่ละตัวไม่เหมือนกัน</summary>
    private const int SketchButtonVariants = 3;

    private const string SketchPanelTexturePath = ArtFolder + "/SketchPanel.png";

    private static string SketchButtonTexturePath(int variant)
    {
        return ArtFolder + "/SketchButton" + (variant + 1) + ".png";
    }

    // ขนาดตัวละครเป็นหน่วยโลก ใช้ร่วมกันทั้งภาพและ collider จะได้ตรงกันเป๊ะ
    private const float PlayerWidth = 1f;
    private const float PlayerHeight = 1.5f;
    private const string ProjectilePrefabPath = PrefabFolder + "/SpellProjectile.prefab";
    private const string CirclePrefabPath = PrefabFolder + "/MagicCircle.prefab";
    private const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";
    private const string MatchManagerPrefabPath = PrefabFolder + "/MatchManager.prefab";

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

        // ต้องสร้างก่อนสร้าง UI เพราะปุ่มทุกตัวจะไปหยิบภาพพวกนี้มาใช้
        sketchButtonSprites = new Sprite[SketchButtonVariants];
        for (int i = 0; i < SketchButtonVariants; i++)
            sketchButtonSprites[i] = CreateSketchButtonSprite(i);

        // เริ่มนับใหม่ทุกครั้งที่ติดตั้ง ปุ่มตัวเดิมจะได้แบบเดิมทุกรอบ
        sketchButtonCursor = 0;

        sketchPanelSprite = CreateSketchPanelSprite();

        MagicCircle circlePrefab = CreateMagicCirclePrefab(circleSprite);
        SpellProjectile projectilePrefab = CreateProjectilePrefab(orbSprite);
        GameObject playerPrefab = CreatePlayerPrefab(circlePrefab, projectilePrefab, capsuleSprite);
        GameObject matchManagerPrefab = CreateMatchManagerPrefab();

        // สร้างซีนเกมก่อน แล้วค่อยซีนห้องรอ เพื่อให้จบด้วยการเปิดซีนห้องรอค้างไว้
        // ซึ่งเป็นซีนที่ผู้เล่นต้องกด Play จากตรงนั้น
        BuildGameScene(squareSprite);
        BuildLobbyScene(playerPrefab, matchManagerPrefab);

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
        Scene scene = OpenOrCreateScene(GameScenePath);

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
    private static void BuildLobbyScene(GameObject playerPrefab, GameObject matchManagerPrefab)
    {
        Scene scene = OpenOrCreateScene(LobbyScenePath);

        ConfigureCamera();
        CreateEventSystem();

        // ไม่ได้เริ่มจากซีนเปล่าแล้ว ต้องลบของเดิมก่อนไม่งั้นได้สองตัวชนกัน
        DestroySceneObject("NetworkManager");
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
        spawnerSo.FindProperty("matchManagerPrefab").objectReferenceValue = matchManagerPrefab;
        spawnerSo.FindProperty("gameSceneName").stringValue = GameSceneName;
        spawnerSo.ApplyModifiedPropertiesWithoutUndo();

        var ui = go.AddComponent<OnlineUI2D>();

        // ไว้บนตัวเดียวกับ NetworkManager เพราะมันตามข้ามซีนไปด้วย
        // เสียงและเอฟเฟกต์จึงตั้งค่าครั้งเดียวใช้ได้ทั้งห้องรอและสนามรบ
        var audioLibrary = go.AddComponent<SpellAudioLibrary>();
        var vfxLibrary = go.AddComponent<SpellVfxLibrary>();
        WireAssetLibraries(audioLibrary, vfxLibrary);

        Text matchBanner = BuildMenuCanvas(ui, go.transform);

        // ป้ายประกาศผลประกาศตัวเองไว้ ให้ตัวจัดการรอบมาถามหาตอนทำงานจริง
        // ผูกล่วงหน้าไม่ได้เพราะตัวจัดการรอบมาจาก prefab ซึ่งอ้างของในฉากไม่ได้
        if (matchBanner != null && matchBanner.GetComponent<MatchBanner>() == null)
            matchBanner.gameObject.AddComponent<MatchBanner>();

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

    // ---------- ผูกไฟล์เสียงและเอฟเฟกต์ ----------

    private const string KenneyAudioFolder = "Assets/Art/Kenney/Audio";
    private const string KenneyParticleFolder = "Assets/Art/Kenney/Particles";

    /// <summary>
    /// ผูกไฟล์เสียงและภาพอนุภาคจาก Kenney (CC0) เข้ากับระบบ
    ///
    /// ไฟล์ไหนหายไปก็ข้ามไปเงียบ ๆ ระบบจะใช้เสียงสังเคราะห์แทนตัวนั้น
    /// จึงลบไฟล์ทิ้งได้โดยเกมไม่พัง แค่เสียงเปลี่ยนไป
    ///
    /// DrawLoop ไม่อยู่ในรายการโดยตั้งใจ เพราะต้องเป็นคลิปที่วนลูปได้ไร้รอยต่อ
    /// ซึ่งไฟล์สำเร็จรูปทั่วไปทำไม่ได้ ใช้เสียงที่สังเคราะห์เองต่อไป
    /// </summary>
    private static void WireAssetLibraries(SpellAudioLibrary audio, SpellVfxLibrary vfx)
    {
        var soundFiles = new (SpellSound sound, string file)[]
        {
            (SpellSound.Cast,        "spell_cast"),
            (SpellSound.Shield,      "spell_shield"),
            (SpellSound.StrokeStart, "stroke_start"),
            (SpellSound.StrokeEnd,   "stroke_end"),
            (SpellSound.Confirm,     "spell_confirm"),
            (SpellSound.Reject,      "spell_reject"),
            (SpellSound.Manifest,    "spell_manifest"),
            (SpellSound.Hit,         "hit"),
            (SpellSound.Blocked,     "hit_blocked"),
            (SpellSound.ShieldBreak, "shield_break"),
            (SpellSound.Jump,        "player_jump"),
            (SpellSound.Death,       "player_death"),
        };

        var audioSo = new SerializedObject(audio);
        SerializedProperty clips = audioSo.FindProperty("clips");
        clips.arraySize = 0;

        foreach ((SpellSound sound, string file) in soundFiles)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{KenneyAudioFolder}/{file}.ogg");
            if (clip == null) continue;

            int index = clips.arraySize;
            clips.arraySize = index + 1;

            SerializedProperty entry = clips.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("sound").enumValueIndex = (int)sound;
            entry.FindPropertyRelative("clip").objectReferenceValue = clip;
        }

        audioSo.ApplyModifiedPropertiesWithoutUndo();

        // ภาพอนุภาคเลือกให้เข้ากับธาตุ ไฟสำหรับไฟ ฝุ่นสำหรับดิน เป็นต้น
        var particleFiles = new (SpellElement element, string file)[]
        {
            (SpellElement.Water, "magic_04"),
            (SpellElement.Fire,  "flame_04"),
            (SpellElement.Earth, "dirt_02"),
            (SpellElement.Wind,  "slash_02"),
        };

        var vfxSo = new SerializedObject(vfx);
        SerializedProperty sprites = vfxSo.FindProperty("sprites");
        sprites.arraySize = 0;

        foreach ((SpellElement element, string file) in particleFiles)
        {
            Sprite sprite = LoadParticleSprite(file);
            if (sprite == null) continue;

            int index = sprites.arraySize;
            sprites.arraySize = index + 1;

            SerializedProperty entry = sprites.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("element").enumValueIndex = (int)element;
            entry.FindPropertyRelative("sprite").objectReferenceValue = sprite;
        }

        vfxSo.FindProperty("genericSprite").objectReferenceValue = LoadParticleSprite("spark_04");
        vfxSo.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// โหลดภาพอนุภาคพร้อมบังคับให้ import เป็น Sprite
    ///
    /// ไฟล์ที่เพิ่งก๊อปเข้ามาอาจถูก import เป็น Texture ธรรมดา ซึ่งเอาไปใส่
    /// SpriteRenderer ไม่ได้ ต้องสั่งเปลี่ยนก่อนแล้ว reimport
    /// </summary>
    private static Sprite LoadParticleSprite(string file)
    {
        string path = $"{KenneyParticleFolder}/{file}.png";
        if (!File.Exists(path)) return null;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            // 512 ต่อหน่วย ภาพขนาด 512px จึงกว้าง 1 หน่วย พอดีกับสเกลที่โค้ดใช้
            importer.spritePixelsPerUnit = 512f;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------- หน้าเมนู (Canvas) ----------

    private static readonly Color PanelColor = new Color(0.09f, 0.10f, 0.16f, 0.96f);
    private static readonly Color AccentColor = new Color(0.42f, 0.72f, 1f);
    // ตัวหนังสือบนพื้นขาว ใช้ InkColor แทนของเดิมที่เป็นสีอ่อน
    private static readonly Color TextColor = new Color(0.20f, 0.22f, 0.28f);
    private static readonly Color ButtonColor = new Color(0.20f, 0.24f, 0.34f);

    // สีส้มของกรอบลายมือ ตามแบบที่ผู้ใช้ให้มา อบลงในภาพเลยไม่ได้ย้อมทีหลัง
    private static readonly Color32 SketchLineColor = new Color32(251, 169, 64, 255);

    // พื้นในกรอบเป็นสีขาว ตัวหนังสือจึงต้องเป็นสีเข้มถึงจะอ่านออก
    private static readonly Color InkColor = new Color(0.20f, 0.22f, 0.28f);

    // สีหัวข้อบนพื้นขาว ใช้ส้มเข้มกว่าเส้นกรอบเล็กน้อยให้อ่านชัด
    private static readonly Color TitleColor = new Color(0.85f, 0.50f, 0.10f);

    /// <summary>
    /// สร้าง Canvas ของเมนูแล้วผูก reference เข้ากับ OnlineUI2D
    ///
    /// วางเป็นลูกของ NetworkManager เพราะ Netcode สั่ง DontDestroyOnLoad ให้
    /// object นั้น Canvas จึงข้ามซีนไปด้วย ถ้าวางแยกไว้ในซีนเมนู มันจะถูกทำลาย
    /// ตอนโหลดสนามรบ แล้วแถบย่อที่ควรโชว์รหัสห้องจะหายไป
    /// </summary>
    private static Text BuildMenuCanvas(OnlineUI2D ui, Transform parent)
    {
        DestroySceneObject("MenuCanvas");

        var canvasGo = new GameObject("MenuCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(parent, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        // สเกลตามความละเอียดจอ เมนูจึงมีสัดส่วนเท่ากันทุกจอ
        // ต่างจาก OnGUI ที่ขนาดคงที่เป็นพิกเซล จอใหญ่แล้วปุ่มจะจิ๋ว
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject profilePanel = CreateProfilePanel(canvasGo.transform, ui);
        GameObject joinPanel = CreatePanel(canvasGo.transform, "JoinPanel", new Vector2(560f, 580f));
        GameObject roomPanel = CreatePanel(canvasGo.transform, "RoomPanel", new Vector2(560f, 620f));
        CreateMicPanel(canvasGo.transform, ui);

        // เมนู Esc ต้องอยู่ท้ายสุดของแคนวาส จะได้วาดทับทุกอย่าง
        // ถ้าสร้างก่อน ของที่สร้างทีหลังจะไปวาดทับเมนูแทน
        CreatePauseMenu(canvasGo.transform, ui);

        // ---- หน้าเข้าห้อง ----
        // ทุกข้อความในหน้านี้ผูกกับตารางแปล เพราะเป็นหน้าที่มีปุ่มสลับภาษาอยู่
        // ถ้าสลับเป็นอังกฤษแล้วหน้ายังเป็นไทย ปุ่มนั้นก็แทบไม่มีประโยชน์
        AddLoc(CreateText(joinPanel.transform, "Title", "", 46, TitleColor, FontStyle.Bold), "menu.title");
        AddLoc(CreateText(joinPanel.transform, "Subtitle", "", 20, TextColor), "menu.subtitle");

        Button hostButton = CreateLocButton(joinPanel.transform, "HostButton", "menu.host");
        AddLoc(CreateText(joinPanel.transform, "OrLabel", "", 20, TextColor), "menu.or");

        InputField codeInput = CreateInput(joinPanel.transform, "CodeInput", "");
        AddLoc(codeInput.placeholder as Text, "menu.codePlaceholder");

        Button joinButton = CreateLocButton(joinPanel.transform, "JoinButton", "menu.join");
        Button editProfileButton = CreateLocButton(joinPanel.transform, "EditProfileButton", "menu.editCharacter");

        // ไมค์กับภาษาเคยเป็นสองปุ่มแยกกันบนหน้านี้ ยุบเหลือปุ่มตั้งค่าปุ่มเดียว
        // ทั้งสองอย่างคือการตั้งค่าเหมือนกัน แยกไว้ทำให้หน้าเมนูยาวโดยไม่จำเป็น
        Button settingsButton = CreateSettingsButton(joinPanel.transform);

        // ผูกตรงนี้เลย ไม่ส่งต่อไปเมธอดผูกรวม เพราะเมธอดนั้นรับพารามิเตอร์ยาวมากอยู่แล้ว
        // เพิ่มอีกตัวจะยิ่งอ่านยากและพลาดง่ายเวลาสลับลำดับ
        //
        // ช่องใน OnlineUI2D ยังชื่อ openMicButton ตามเดิม เปลี่ยนชื่อช่องไม่ได้
        // เพราะ Unity จับคู่ค่าที่ผูกไว้ในฉากด้วยชื่อช่อง เปลี่ยนแล้วของที่ผูกไว้จะหลุด
        var micButtonSo = new SerializedObject(ui);
        micButtonSo.FindProperty("openMicButton").objectReferenceValue = settingsButton;
        micButtonSo.ApplyModifiedPropertiesWithoutUndo();

        // ---- หน้าในห้อง ----
        Text roleText = CreateText(roomPanel.transform, "RoleText", "คุณเป็นเจ้าของห้อง", 26, TitleColor, FontStyle.Bold);
        CreateText(roomPanel.transform, "CodeCaption", "รหัสห้อง", 20, TextColor);

        // รหัสห้องตัวใหญ่พิเศษ เพราะเป็นข้อความที่ต้องอ่านให้เพื่อนฟังทางโทรศัพท์
        Text roomCodeText = CreateText(roomPanel.transform, "RoomCodeText", "ABCDEF", 52, TitleColor, FontStyle.Bold);

        Button copyButton = CreateButton(roomPanel.transform, "CopyButton", "คัดลอกรหัส");
        Text playersText = CreateText(roomPanel.transform, "PlayersText", "ผู้เล่นในห้อง 1 / 4 คน", 22, TextColor);
        Button startButton = CreateButton(roomPanel.transform, "StartButton", "เริ่มเกม");
        Text waitText = CreateText(roomPanel.transform, "WaitText", "รอเจ้าของห้องกดเริ่มเกม...", 22, TextColor);
        Button leaveButton = CreateButton(roomPanel.transform, "LeaveButton", "ออกจากห้อง");

        // ---- แถบย่อตอนอยู่ในสนามรบ ----
        GameObject compactPanel = CreatePanel(canvasGo.transform, "CompactPanel", new Vector2(340f, 130f));
        var compactRect = compactPanel.GetComponent<RectTransform>();
        compactRect.anchorMin = new Vector2(0f, 1f);
        compactRect.anchorMax = new Vector2(0f, 1f);
        compactRect.pivot = new Vector2(0f, 1f);
        compactRect.anchoredPosition = new Vector2(24f, -24f);

        Text compactText = CreateText(compactPanel.transform, "CompactText", "ห้อง -   ผู้เล่น 0", 20, TextColor);
        Button compactLeave = CreateButton(compactPanel.transform, "CompactLeaveButton", "ออกจากห้อง");

        // ---- แถบสถานะล่างจอ ----
        Text statusText = CreateText(canvasGo.transform, "StatusText", "", 22, Color.white);

        // แถบช่วยสอนกับหลอดวัดเสียง เคยวาดด้วย OnGUI ตอนนี้เป็นของบนแคนวาสแล้ว
        // จึงย้าย ปรับขนาด เปลี่ยนสี และแก้ข้อความได้เองจาก Inspector
        CreateHintBar(canvasGo.transform);
        CreateVoiceMeter(canvasGo.transform);
        var statusRect = statusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0f);
        statusRect.anchorMax = new Vector2(0.5f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 40f);
        statusRect.sizeDelta = new Vector2(900f, 40f);

        WireMenuUI(ui, profilePanel, editProfileButton, joinPanel, roomPanel, compactPanel, codeInput,
            hostButton, joinButton, roleText, roomCodeText, playersText, waitText,
            copyButton, startButton, leaveButton, compactText, compactLeave, statusText);

        return CreateMatchBanner(canvasGo.transform);
    }

    /// <summary>
    /// ป้ายประกาศผลกลางบนจอ ใช้บอกผู้ชนะและสถานะการดูคนอื่นตอนตกรอบ
    /// วางบนสุดเพราะเป็นข้อความสำคัญที่สุดในจอตอนจบรอบ
    /// แต่ไม่วางกลางจอเป๊ะ ๆ เพราะจะบังพื้นที่ที่ผู้เล่นใช้วาดเวท
    /// </summary>
    private static Text CreateMatchBanner(Transform canvas)
    {
        Text banner = CreateText(canvas, "MatchBanner", "", 40, AccentColor, FontStyle.Bold);

        var rect = banner.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -60f);
        rect.sizeDelta = new Vector2(1100f, 70f);

        Object.DestroyImmediate(banner.GetComponent<LayoutElement>());

        return banner;
    }

    private static void WireMenuUI(
        OnlineUI2D ui,
        GameObject profilePanel, Button editProfileButton,
        GameObject joinPanel, GameObject roomPanel, GameObject compactPanel,
        InputField codeInput, Button hostButton, Button joinButton,
        Text roleText, Text roomCodeText, Text playersText, Text waitText,
        Button copyButton, Button startButton, Button leaveButton,
        Text compactText, Button compactLeave, Text statusText)
    {
        var so = new SerializedObject(ui);

        so.FindProperty("profilePanel").objectReferenceValue = profilePanel;
        so.FindProperty("editProfileButton").objectReferenceValue = editProfileButton;
        so.FindProperty("joinPanel").objectReferenceValue = joinPanel;
        so.FindProperty("roomPanel").objectReferenceValue = roomPanel;
        so.FindProperty("compactPanel").objectReferenceValue = compactPanel;
        so.FindProperty("codeInput").objectReferenceValue = codeInput;
        so.FindProperty("hostButton").objectReferenceValue = hostButton;
        so.FindProperty("joinButton").objectReferenceValue = joinButton;
        so.FindProperty("roleText").objectReferenceValue = roleText;
        so.FindProperty("roomCodeText").objectReferenceValue = roomCodeText;
        so.FindProperty("playersText").objectReferenceValue = playersText;
        so.FindProperty("waitText").objectReferenceValue = waitText;
        so.FindProperty("copyButton").objectReferenceValue = copyButton;
        so.FindProperty("startButton").objectReferenceValue = startButton;
        so.FindProperty("leaveButton").objectReferenceValue = leaveButton;
        so.FindProperty("compactText").objectReferenceValue = compactText;
        so.FindProperty("compactLeaveButton").objectReferenceValue = compactLeave;
        so.FindProperty("statusText").objectReferenceValue = statusText;
        so.FindProperty("gameSceneName").stringValue = GameSceneName;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// หน้าตั้งชื่อและวาดตัวละคร เป็นหน้าแรกสุดก่อนเข้าห้อง
    ///
    /// ต้องมาก่อนเพราะข้อมูลตัวละครต้องพร้อมตั้งแต่ก่อนต่อเน็ต
    /// จะได้ส่งไปพร้อมตอนเกิดตัวละคร ไม่ต้องส่งกลางเกมแล้วให้คนอื่นเห็นตัวเปล่าก่อน
    /// </summary>
    private static GameObject CreateProfilePanel(Transform canvas, OnlineUI2D ui)
    {
        // จัดเป็นสองคอลัมน์: ควบคุมอยู่ซ้าย กระดานวาดอยู่ขวา
        // แยกกันแล้วกระดานได้พื้นที่เต็มความสูงของการ์ด วาดง่ายขึ้นมาก
        // และเหลือที่พอให้โชว์ตัวอย่างว่าในเกมจะออกมาหน้าตาแบบไหน
        GameObject panel = CreatePanel(canvas, "ProfilePanel", new Vector2(1280f, 900f), autoHeight: false);
        MakeHorizontal(panel, 24f);

        GameObject left = CreateColumn(panel.transform, "LeftColumn", 460f, 0f);
        GameObject right = CreateColumn(panel.transform, "RightColumn", 0f, 1f);

        // ---------- คอลัมน์ซ้าย: ชื่อ ตัวอย่าง ปุ่ม ----------

        CreateText(left.transform, "Title", "ตั้งค่าตัวละคร", 38, TitleColor, FontStyle.Bold);
        CreateText(left.transform, "NameCaption", "ชื่อของคุณ", 20, TextColor);

        InputField nameInput = CreateInput(left.transform, "NameInput", "ใส่ชื่อ");
        nameInput.characterLimit = MagicDrawing.PlayerProfile.MaxNameLength;

        CreateText(left.transform, "PreviewCaption", "ในเกมจะเห็นแบบนี้", 20, TextColor);
        RawImage previewCharacter = CreateGamePreview(left.transform, out Text previewName);

        PenWidgets pen = CreatePenTools(left.transform);

        Text sizeHint = CreateText(left.transform, "SizeHint",
            "ลากเมาส์ในกรอบขวาเพื่อวาดตัวละคร", 19, TextColor);

        Button undoButton = CreateButton(left.transform, "UndoButton", "ลบเส้นล่าสุด");
        Button clearButton = CreateButton(left.transform, "ClearButton", "ล้างทั้งหมด");
        Button confirmButton = CreateButton(left.transform, "ConfirmProfileButton", "ยืนยัน แล้วไปเข้าห้อง");

        // ---------- คอลัมน์ขวา: กระดานวาด ----------

        CreateText(right.transform, "DrawCaption", "วาดตัวละครของคุณ", 20, TextColor);

        // ตัวนอกเป็นแค่ที่จองพื้นที่ให้ layout ส่วนจัตุรัสจริงอยู่ข้างใน
        // ใช้ AspectRatioFitter บังคับให้เป็นจัตุรัสและอยู่กึ่งกลางเสมอ
        // เพราะปลายทางเป็นภาพจัตุรัส ถ้าปล่อยให้ยืดเต็มกรอบ วาดวงกลมจะได้วงรี
        var slotGo = new GameObject("DrawSlot", typeof(RectTransform));
        slotGo.transform.SetParent(right.transform, false);

        var slotElement = slotGo.AddComponent<LayoutElement>();
        // กินความสูงที่เหลือทั้งหมดของคอลัมน์ จัตุรัสจึงใหญ่ที่สุดเท่าที่การ์ดให้ได้
        slotElement.flexibleHeight = 1f;

        var areaGo = new GameObject("DrawArea", typeof(Image), typeof(AspectRatioFitter));
        areaGo.transform.SetParent(slotGo.transform, false);
        areaGo.GetComponent<Image>().color = new Color(0.13f, 0.15f, 0.22f);

        var fitter = areaGo.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1f;

        var previewGo = new GameObject("Preview", typeof(RawImage));
        previewGo.transform.SetParent(areaGo.transform, false);
        StretchToParent(previewGo.GetComponent<RectTransform>());
        var preview = previewGo.GetComponent<RawImage>();

        // ---------- ผูกทุกอย่างเข้าด้วยกัน ----------

        var pad = panel.AddComponent<MagicDrawing.ProfileDrawPad>();
        var padSo = new SerializedObject(pad);
        padSo.FindProperty("drawArea").objectReferenceValue = areaGo.GetComponent<RectTransform>();
        padSo.FindProperty("preview").objectReferenceValue = preview;
        padSo.FindProperty("sizeHint").objectReferenceValue = sizeHint;
        padSo.ApplyModifiedPropertiesWithoutUndo();

        var penControls = panel.AddComponent<MagicDrawing.PenControls>();
        var penSo = new SerializedObject(penControls);
        penSo.FindProperty("pad").objectReferenceValue = pad;
        penSo.FindProperty("sizeSlider").objectReferenceValue = pen.SizeSlider;
        penSo.FindProperty("sizeDot").objectReferenceValue = pen.SizeDot;
        SetObjectArray(penSo.FindProperty("swatchButtons"), pen.Buttons);
        SetObjectArray(penSo.FindProperty("swatchHighlights"), pen.Highlights);
        SetColorArray(penSo.FindProperty("swatchColors"), pen.Colors);
        penSo.ApplyModifiedPropertiesWithoutUndo();

        var livePreview = panel.AddComponent<MagicDrawing.ProfileCharacterPreview>();
        var liveSo = new SerializedObject(livePreview);
        liveSo.FindProperty("pad").objectReferenceValue = pad;
        liveSo.FindProperty("characterImage").objectReferenceValue = previewCharacter;
        liveSo.FindProperty("nameLabel").objectReferenceValue = previewName;
        liveSo.FindProperty("nameInput").objectReferenceValue = nameInput;
        liveSo.ApplyModifiedPropertiesWithoutUndo();

        var uiSo = new SerializedObject(ui);
        uiSo.FindProperty("nameInput").objectReferenceValue = nameInput;
        uiSo.FindProperty("drawPad").objectReferenceValue = pad;
        uiSo.FindProperty("confirmProfileButton").objectReferenceValue = confirmButton;
        uiSo.FindProperty("undoStrokeButton").objectReferenceValue = undoButton;
        uiSo.FindProperty("clearDrawingButton").objectReferenceValue = clearButton;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    /// <summary>เปลี่ยนการ์ดจากเรียงลงล่างเป็นเรียงข้าง</summary>
    private static void MakeHorizontal(GameObject panel, float spacing)
    {
        var oldLayout = panel.GetComponent<VerticalLayoutGroup>();
        RectOffset padding = oldLayout != null ? oldLayout.padding : new RectOffset(28, 28, 24, 24);
        if (oldLayout != null) Object.DestroyImmediate(oldLayout);

        var layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.padding = padding;
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        // ความกว้างคุมเองผ่าน LayoutElement ของแต่ละคอลัมน์
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
    }

    /// <summary>คอลัมน์หนึ่งช่องในการ์ด เรียงของลงล่างตามปกติ</summary>
    private static GameObject CreateColumn(Transform parent, string name, float width, float flexible)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var element = go.AddComponent<LayoutElement>();
        if (width > 0f) element.preferredWidth = width;
        element.flexibleWidth = flexible;

        return go;
    }

    /// <summary>
    /// กรอบตัวอย่างที่จำลองหน้าตาในสนามรบ ป้ายชื่อเหนือหัวและตัวละครยืนบนพื้น
    /// ไม่ได้จำลองฉากจริง แค่ให้เห็นสัดส่วนและชื่อคู่กันว่าอ่านออกไหม
    /// </summary>
    private static RawImage CreateGamePreview(Transform parent, out Text nameLabel)
    {
        var frame = new GameObject("GamePreview", typeof(Image));
        frame.transform.SetParent(parent, false);
        frame.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.17f);

        var element = frame.AddComponent<LayoutElement>();
        element.minHeight = 220f;
        element.preferredHeight = 220f;

        // ป้ายชื่อติดขอบบน
        nameLabel = CreateText(frame.transform, "NameLabel", "ผู้เล่น", 22, AccentColor, FontStyle.Bold);
        var nameRect = nameLabel.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.sizeDelta = new Vector2(0f, 32f);
        nameRect.anchoredPosition = new Vector2(0f, -12f);

        // เส้นพื้นให้รู้ว่าตัวละครยืนอยู่ตรงไหน
        var ground = new GameObject("GroundLine", typeof(Image));
        ground.transform.SetParent(frame.transform, false);
        ground.GetComponent<Image>().color = new Color(0.30f, 0.34f, 0.42f);

        var groundRect = ground.GetComponent<RectTransform>();
        groundRect.anchorMin = new Vector2(0.12f, 0f);
        groundRect.anchorMax = new Vector2(0.88f, 0f);
        groundRect.pivot = new Vector2(0.5f, 0f);
        groundRect.sizeDelta = new Vector2(0f, 3f);
        groundRect.anchoredPosition = new Vector2(0f, 22f);

        // ตัวละครยืนบนเส้นพื้น เป็นจัตุรัสเหมือนภาพที่ใช้ในเกมจริง
        var characterGo = new GameObject("Character", typeof(RawImage));
        characterGo.transform.SetParent(frame.transform, false);

        var characterRect = characterGo.GetComponent<RectTransform>();
        characterRect.anchorMin = new Vector2(0.5f, 0f);
        characterRect.anchorMax = new Vector2(0.5f, 0f);
        characterRect.pivot = new Vector2(0.5f, 0f);
        characterRect.sizeDelta = new Vector2(150f, 150f);
        characterRect.anchoredPosition = new Vector2(0f, 25f);

        return characterGo.GetComponent<RawImage>();
    }

    /// <summary>
    /// การ์ดกลางจอ ใช้ VerticalLayoutGroup เรียงของข้างในให้อัตโนมัติ
    /// จะได้ไม่ต้องมานั่งคำนวณตำแหน่งทีละชิ้น และเพิ่ม/ลบชิ้นได้โดยไม่ต้องขยับของอื่น
    /// </summary>
    private static GameObject CreatePanel(Transform parent, string name, Vector2 size, bool autoHeight = true)
    {
        var go = new GameObject(name, typeof(Image), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);

        var background = go.GetComponent<Image>();
        Sprite panelFrame = SketchPanelSprite;

        if (panelFrame != null)
        {
            background.sprite = panelFrame;
            background.type = Image.Type.Sliced;
            // ภาพอบสีมาแล้วทั้งพื้นขาวและเส้นส้ม จึงไม่ย้อมทับ
            background.color = Color.white;
        }
        else
        {
            background.color = PanelColor;
        }

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        var layout = go.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;

        // ต้องคุมความสูงของลูกด้วย ไม่ใช่แค่ความกว้าง
        //
        // ถ้าไม่คุม ทุกชิ้นจะสูง 100 ตามค่าเริ่มต้นของ RectTransform
        // ทั้งที่ปุ่มขอไว้ 56 ผลคือปุ่มบวมเกินจริง ทับกันเอง และรวมแล้วยาว
        // เกินพื้นหลังการ์ดจนของล้นออกไปนอกกรอบ
        layout.childControlHeight = true;

        if (autoHeight)
        {
            // ให้การ์ดสูงตามเนื้อหาจริง ไม่ใช่ตั้งความสูงตายตัวไว้
            // ถ้าตั้งตายตัวแล้วเพิ่มปุ่มทีหลัง ของจะล้นออกนอกกรอบโดยไม่มีอะไรเตือน
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        return go;
    }

    private static Font editorFont;

    /// <summary>
    /// ฟอนต์ที่ใส่ให้ข้อความทุกตัวตอนสร้าง
    /// อ้างที่อยู่จาก GameFont ที่เดียว ย้ายไฟล์ฟอนต์แล้วแก้จุดเดียวจบ
    /// </summary>
    private static Font EditorFont
    {
        get
        {
            if (editorFont == null)
                editorFont = AssetDatabase.LoadAssetAtPath<Font>(MagicDrawing.GameFont.AssetPath);

            return editorFont;
        }
    }

    private static Text CreateText(
        Transform parent, string name, string content, int fontSize, Color color,
        FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // ใส่ฟอนต์ตั้งแต่ตอนสร้าง จะได้เห็นตัวหนังสือถูกต้องใน Scene view
        // ไม่ต้องกด Play ก่อนถึงจะรู้ว่าจัดวางพอดีไหม
        // (ตอนรัน OnlineUI2D ยังแปะซ้ำให้อีกที เผื่อข้อความที่สร้างทีหลัง)
        Font font = EditorFont;
        if (font != null) text.font = font;

        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, fontSize + 14f);

        var element = go.AddComponent<LayoutElement>();
        element.minHeight = fontSize + 14f;
        element.preferredHeight = fontSize + 14f;

        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();

        Sprite frame = NextSketchButtonSprite();
        if (frame != null)
        {
            image.sprite = frame;
            // 9-slice ทำให้ปุ่มยืดหดได้ทุกขนาดโดยมุมไม่บิด
            image.type = Image.Type.Sliced;
            // ภาพอบสีมาแล้วทั้งพื้นขาวและเส้นส้ม จึงไม่ย้อมทับ
            image.color = Color.white;
        }
        else
        {
            // หาไฟล์ภาพไม่เจอ ยังต้องเห็นปุ่มอยู่ ไม่ใช่กรอบใส ๆ ที่กดไม่ถูก
            image.color = ButtonColor;
        }

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        // สีตอนชี้และตอนกดต่างจากปกติชัดเจน ผู้เล่นจะได้รู้ว่ากดติดแล้ว
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        button.colors = colors;

        var element = go.AddComponent<LayoutElement>();
        element.minHeight = 56f;
        element.preferredHeight = 56f;

        Text text = CreateText(go.transform, "Label", label, 22, InkColor);
        StretchToParent(text.GetComponent<RectTransform>());
        Object.DestroyImmediate(text.GetComponent<LayoutElement>());

        return button;
    }

    private static InputField CreateInput(Transform parent, string name, string placeholder)
    {
        var go = new GameObject(name, typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);

        // ใช้กรอบลายมือแบบเดียวกับปุ่ม ไม่งั้นช่องกรอกจะเป็นสี่เหลี่ยมทึบสีเข้ม
        // อันเดียวที่ขัดกับของอื่นทั้งหน้า
        var background = go.GetComponent<Image>();
        Sprite frame = NextSketchButtonSprite();

        if (frame != null)
        {
            background.sprite = frame;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
        }
        else
        {
            background.color = new Color(0.16f, 0.18f, 0.26f);
        }

        var element = go.AddComponent<LayoutElement>();
        element.minHeight = 60f;
        element.preferredHeight = 60f;

        // พื้นขาวแล้ว ตัวหนังสือต้องเข้ม ตัวบอกใบ้จางกว่าค่าที่พิมพ์จริง
        Text placeholderText = CreateText(go.transform, "Placeholder", placeholder, 24,
            new Color(0.55f, 0.57f, 0.62f));
        Text valueText = CreateText(go.transform, "Text", "", 28, InkColor, FontStyle.Bold);

        foreach (Text t in new[] { placeholderText, valueText })
        {
            StretchToParent(t.GetComponent<RectTransform>());
            Object.DestroyImmediate(t.GetComponent<LayoutElement>());
        }

        // InputField ต้องการ Text ที่ไม่ตัดบรรทัด ไม่งั้นตัวอักษรจะหายตอนพิมพ์ยาว
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        valueText.supportRichText = false;

        var input = go.GetComponent<InputField>();
        input.textComponent = valueText;
        input.placeholder = placeholderText;
        input.characterLimit = 10;

        return input;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 6f);
        rect.offsetMax = new Vector2(-12f, -6f);
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
        // ถ้าในฉากมีพื้นให้ยืนอยู่แล้ว ไม่ต้องสร้างเพิ่ม
        //
        // ผู้เล่นที่สร้างแมพเองมีพื้นของตัวเองอยู่แล้ว ถ้าสร้างซ้ำทุกครั้งที่ติดตั้ง
        // จะได้แท่งเทา ๆ พาดกลางแมพเพิ่มขึ้นเรื่อย ๆ
        // แท่งพื้นนี้มีไว้สำหรับฉากเปล่าเท่านั้น จะได้มีที่ยืนตั้งแต่กด Play ครั้งแรก
        if (Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None).Length > 0)
            return;

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
        // ต้องใช้วัสดุที่เป็นไฟล์จริงในโปรเจกต์ ห้ามใช้ new Material(...)
        //
        // วัสดุที่สร้างด้วย new อยู่แค่ในหน่วยความจำ ไม่มีไฟล์รองรับ
        // พอ SaveAsPrefabAsset เซฟ prefab อ้างอิงจึงหลุดเป็นค่าว่าง
        // แล้ว Unity วาด renderer ที่ไม่มีวัสดุเป็นสีม่วงบานเย็น
        //
        // Sprites-Default เป็นวัสดุที่ Unity ติดมาให้ ตัวเดียวกับที่ SpriteRenderer
        // ใช้อยู่แล้ว จึงเข้ากับ URP และอ้างอิงได้ถาวร
        trail.material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
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

    /// <summary>
    /// Prefab ตัวจัดการรอบ ต้องมี NetworkObject คู่กับ MatchManager เสมอ
    ///
    /// เคยแปะ MatchManager ไว้บน GameObject ของ NetworkManager ตรง ๆ ซึ่งใช้ไม่ได้
    /// เพราะ NetworkManager ห้ามมี NetworkObject อยู่ด้วย NetworkBehaviour ตัวนั้น
    /// จึงไม่เคย spawn และ IsServer เป็น false ตลอด ระบบแพ้ชนะเลยเงียบสนิท
    /// </summary>
    /// <summary>
    /// ใส่ prefab ลงรายชื่อที่ Netcode รู้จัก
    ///
    /// Netcode ต้องรู้จัก prefab ล่วงหน้าถึงจะ spawn ข้ามเครื่องได้
    /// ปกติมันเติมให้เองตอนสร้าง prefab ที่มี NetworkObject แต่การเติมอัตโนมัติ
    /// ปิดได้ในตั้งค่าโปรเจกต์ จึงเติมซ้ำตรงนี้ให้แน่ใจ ถ้ามีอยู่แล้วก็ข้าม
    /// </summary>
    private static void RegisterNetworkPrefab(GameObject prefab)
    {
        const string listPath = "Assets/DefaultNetworkPrefabs.asset";

        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
        if (list == null || prefab == null) return;

        // PrefabList อ่านได้อย่างเดียว ต้องเพิ่มผ่านเมธอดของตัวมันเอง
        if (list.Contains(prefab)) return;

        list.Add(new NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(list);
    }

    private static GameObject CreateMatchManagerPrefab()
    {
        var root = new GameObject("MatchManager");
        root.AddComponent<NetworkObject>();
        root.AddComponent<MatchManager>();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, MatchManagerPrefabPath);
        Object.DestroyImmediate(root);

        RegisterNetworkPrefab(saved);
        return saved;
    }

    private static GameObject CreatePlayerPrefab(
        MagicCircle circlePrefab,
        SpellProjectile projectilePrefab,
        Sprite capsuleSprite)
    {
        var root = new GameObject("Player");

        // ภาพอยู่บนลูก ไม่ใช่บนตัวหลัก เพราะอนิเมชันหมุนและย่อเฉพาะภาพ
        // ถ้าหมุนตัวหลัก collider จะหมุนตามแล้วตัวละครจะจมพื้นหรือลอยขึ้นเอง
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = capsuleSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 10;

        visual.AddComponent<CharacterAnimator2D>();

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

        // ชื่อและรูปที่ผู้เล่นวาดไว้ในเมนู ต้องรู้ว่าจะเอารูปไปใส่ตัวไหน
        var appearance = root.AddComponent<PlayerAppearance>();
        var appearanceSo = new SerializedObject(appearance);
        appearanceSo.FindProperty("targetRenderer").objectReferenceValue = renderer;
        appearanceSo.FindProperty("worldSize").floatValue = PlayerHeight;
        appearanceSo.ApplyModifiedPropertiesWithoutUndo();

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
        // Unity รองรับ EventSystem ได้ฉากละตัวเดียว มีหลายตัวแล้วมันจะเตือน
        // และปิดตัวที่เกินทิ้ง ทำให้การกดปุ่มเดาไม่ถูกว่าตัวไหนกำลังทำงาน
        //
        // ต้องเช็คก่อนสร้าง เพราะคำสั่งติดตั้งฉากถูกสั่งซ้ำได้หลายรอบ
        // รอบก่อนหน้าสร้างไว้แล้วรอบนี้สร้างอีกก็จะกองกันเรื่อย ๆ
        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;

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
    /// <summary>
    /// เปิดซีนที่มีอยู่แล้ว ถ้ายังไม่มีค่อยสร้างใหม่จากแม่แบบ
    ///
    /// สำคัญมาก ห้ามสร้างซีนใหม่ทับของเดิมเด็ดขาด
    /// เดิมสคริปต์นี้เปิดแม่แบบเปล่าแล้วเซฟทับ ทำให้ของที่ผู้ใช้วางไว้เอง
    /// เช่น แมพที่สร้างมาทั้งวัน หายทั้งหมดโดยไม่มีทางกู้ถ้ายังไม่ได้ commit
    ///
    /// สคริปต์นี้เป็นเจ้าของแค่ของที่ตัวเองสร้าง (Ground, NetworkManager, MenuCanvas,
    /// EventSystem) ของพวกนั้นเขียนทับได้ ที่เหลือในฉากต้องไม่ไปยุ่ง
    /// </summary>
    private static Scene OpenOrCreateScene(string scenePath)
    {
        if (File.Exists(scenePath))
        {
            Scene existing = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            int kept = existing.GetRootGameObjects().Length;
            if (kept > 0)
                Debug.Log($"[MagicGameSetup] เปิดซีนเดิม {scenePath} ของที่มีอยู่ {kept} ชิ้นถูกเก็บไว้");

            return existing;
        }

        return NewSceneFromTemplate();
    }

    /// <summary>ลบของเดิมชื่อเดียวกันทิ้ง ใช้ก่อนสร้างใหม่ กันของซ้อนกันตอนสั่งซ้ำ</summary>
    private static void DestroySceneObject(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) Object.DestroyImmediate(existing);
    }

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
    /// <summary>ของที่สร้างไว้สำหรับแถบเครื่องมือปากกา รอผูกเข้ากับกระดานทีหลัง</summary>
    private struct PenWidgets
    {
        public Slider SizeSlider;
        public RectTransform SizeDot;
        public Button[] Buttons;
        public Color[] Colors;
        public Image[] Highlights;
    }

    /// <summary>
    /// แถบเครื่องมือปากกา: แถบเลื่อนขนาด กับปุ่มเลือกสี
    ///
    /// สร้างของก่อน แล้วค่อยผูกกับกระดานทีหลัง เพราะกระดานถูกสร้างหลังจากนี้
    /// </summary>
    private static PenWidgets CreatePenTools(Transform parent)
    {
        CreateText(parent, "PenSizeCaption", "ขนาดปากกา", 20, TextColor);

        // แถวเดียวมีทั้งแถบเลื่อนและจุดตัวอย่าง จะได้เห็นผลทันทีที่เลื่อน
        var row = new GameObject("PenSizeRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 14f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        var rowElement = row.AddComponent<LayoutElement>();
        rowElement.minHeight = 44f;
        rowElement.preferredHeight = 44f;

        Slider slider = CreateSlider(row.transform, "PenSizeSlider");
        var sliderElement = slider.gameObject.AddComponent<LayoutElement>();
        sliderElement.flexibleWidth = 1f;
        sliderElement.minHeight = 24f;

        // กรอบจุดตัวอย่าง ขนาดคงที่ ส่วนจุดข้างในโตตามขนาดปากกา
        var dotFrame = new GameObject("PenSizeDotFrame", typeof(RectTransform));
        dotFrame.transform.SetParent(row.transform, false);

        var dotFrameElement = dotFrame.AddComponent<LayoutElement>();
        dotFrameElement.minWidth = 40f;
        dotFrameElement.preferredWidth = 40f;
        dotFrameElement.minHeight = 40f;

        var dot = new GameObject("PenSizeDot", typeof(Image));
        dot.transform.SetParent(dotFrame.transform, false);
        dot.GetComponent<Image>().color = TextColor;

        var dotRect = dot.GetComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = Vector2.zero;
        dotRect.sizeDelta = new Vector2(12f, 12f);

        CreateText(parent, "PenColorCaption", "สีปากกา", 20, TextColor);

        Color[] colors =
        {
            Color.white,
            new Color(0.20f, 0.22f, 0.28f),   // เกือบดำ ใช้ตัดเส้น
            new Color(1.00f, 0.35f, 0.35f),   // แดง
            new Color(1.00f, 0.65f, 0.25f),   // ส้ม
            new Color(1.00f, 0.90f, 0.35f),   // เหลือง
            new Color(0.45f, 0.85f, 0.45f),   // เขียว
            new Color(0.35f, 0.65f, 1.00f),   // ฟ้า
            new Color(0.75f, 0.50f, 1.00f),   // ม่วง
        };

        var swatchRow = new GameObject("PenColorRow", typeof(RectTransform));
        swatchRow.transform.SetParent(parent, false);

        var swatchLayout = swatchRow.AddComponent<HorizontalLayoutGroup>();
        swatchLayout.spacing = 8f;
        swatchLayout.childAlignment = TextAnchor.MiddleCenter;
        swatchLayout.childControlWidth = true;
        swatchLayout.childControlHeight = true;
        swatchLayout.childForceExpandWidth = true;
        swatchLayout.childForceExpandHeight = true;

        var swatchElement = swatchRow.AddComponent<LayoutElement>();
        swatchElement.minHeight = 44f;
        swatchElement.preferredHeight = 44f;

        var buttons = new Button[colors.Length];
        var highlights = new Image[colors.Length];

        for (int i = 0; i < colors.Length; i++)
        {
            // กรอบเลือกอยู่ชั้นนอก ตัวสีอยู่ชั้นใน กรอบจึงโผล่รอบ ๆ ได้
            var frame = new GameObject($"Swatch{i}", typeof(Image), typeof(Button));
            frame.transform.SetParent(swatchRow.transform, false);

            var highlight = frame.GetComponent<Image>();
            highlight.color = AccentColor;
            highlight.enabled = false;

            var swatch = new GameObject("Color", typeof(Image));
            swatch.transform.SetParent(frame.transform, false);
            swatch.GetComponent<Image>().color = colors[i];

            RectTransform swatchRect = swatch.GetComponent<RectTransform>();
            StretchToParent(swatchRect);
            // เว้นขอบไว้ 4 พิกเซล ให้กรอบที่อยู่ข้างหลังโผล่ออกมาเห็นได้
            swatchRect.offsetMin = new Vector2(4f, 4f);
            swatchRect.offsetMax = new Vector2(-4f, -4f);

            var button = frame.GetComponent<Button>();
            // ให้ปุ่มไฮไลต์ตัวสี ไม่ใช่กรอบ ไม่งั้นกดแล้วกรอบกะพริบ
            button.targetGraphic = swatch.GetComponent<Image>();

            buttons[i] = button;
            highlights[i] = highlight;
        }

        return new PenWidgets
        {
            SizeSlider = slider,
            SizeDot = dotRect,
            Buttons = buttons,
            Colors = colors,
            Highlights = highlights,
        };
    }

    /// <summary>
    /// แถบเลื่อนแบบพื้นฐาน Unity ไม่มีตัวช่วยสร้างจากโค้ด ต้องประกอบเองทั้งชุด
    /// โครงสร้างที่ Slider ต้องการคือ พื้นหลัง แถบที่เติม และหมุดลาก
    /// </summary>
    private static Slider CreateSlider(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);

        var background = new GameObject("Background", typeof(Image));
        background.transform.SetParent(go.transform, false);
        background.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.25f);
        StretchToParent(background.GetComponent<RectTransform>());

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        StretchToParent(fillAreaRect);
        // เว้นที่ให้หมุดไม่ให้แถบล้นออกนอกปลายทั้งสองข้าง
        fillAreaRect.offsetMin = new Vector2(10f, 0f);
        fillAreaRect.offsetMax = new Vector2(-10f, 0f);

        var fill = new GameObject("Fill", typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = AccentColor;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        StretchToParent(fillRect);
        fillRect.sizeDelta = new Vector2(10f, 0f);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        StretchToParent(handleAreaRect);
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        var handle = new GameObject("Handle", typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<Image>().color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 0f);

        var slider = go.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;

        return slider;
    }

    private static void SetObjectArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void SetColorArray(SerializedProperty property, Color[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).colorValue = values[i];
    }

    private static Sprite[] sketchButtonSprites;
    private static int sketchButtonCursor;
    private static Sprite sketchPanelSprite;

    /// <summary>กรอบการ์ดลายมือ ใช้ใบเดียวกันทุกการ์ด</summary>
    private static Sprite SketchPanelSprite
    {
        get
        {
            if (sketchPanelSprite == null)
                sketchPanelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SketchPanelTexturePath);

            return sketchPanelSprite;
        }
    }

    /// <summary>
    /// หยิบกรอบปุ่มแบบถัดไป เวียนไปเรื่อย ๆ
    /// ปุ่มที่อยู่ติดกันจึงไม่ซ้ำแบบกัน ดูเหมือนวาดทีละอันจริง ๆ
    /// </summary>
    private static Sprite NextSketchButtonSprite()
    {
        if (sketchButtonSprites == null)
        {
            sketchButtonSprites = new Sprite[SketchButtonVariants];
            for (int i = 0; i < SketchButtonVariants; i++)
                sketchButtonSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(SketchButtonTexturePath(i));
        }

        for (int attempt = 0; attempt < SketchButtonVariants; attempt++)
        {
            Sprite candidate = sketchButtonSprites[sketchButtonCursor % SketchButtonVariants];
            sketchButtonCursor++;
            if (candidate != null) return candidate;
        }

        return null;
    }

    /// <summary>
    /// กรอบปุ่มลายมือ เส้นสั่น ๆ สองรอบทับกัน เข้ากับฟอนต์ Itim ที่เป็นลายมือ
    ///
    /// วาดด้วยโค้ดแทนการใช้ไฟล์ภาพ เพราะ
    /// 1. ปรับความหนา ความสั่น และขนาดมุมได้ที่เดียวโดยไม่ต้องวาดใหม่
    /// 2. วาดเป็นสีขาวล้วน จึงย้อมเป็นสีอะไรก็ได้ตอนใช้งาน
    /// 3. ตั้งขอบ 9-slice ให้พอดีกับมุมได้เป๊ะ เพราะเรารู้รัศมีมุมอยู่แล้ว
    ///
    /// 9-slice ทำให้ปุ่มยืดหดได้ทุกขนาดโดยมุมไม่บิด ตรงกลางจะยืดตามความกว้าง
    /// ความสั่นของเส้นตรงกลางจึงถูกยืดตามไปด้วย ซึ่งยอมรับได้สำหรับสไตล์ลายมือ
    /// </summary>
    private static Sprite CreateSketchButtonSprite(int variant)
    {

        // แต่ละแบบใช้ระยะห่างระหว่างสองเส้น ความสั่น และจังหวะคลื่นต่างกัน
        // ผลคือกรอบสามแบบที่ดูเป็นลายมือคนเดียวกัน แต่ไม่ใช่อันเดียวกัน
        float[] innerInset = { 5.5f, 5.0f, 6.5f };
        float[] outerInset = { 8.5f, 10.0f, 9.0f };
        float[] innerWobble = { 2.4f, 1.9f, 2.8f };
        float[] outerWobble = { 1.8f, 2.6f, 2.1f };
        float[] innerPhase = { 0.7f, 2.3f, 4.6f };
        float[] outerPhase = { 3.9f, 5.1f, 1.4f };

        int v = Mathf.Clamp(variant, 0, SketchButtonVariants - 1);

        return BakeSketchFrame(
            SketchButtonTexturePath(variant),
            256, 96, 34f, 44, 2.2f,
            innerInset[v], outerInset[v],
            innerWobble[v], outerWobble[v],
            innerPhase[v], outerPhase[v]);
    }

    /// <summary>
    /// กรอบการ์ดลายมือ สูงกว่าปุ่มมาก จึงต้องใช้ภาพคนละใบ
    /// ถ้ายืดภาพปุ่มมาใช้ ความสั่นของเส้นจะถูกยืดจนดูเป็นคลื่นยาว ๆ ไม่เหมือนลายมือ
    /// </summary>
    private static Sprite CreateSketchPanelSprite()
    {
        return BakeSketchFrame(
            SketchPanelTexturePath,
            256, 320, 42f, 58, 2.6f,
            6.0f, 10.0f,
            2.6f, 2.0f,
            1.9f, 4.7f);
    }

    /// <summary>
    /// อบกรอบลายมือหนึ่งใบ พื้นในเป็นสีขาวทึบ ขอบเป็นเส้นส้มลากสองรอบ
    ///
    /// อบสีลงในภาพเลยแทนการวาดขาวล้วนแล้วย้อมทีหลัง เพราะพื้นกับเส้นต้องคนละสี
    /// ถ้าย้อมทั้งใบจะได้สีเดียวกันหมด
    /// </summary>
    private static Sprite BakeSketchFrame(
        string path, int width, int height, float cornerRadius, int border, float lineRadius,
        float innerInset, float outerInset,
        float innerWobble, float outerWobble,
        float innerPhase, float outerPhase)
    {
        var pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

        // เติมพื้นขาวก่อน แล้วค่อยลากเส้นทับ ลำดับสลับกันไม่ได้
        // ไม่งั้นพื้นจะกลบเส้นที่ลากไว้
        FillRoundedRect(pixels, width, height, cornerRadius, innerInset + 1f,
            new Color32(255, 255, 255, 255));

        DrawWobblyRoundedRect(pixels, width, height, cornerRadius,
            innerInset, lineRadius, innerWobble, innerPhase);
        DrawWobblyRoundedRect(pixels, width, height, cornerRadius,
            outerInset, lineRadius, outerWobble, outerPhase);

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();

        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        // ขอบ 9-slice กว้างกว่ารัศมีมุม มุมจึงไม่โดนยืด
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// เติมสี่เหลี่ยมมุมมนทึบ ใช้เป็นพื้นหลังใต้กรอบลายมือ
    ///
    /// วัดระยะจากขอบรูปด้วยสูตรระยะทางแทนการไล่เช็คทีละด้าน
    /// ได้ขอบที่เนียนโดยไม่ต้องแยกกรณีมุมกับด้านตรง
    /// </summary>
    private static void FillRoundedRect(
        Color32[] pixels, int width, int height, float cornerRadius, float inset, Color32 color)
    {
        float halfWidth = width * 0.5f - inset;
        float halfHeight = height * 0.5f - inset;
        float radius = Mathf.Min(cornerRadius, Mathf.Min(halfWidth, halfHeight));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - width * 0.5f) - (halfWidth - radius);
                float dy = Mathf.Abs(y + 0.5f - height * 0.5f) - (halfHeight - radius);

                float outsideX = Mathf.Max(dx, 0f);
                float outsideY = Mathf.Max(dy, 0f);
                float distance = Mathf.Min(Mathf.Max(dx, dy), 0f)
                    + Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY)
                    - radius;

                // ไล่ขอบหนึ่งพิกเซล ให้ไม่เป็นบันได
                float alpha = Mathf.Clamp01(0.5f - distance);
                if (alpha <= 0f) continue;

                pixels[y * width + x] = new Color32(color.r, color.g, color.b, (byte)(alpha * 255f));
            }
        }
    }

    /// <summary>
    /// วาดกรอบมนหนึ่งรอบ โดยเลื่อนแต่ละจุดเข้าออกตามคลื่นเพื่อให้เส้นดูสั่นแบบลายมือ
    ///
    /// ใช้ผลรวมของคลื่นสองความถี่ที่เป็นจำนวนเต็มรอบ เส้นจึงบรรจบกันพอดี
    /// ถ้าใช้ค่าสุ่มธรรมดา จุดเริ่มกับจุดจบจะไม่ตรงกันแล้วเห็นเป็นรอยต่อ
    /// </summary>
    private static void DrawWobblyRoundedRect(
        Color32[] pixels, int width, int height,
        float cornerRadius, float inset, float lineRadius,
        float wobble, float phase)
    {
        float left = inset;
        float right = width - inset;
        float bottom = inset;
        float top = height - inset;

        float radius = Mathf.Min(cornerRadius, Mathf.Min(right - left, top - bottom) * 0.5f);

        const int steps = 360;
        var points = new Vector2[steps];

        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            Vector2 point = RoundedRectPoint(left, right, bottom, top, radius, t, out Vector2 normal);

            // สองความถี่ทำให้ดูเป็นธรรมชาติกว่าคลื่นเดียว ไม่เป็นระเบียบเกินไป
            float offset =
                Mathf.Sin(t * Mathf.PI * 2f * 3f + phase) * wobble +
                Mathf.Sin(t * Mathf.PI * 2f * 7f + phase * 1.7f) * wobble * 0.45f;

            points[i] = point + normal * offset;
        }

        for (int i = 0; i < steps; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % steps];
            StampLine(pixels, width, height, a, b, lineRadius, SketchLineColor);
        }
    }

    /// <summary>
    /// จุดบนเส้นรอบรูปสี่เหลี่ยมมุมมน ที่ระยะ t (0..1) รอบเส้นรอบรูป
    /// คืนทิศตั้งฉากออกด้านนอกมาด้วย เอาไว้เลื่อนจุดให้เส้นสั่น
    ///
    /// เดินตามเส้นรอบรูปจริงตามสัดส่วนความยาว ไม่ใช่แบ่งเท่า ๆ กันสี่ด้าน
    /// ไม่งั้นด้านสั้นจะมีจุดกระจุกจนเส้นหนากว่าด้านยาว
    /// </summary>
    private static Vector2 RoundedRectPoint(
        float left, float right, float bottom, float top, float radius, float t, out Vector2 normal)
    {
        float straightX = Mathf.Max(0f, (right - left) - radius * 2f);
        float straightY = Mathf.Max(0f, (top - bottom) - radius * 2f);
        float arc = radius * Mathf.PI * 0.5f;

        float perimeter = straightX * 2f + straightY * 2f + arc * 4f;
        float distance = t * perimeter;

        // ไล่ทีละช่วง เริ่มจากกลางขอบล่างวนทวนเข็ม
        float cursor = distance;

        // ขอบล่าง ครึ่งหลัง
        float half = straightX * 0.5f;
        if (cursor < half)
        {
            normal = Vector2.down;
            return new Vector2(left + radius + half + cursor, bottom);
        }
        cursor -= half;

        if (cursor < arc)
        {
            float angle = Mathf.PI * 1.5f + (cursor / arc) * Mathf.PI * 0.5f;
            normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return new Vector2(right - radius, bottom + radius) + normal * radius;
        }
        cursor -= arc;

        if (cursor < straightY)
        {
            normal = Vector2.right;
            return new Vector2(right, bottom + radius + cursor);
        }
        cursor -= straightY;

        if (cursor < arc)
        {
            float angle = (cursor / arc) * Mathf.PI * 0.5f;
            normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return new Vector2(right - radius, top - radius) + normal * radius;
        }
        cursor -= arc;

        if (cursor < straightX)
        {
            normal = Vector2.up;
            return new Vector2(right - radius - cursor, top);
        }
        cursor -= straightX;

        if (cursor < arc)
        {
            float angle = Mathf.PI * 0.5f + (cursor / arc) * Mathf.PI * 0.5f;
            normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return new Vector2(left + radius, top - radius) + normal * radius;
        }
        cursor -= arc;

        if (cursor < straightY)
        {
            normal = Vector2.left;
            return new Vector2(left, top - radius - cursor);
        }
        cursor -= straightY;

        if (cursor < arc)
        {
            float angle = Mathf.PI + (cursor / arc) * Mathf.PI * 0.5f;
            normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return new Vector2(left + radius, bottom + radius) + normal * radius;
        }
        cursor -= arc;

        normal = Vector2.down;
        return new Vector2(left + radius + cursor, bottom);
    }

    /// <summary>แต้มวงกลมถี่ ๆ ตลอดแนวจากจุดหนึ่งไปอีกจุด</summary>
    private static void StampLine(
        Color32[] pixels, int width, int height,
        Vector2 from, Vector2 to, float radius, Color32 color)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance));

        for (int i = 0; i <= steps; i++)
        {
            Vector2 center = Vector2.Lerp(from, to, (float)i / steps);

            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(center.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(center.y + radius));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - center.x;
                    float dy = y + 0.5f - center.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > radius) continue;

                    byte alpha = (byte)(Mathf.Clamp01(radius - d) * 255f);
                    if (alpha == 0) continue;

                    int index = y * width + x;
                    Color32 dst = pixels[index];

                    if (dst.a == 0)
                    {
                        // ยังว่างอยู่ ทับได้เลย
                        pixels[index] = new Color32(color.r, color.g, color.b, alpha);
                        continue;
                    }

                    // มีของอยู่แล้ว ต้องผสมสี ไม่ใช่เลือกค่าที่ทึบกว่าแบบเดิม
                    //
                    // กฎเดิมใช้ได้ตอนที่ทั้งภาพมีสีเดียว แต่ตอนนี้มีพื้นขาวรองอยู่
                    // เส้นส้มที่ทึบเท่ากันจะทับพื้นขาวไม่ได้เลย กรอบจะหายไปทั้งเส้น
                    //
                    // ความทึบยังใช้ค่าที่มากกว่า ขอบเส้นจึงยังเนียนเหมือนเดิม
                    float srcA = alpha / 255f;
                    float invA = 1f - srcA;

                    pixels[index] = new Color32(
                        (byte)(color.r * srcA + dst.r * invA),
                        (byte)(color.g * srcA + dst.g * invA),
                        (byte)(color.b * srcA + dst.b * invA),
                        alpha > dst.a ? alpha : dst.a);
                }
            }
        }
    }
    /// <summary>
    /// แถบบทช่วยสอนล่างจอ ย้ายจาก OnGUI มาเป็นข้อความบนแคนวาส
    /// ข้อความจริงเก็บอยู่ใน SpellHintLabel แก้ได้จาก Inspector โดยไม่ต้องแตะโค้ด
    /// </summary>
    private static void CreateHintBar(Transform canvas)
    {
        var frame = new GameObject("HintBar", typeof(Image));
        frame.transform.SetParent(canvas, false);

        // พื้นหลังจาง ๆ พอให้อ่านออกบนฉากสว่าง แต่ไม่บังเกม
        frame.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.14f, 0.55f);

        var frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0f, 0f);
        frameRect.anchorMax = new Vector2(1f, 0f);
        frameRect.pivot = new Vector2(0.5f, 0f);
        frameRect.offsetMin = new Vector2(24f, 18f);
        frameRect.offsetMax = new Vector2(-24f, 0f);
        frameRect.sizeDelta = new Vector2(frameRect.sizeDelta.x, 74f);

        Text label = CreateText(frame.transform, "HintText", "", 20, Color.white);
        Object.DestroyImmediate(label.GetComponent<LayoutElement>());

        var labelRect = label.GetComponent<RectTransform>();
        StretchToParent(labelRect);
        labelRect.offsetMin = new Vector2(16f, 8f);
        labelRect.offsetMax = new Vector2(-16f, -8f);

        label.alignment = TextAnchor.MiddleCenter;

        var hint = frame.AddComponent<MagicDrawing.SpellHintLabel>();
        var so = new SerializedObject(hint);
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// หลอดวัดเสียงพูดแนวตั้ง มุมขวาบน
    ///
    /// ใช้ Image แบบ Filled เติมจากล่างขึ้นบน จึงสั่งความสูงด้วยค่าเดียว
    /// ไม่ต้องไปยุ่งกับขนาดของ RectTransform ทุกเฟรม
    /// </summary>
    private static void CreateVoiceMeter(Transform canvas)
    {
        var frame = new GameObject("VoiceMeter", typeof(Image));
        frame.transform.SetParent(canvas, false);
        frame.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.14f, 0.65f);

        var frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(1f, 1f);
        frameRect.anchorMax = new Vector2(1f, 1f);
        frameRect.pivot = new Vector2(1f, 1f);
        frameRect.anchoredPosition = new Vector2(-24f, -24f);
        frameRect.sizeDelta = new Vector2(46f, 200f);

        // ช่องด้านในเว้นขอบไว้ ให้เห็นกรอบรอบหลอด
        var track = new GameObject("Track", typeof(Image));
        track.transform.SetParent(frame.transform, false);
        track.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f);

        var trackRect = track.GetComponent<RectTransform>();
        StretchToParent(trackRect);
        trackRect.offsetMin = new Vector2(8f, 8f);
        trackRect.offsetMax = new Vector2(-8f, -8f);

        var fillGo = new GameObject("Fill", typeof(Image));
        fillGo.transform.SetParent(track.transform, false);
        StretchToParent(fillGo.GetComponent<RectTransform>());

        var fill = fillGo.GetComponent<Image>();
        // ต้องมี sprite ถึงจะใช้โหมด Filled ได้ ใช้ภาพสี่เหลี่ยมขาวที่สร้างไว้แล้ว
        fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareTexturePath);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 0f;

        // เส้นบอกว่าต้องตะโกนดังถึงไหนเวทถึงจะออก
        var marker = new GameObject("Threshold", typeof(Image));
        marker.transform.SetParent(track.transform, false);
        marker.GetComponent<Image>().color = Color.white;

        var markerRect = marker.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0f, 0.55f);
        markerRect.anchorMax = new Vector2(1f, 0.55f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = Vector2.zero;
        markerRect.sizeDelta = new Vector2(6f, 3f);
        marker.SetActive(false);

        // ขีดบอกว่าเสียงเคยขึ้นไปถึงไหนแล้ว คนละเส้นกับเกณฑ์ยิง
        var peak = new GameObject("Peak", typeof(Image));
        peak.transform.SetParent(track.transform, false);
        peak.GetComponent<Image>().color = new Color(1f, 0.95f, 0.4f);

        var peakRect = peak.GetComponent<RectTransform>();
        peakRect.anchorMin = new Vector2(0f, 0f);
        peakRect.anchorMax = new Vector2(1f, 0f);
        peakRect.pivot = new Vector2(0.5f, 0.5f);
        peakRect.anchoredPosition = Vector2.zero;
        peakRect.sizeDelta = new Vector2(0f, 2f);
        peak.SetActive(false);

        Text status = CreateText(frame.transform, "Status", "", 13, Color.white);
        Object.DestroyImmediate(status.GetComponent<LayoutElement>());

        var statusRect = status.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0f);
        statusRect.anchorMax = new Vector2(0.5f, 0f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.anchoredPosition = new Vector2(0f, -4f);
        statusRect.sizeDelta = new Vector2(120f, 20f);
        status.alignment = TextAnchor.UpperCenter;

        var meter = frame.AddComponent<MagicDrawing.VoiceMeter>();
        var so = new SerializedObject(meter);
        so.FindProperty("fill").objectReferenceValue = fill;
        so.FindProperty("statusLabel").objectReferenceValue = status;
        so.FindProperty("thresholdMarker").objectReferenceValue = markerRect;
        so.FindProperty("peakMarker").objectReferenceValue = peakRect;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    /// <summary>
    /// หน้าตั้งค่าไมโครโฟน เลือกไมค์แล้วทดสอบว่าได้ยินจริงไหม
    ///
    /// มีเพราะ "ไมค์ไม่ติด" หาสาเหตุเองไม่ได้เลยจากในเกม ผู้เล่นเห็นแค่ว่าเวทไม่ออก
    /// แต่แยกไม่ออกว่าเพราะพูดเบาไป เลือกไมค์ผิดตัว หรือ Windows ไม่ให้สิทธิ์
    /// </summary>
    private static GameObject CreateMicPanel(Transform canvas, OnlineUI2D ui)
    {
        GameObject panel = CreatePanel(canvas, "MicPanel", new Vector2(620f, 560f));

        AddLoc(CreateText(panel.transform, "Title", "", 34, TitleColor, FontStyle.Bold), "settings.title");

        // ภาษาอยู่บนสุด เพราะถ้าอ่านหัวข้ออื่นไม่ออก ต้องเจอธงก่อนเป็นอย่างแรก
        AddLoc(CreateText(panel.transform, "LanguageCaption", "", 20, TextColor), "settings.language");
        CreateLanguageRow(panel.transform);

        AddLoc(CreateText(panel.transform, "Caption", "", 20, TextColor), "settings.micDevice");

        Dropdown dropdown = CreateDropdown(panel.transform, "DeviceDropdown");

        AddLoc(CreateText(panel.transform, "TestCaption", "", 20, TextColor), "settings.micTest");

        // หลอดทดสอบวางแนวนอน อ่านง่ายกว่าแนวตั้งเมื่ออยู่ในการ์ด
        var track = new GameObject("LevelTrack", typeof(Image));
        track.transform.SetParent(panel.transform, false);
        track.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f);

        var trackElement = track.AddComponent<LayoutElement>();
        trackElement.minHeight = 34f;
        trackElement.preferredHeight = 34f;

        var fillGo = new GameObject("Fill", typeof(Image));
        fillGo.transform.SetParent(track.transform, false);
        StretchToParent(fillGo.GetComponent<RectTransform>());

        var fill = fillGo.GetComponent<Image>();
        fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareTexturePath);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;

        Text status = CreateText(panel.transform, "Status", "", 19, TextColor);

        // ข้อความสถานะยาวได้หลายบรรทัด ต้องเผื่อที่ไว้ ไม่งั้นตัวหนังสือจะถูกตัด
        var statusElement = status.GetComponent<LayoutElement>();
        if (statusElement != null)
        {
            statusElement.minHeight = 72f;
            statusElement.preferredHeight = 72f;
        }
        status.alignment = TextAnchor.UpperCenter;

        Button close = CreateLocButton(panel.transform, "CloseMicButton", "settings.done");

        var test = panel.AddComponent<MagicDrawing.MicTestPanel>();
        var testSo = new SerializedObject(test);
        testSo.FindProperty("deviceDropdown").objectReferenceValue = dropdown;
        testSo.FindProperty("levelFill").objectReferenceValue = fill;
        testSo.FindProperty("statusLabel").objectReferenceValue = status;
        testSo.ApplyModifiedPropertiesWithoutUndo();

        var uiSo = new SerializedObject(ui);
        uiSo.FindProperty("micPanel").objectReferenceValue = panel;
        uiSo.FindProperty("closeMicButton").objectReferenceValue = close;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    /// <summary>
    /// กล่องเลือกแบบพื้นฐาน Unity ไม่มีตัวช่วยสร้างจากโค้ด ต้องประกอบเองทั้งชุด
    /// โครงที่ Dropdown ต้องการคือ ป้ายข้อความ ลูกศร และแม่แบบรายการที่กางลงมา
    /// </summary>
    private static Dropdown CreateDropdown(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(Image), typeof(Dropdown));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f);

        var element = go.AddComponent<LayoutElement>();
        element.minHeight = 52f;
        element.preferredHeight = 52f;

        Text label = CreateText(go.transform, "Label", "", 20, Color.white);
        Object.DestroyImmediate(label.GetComponent<LayoutElement>());
        var labelRect = label.GetComponent<RectTransform>();
        StretchToParent(labelRect);
        labelRect.offsetMin = new Vector2(14f, 0f);
        labelRect.offsetMax = new Vector2(-30f, 0f);
        label.alignment = TextAnchor.MiddleLeft;

        // แม่แบบรายการที่กางลงมา ต้องปิดไว้ Unity จะเปิดเองตอนกด
        var template = new GameObject("Template", typeof(Image), typeof(ScrollRect));
        template.transform.SetParent(go.transform, false);
        template.GetComponent<Image>().color = new Color(0.13f, 0.15f, 0.20f);

        var templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, 2f);
        templateRect.sizeDelta = new Vector2(0f, 190f);

        var viewport = new GameObject("Viewport", typeof(Image), typeof(Mask));
        viewport.transform.SetParent(template.transform, false);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        var viewportRect = viewport.GetComponent<RectTransform>();
        StretchToParent(viewportRect);

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 46f);

        var item = new GameObject("Item", typeof(Toggle));
        item.transform.SetParent(content.transform, false);
        var itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, 46f);

        var itemBackground = new GameObject("Item Background", typeof(Image));
        itemBackground.transform.SetParent(item.transform, false);
        itemBackground.GetComponent<Image>().color = new Color(0.20f, 0.23f, 0.30f);
        StretchToParent(itemBackground.GetComponent<RectTransform>());

        Text itemLabel = CreateText(item.transform, "Item Label", "", 19, Color.white);
        Object.DestroyImmediate(itemLabel.GetComponent<LayoutElement>());
        var itemLabelRect = itemLabel.GetComponent<RectTransform>();
        StretchToParent(itemLabelRect);
        itemLabelRect.offsetMin = new Vector2(14f, 0f);
        itemLabelRect.offsetMax = new Vector2(-10f, 0f);
        itemLabel.alignment = TextAnchor.MiddleLeft;

        var toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = itemBackground.GetComponent<Image>();

        var scroll = template.GetComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.viewport = viewportRect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var dropdown = go.GetComponent<Dropdown>();
        dropdown.targetGraphic = go.GetComponent<Image>();
        dropdown.captionText = label;
        dropdown.itemText = itemLabel;
        dropdown.template = templateRect;

        template.SetActive(false);

        return dropdown;
    }
    /// <summary>
    /// เมนูกลางจอที่เปิดด้วย Esc รวมรหัสห้อง ปุ่มออก ตั้งค่า และวิธีเล่นไว้ที่เดียว
    /// เดิมข้อมูลพวกนี้ลอยอยู่มุมจอ ซึ่งบังพื้นที่เล่นและกดโดนโดยไม่ตั้งใจได้ง่าย
    /// </summary>
    private static void CreatePauseMenu(Transform canvas, OnlineUI2D ui)
    {
        // ฉากหลังมืดคลุมทั้งจอ กดทะลุไปโดนเกมข้างหลังไม่ได้
        var root = new GameObject("PauseRoot", typeof(Image));
        root.transform.SetParent(canvas, false);
        root.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.75f);
        StretchToParent(root.GetComponent<RectTransform>());

        GameObject card = CreatePanel(root.transform, "PauseCard", new Vector2(760f, 620f));

        Text title = CreateText(card.transform, "Title", "", 36, TitleColor, FontStyle.Bold);
        AddLoc(title, "pause.title");

        Text roomLabel = CreateText(card.transform, "RoomLabel", "", 20, TextColor);

        // ---------- หน้าหลัก ----------

        GameObject mainPage = CreateColumn(card.transform, "MainPage", 0f, 1f);

        Button resume = CreateLocButton(mainPage.transform, "ResumeButton", "pause.resume");
        Button settings = CreateLocButton(mainPage.transform, "SettingsButton", "pause.settings");
        Button tutorial = CreateLocButton(mainPage.transform, "TutorialButton", "pause.tutorial");
        Button leave = CreateLocButton(mainPage.transform, "LeaveButton", "pause.leave");

        // ---------- หน้าตั้งค่า ----------

        GameObject settingsPage = CreateColumn(card.transform, "SettingsPage", 0f, 1f);

        Text langCaption = CreateText(settingsPage.transform, "LangCaption", "", 20, TextColor);
        AddLoc(langCaption, "settings.language");

        Button langButton = CreateButton(settingsPage.transform, "LanguageButton", "ไทย / English");
        Text langValue = CreateText(settingsPage.transform, "LangValue", "", 22, TitleColor, FontStyle.Bold);

        Text volCaption = CreateText(settingsPage.transform, "VolumeCaption", "", 20, TextColor);
        AddLoc(volCaption, "settings.volume");

        Slider volumeSlider = CreateSlider(settingsPage.transform, "VolumeSlider");
        var volElement = volumeSlider.gameObject.AddComponent<LayoutElement>();
        volElement.minHeight = 30f;
        volElement.preferredHeight = 30f;

        Text volValue = CreateText(settingsPage.transform, "VolumeValue", "100 %", 22, TitleColor, FontStyle.Bold);

        var settingsPanel = settingsPage.AddComponent<MagicDrawing.GameSettingsPanel>();
        var settingsSo = new SerializedObject(settingsPanel);
        settingsSo.FindProperty("languageButton").objectReferenceValue = langButton;
        settingsSo.FindProperty("languageValue").objectReferenceValue = langValue;
        settingsSo.FindProperty("volumeSlider").objectReferenceValue = volumeSlider;
        settingsSo.FindProperty("volumeValue").objectReferenceValue = volValue;
        settingsSo.ApplyModifiedPropertiesWithoutUndo();

        // ---------- หน้าวิธีเล่น ----------

        GameObject tutorialPage = CreateColumn(card.transform, "TutorialPage", 0f, 1f);

        var pictureGo = new GameObject("Picture", typeof(Image));
        pictureGo.transform.SetParent(tutorialPage.transform, false);

        var picture = pictureGo.GetComponent<Image>();
        picture.preserveAspect = true;

        var pictureElement = pictureGo.AddComponent<LayoutElement>();
        pictureElement.minHeight = 240f;
        pictureElement.preferredHeight = 240f;

        Text tutTitle = CreateText(tutorialPage.transform, "TutTitle", "", 26, TitleColor, FontStyle.Bold);
        Text tutBody = CreateText(tutorialPage.transform, "TutBody", "", 20, TextColor);

        // เนื้อหาบางหน้ายาวหลายบรรทัด ต้องเผื่อที่ไว้ ไม่งั้นตัวหนังสือถูกตัด
        var bodyElement = tutBody.GetComponent<LayoutElement>();
        if (bodyElement != null)
        {
            bodyElement.minHeight = 110f;
            bodyElement.preferredHeight = 110f;
        }
        tutBody.alignment = TextAnchor.UpperCenter;

        // แถวปุ่มเปลี่ยนหน้า
        var navRow = new GameObject("NavRow", typeof(RectTransform));
        navRow.transform.SetParent(tutorialPage.transform, false);

        var navLayout = navRow.AddComponent<HorizontalLayoutGroup>();
        navLayout.spacing = 12f;
        navLayout.childAlignment = TextAnchor.MiddleCenter;
        navLayout.childControlWidth = true;
        navLayout.childControlHeight = true;
        navLayout.childForceExpandWidth = true;
        navLayout.childForceExpandHeight = true;

        var navElement = navRow.AddComponent<LayoutElement>();
        navElement.minHeight = 56f;
        navElement.preferredHeight = 56f;

        Button prev = CreateLocButton(navRow.transform, "PrevButton", "tut.prev");
        Text pageLabel = CreateText(navRow.transform, "PageLabel", "1 / 5", 20, TextColor);
        Button next = CreateLocButton(navRow.transform, "NextButton", "tut.next");

        var tutorialPanel = tutorialPage.AddComponent<MagicDrawing.TutorialPanel>();
        WireTutorial(tutorialPanel, picture, tutTitle, tutBody, pageLabel, next, prev);

        // ---------- ปุ่มย้อนกลับ ----------

        Button back = CreateLocButton(card.transform, "BackButton", "pause.back");

        // ---------- ผูกเข้าตัวคุมเมนู ----------

        // ตัวคุมเมนูต้องอยู่บนแคนวาสที่เปิดอยู่ตลอด ไม่ใช่บน root ที่ถูกปิดไว้
        //
        // Unity ไม่รัน Update ของวัตถุที่ถูกปิด ถ้าเอาตัวคุมไปไว้บน root
        // จะไม่มีใครคอยฟังปุ่ม Esc เลย เมนูจึงไม่มีทางเปิดได้
        var pause = canvas.gameObject.AddComponent<MagicDrawing.PauseMenu>();
        var pauseSo = new SerializedObject(pause);
        pauseSo.FindProperty("root").objectReferenceValue = root;
        pauseSo.FindProperty("mainPage").objectReferenceValue = mainPage;
        pauseSo.FindProperty("settingsPage").objectReferenceValue = settingsPage;
        pauseSo.FindProperty("tutorialPage").objectReferenceValue = tutorialPage;
        pauseSo.FindProperty("resumeButton").objectReferenceValue = resume;
        pauseSo.FindProperty("settingsButton").objectReferenceValue = settings;
        pauseSo.FindProperty("tutorialButton").objectReferenceValue = tutorial;
        pauseSo.FindProperty("leaveButton").objectReferenceValue = leave;
        pauseSo.FindProperty("backButton").objectReferenceValue = back;
        pauseSo.FindProperty("roomLabel").objectReferenceValue = roomLabel;
        pauseSo.ApplyModifiedPropertiesWithoutUndo();

        // ปิดไว้ก่อน เปิดเมื่อกด Esc เท่านั้น
        root.SetActive(false);
    }

    /// <summary>ใส่เนื้อหาห้าหน้าพร้อมภาพประกอบที่วาดด้วยโค้ด</summary>
    private static void WireTutorial(
        MagicDrawing.TutorialPanel panel, Image picture,
        Text title, Text body, Text pageLabel, Button next, Button prev)
    {
        Sprite[] art = TutorialArtGenerator.CreateAll();

        string[] titleKeys =
        {
            "tut.draw.title", "tut.shapes.title", "tut.shield.title",
            "tut.fire.title", "tut.counter.title",
        };

        string[] bodyKeys =
        {
            "tut.draw.body", "tut.shapes.body", "tut.shield.body",
            "tut.fire.body", "tut.counter.body",
        };

        var so = new SerializedObject(panel);
        so.FindProperty("picture").objectReferenceValue = picture;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        so.FindProperty("bodyLabel").objectReferenceValue = body;
        so.FindProperty("pageLabel").objectReferenceValue = pageLabel;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.FindProperty("prevButton").objectReferenceValue = prev;

        SerializedProperty pages = so.FindProperty("pages");
        pages.arraySize = titleKeys.Length;

        for (int i = 0; i < titleKeys.Length; i++)
        {
            SerializedProperty page = pages.GetArrayElementAtIndex(i);
            page.FindPropertyRelative("Picture").objectReferenceValue = i < art.Length ? art[i] : null;
            page.FindPropertyRelative("TitleKey").stringValue = titleKeys[i];
            page.FindPropertyRelative("BodyKey").stringValue = bodyKeys[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>ปุ่มที่ข้อความแปลตามภาษาที่เลือก</summary>
    private static Button CreateLocButton(Transform parent, string name, string key)
    {
        Button button = CreateButton(parent, name, "");

        Text label = button.GetComponentInChildren<Text>();
        if (label != null) AddLoc(label, key);

        return button;
    }

    /// <summary>
    /// อัปเดตเมนูในฉากที่มีอยู่ให้ตรงกับตัวสร้างฉากรุ่นล่าสุด
    ///
    /// มีแยกจากการติดตั้งฉากอัตโนมัติ เพราะการติดตั้งใหม่จะสร้างฉากสนามรบทับด้วย
    /// ซึ่งลบแมพที่ปั้นไว้ทิ้ง คำสั่งนี้แตะเฉพาะเมนู ไม่ยุ่งกับฉากสนามรบเลย
    /// </summary>
    [MenuItem("Tools/เกมวาดวงเวท/อัปเดตเมนูในฉากที่มีอยู่", priority = 1)]
    public static void UpgradeExistingMenu()
    {
        Scene scene = EditorSceneManager.GetSceneByPath(LobbyScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

        OnlineUI2D ui = Object.FindFirstObjectByType<OnlineUI2D>(FindObjectsInactive.Include);
        if (ui == null)
        {
            Debug.LogError("[อัปเดตเมนู] ไม่เจอ OnlineUI2D ในฉาก");
            return;
        }

        Transform joinPanel = FindByName(scene, "JoinPanel");
        Transform micPanel = FindByName(scene, "MicPanel");
        if (joinPanel == null || micPanel == null)
        {
            Debug.LogError("[อัปเดตเมนู] ไม่เจอ JoinPanel หรือ MicPanel");
            return;
        }

        // ---- หน้าเข้าห้อง: ยุบปุ่มไมค์กับปุ่มภาษาเหลือปุ่มตั้งค่าปุ่มเดียว ----
        DestroyChild(joinPanel, "MicButton");
        DestroyChild(joinPanel, "LanguageButton");

        if (joinPanel.Find("SettingsButton") == null)
        {
            Button settings = CreateSettingsButton(joinPanel);

            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("openMicButton").objectReferenceValue = settings;
            uiSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- หน้าตั้งค่า: เติมแถวธงและผูกคำแปลของหัวข้อ ----
        EnsureLoc(micPanel.Find("Title"), "settings.title");
        EnsureLoc(micPanel.Find("Caption"), "settings.micDevice");
        EnsureLoc(micPanel.Find("TestCaption"), "settings.micTest");
        EnsureLoc(micPanel.Find("CloseMicButton"), "settings.done");

        if (micPanel.Find("LanguageRow") == null)
        {
            Text caption = CreateText(micPanel, "LanguageCaption", "", 20, TextColor);
            AddLoc(caption, "settings.language");

            GameObject row = CreateLanguageRow(micPanel);

            // ภาษาต้องอยู่บนสุดถัดจากหัวข้อ คนที่อ่านไม่ออกจะได้เจอธงก่อน
            caption.transform.SetSiblingIndex(1);
            row.transform.SetSiblingIndex(2);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[อัปเดตเมนู] เรียบร้อย ฉากสนามรบไม่ถูกแตะต้อง");
    }

    private static Transform FindByName(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr;

        return null;
    }

    private static void DestroyChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    /// <summary>ผูกคำแปลโดยไม่ซ้ำของเดิม ใช้กับฉากที่เคยผูกไปแล้วบางส่วน</summary>
    private static void EnsureLoc(Transform target, string key)
    {
        if (target == null) return;

        Text label = target.GetComponent<Text>();
        if (label == null) label = target.GetComponentInChildren<Text>(true);
        if (label == null) return;

        var existing = label.GetComponent<MagicDrawing.LocalizedText>();
        if (existing != null)
        {
            var so = new SerializedObject(existing);
            so.FindProperty("key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
            return;
        }

        AddLoc(label, key);
    }

    /// <summary>
    /// ปุ่มเปิดหน้าตั้งค่า มีฟันเฟืองนำหน้าข้อความ
    ///
    /// ฟันเฟืองเป็นสัญลักษณ์ที่คนรู้จักกันทั่วโลกว่าแปลว่าตั้งค่า
    /// ผู้เล่นที่อ่านข้อความบนปุ่มไม่ออกจึงยังเดาถูกว่าปุ่มนี้ทำอะไร
    /// </summary>
    private static Button CreateSettingsButton(Transform parent)
    {
        Button button = CreateLocButton(parent, "SettingsButton", "settings.title");

        var iconGo = new GameObject("GearIcon", typeof(Image));
        iconGo.transform.SetParent(button.transform, false);

        var icon = iconGo.GetComponent<Image>();
        icon.sprite = IconArtGenerator.GearIcon();
        icon.preserveAspect = true;

        // ไอคอนต้องไม่กินคลิก ไม่งั้นกดโดนตรงฟันเฟืองแล้วปุ่มไม่ทำงาน
        icon.raycastTarget = false;

        var rect = iconGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(30f, 30f);
        rect.anchoredPosition = new Vector2(24f, 0f);

        return button;
    }

    /// <summary>แถวธงสองผืนสำหรับเลือกภาษา</summary>
    private static GameObject CreateLanguageRow(Transform parent)
    {
        var row = new GameObject("LanguageRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 78f;
        element.preferredHeight = 78f;

        Image thaiFlag, englishFlag;
        Graphic thaiMark, englishMark;

        Button thai = CreateFlagButton(row.transform, "ThaiButton",
            IconArtGenerator.ThaiFlag(), out thaiFlag, out thaiMark);
        Button english = CreateFlagButton(row.transform, "EnglishButton",
            IconArtGenerator.EnglishFlag(), out englishFlag, out englishMark);

        var picker = row.AddComponent<MagicDrawing.LanguagePicker>();
        var so = new SerializedObject(picker);
        so.FindProperty("thaiButton").objectReferenceValue = thai;
        so.FindProperty("englishButton").objectReferenceValue = english;
        so.FindProperty("thaiFlag").objectReferenceValue = thaiFlag;
        so.FindProperty("englishFlag").objectReferenceValue = englishFlag;
        so.FindProperty("thaiHighlight").objectReferenceValue = thaiMark;
        so.FindProperty("englishHighlight").objectReferenceValue = englishMark;
        so.ApplyModifiedPropertiesWithoutUndo();

        return row;
    }

    /// <summary>ปุ่มธงหนึ่งผืน พร้อมกรอบเน้นที่เปิดปิดได้</summary>
    private static Button CreateFlagButton(
        Transform parent, string name, Sprite flagSprite, out Image flag, out Graphic highlight)
    {
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareTexturePath);

        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        // พื้นหลังปุ่มต้องเปิดอยู่เสมอ เพราะเป็นตัวรับคลิก
        // ถ้าเอากรอบเน้นมาเป็นตัวรับคลิกแล้วปิดมันตอนไม่ได้เลือก ปุ่มจะกดไม่ได้
        var background = go.GetComponent<Image>();
        background.sprite = square;
        background.color = new Color(0.16f, 0.18f, 0.24f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = background;

        var element = go.AddComponent<LayoutElement>();
        element.minHeight = 70f;
        element.preferredHeight = 70f;

        var markGo = new GameObject("Highlight", typeof(Image));
        markGo.transform.SetParent(go.transform, false);
        StretchToParent(markGo.GetComponent<RectTransform>());

        var mark = markGo.GetComponent<Image>();
        mark.sprite = square;
        mark.color = AccentColor;
        mark.raycastTarget = false;
        highlight = mark;

        var flagGo = new GameObject("Flag", typeof(Image));
        flagGo.transform.SetParent(go.transform, false);

        flag = flagGo.GetComponent<Image>();
        flag.sprite = flagSprite;
        flag.preserveAspect = true;
        flag.raycastTarget = false;

        // เว้นขอบให้เห็นกรอบเน้นที่อยู่ข้างหลังโผล่ออกมารอบธง
        var flagRect = flagGo.GetComponent<RectTransform>();
        StretchToParent(flagRect);
        flagRect.offsetMin = new Vector2(8f, 8f);
        flagRect.offsetMax = new Vector2(-8f, -8f);

        return button;
    }

    /// <summary>ผูกป้ายเข้ากับกุญแจในตารางแปล</summary>
    private static void AddLoc(Text label, string key)
    {
        if (label == null) return;

        var loc = label.gameObject.AddComponent<MagicDrawing.LocalizedText>();
        var so = new SerializedObject(loc);
        so.FindProperty("key").stringValue = key;
        so.ApplyModifiedPropertiesWithoutUndo();

        // เก็บข้อความจริงไว้ในฉากด้วย ไม่ปล่อยว่างให้ตัวแปลเติมตอนรันอย่างเดียว
        //
        // สองเหตุผล: เปิดฉากในเอดิเตอร์แล้วเห็นหน้าตาจริงว่าอะไรล้นอะไรสั้นไป
        // และถ้าวันหนึ่งตัวแปลพัง ผู้เล่นจะยังเห็นข้อความ ไม่ใช่ปุ่มเปล่า
        label.text = Loc.Get(key);
        EditorUtility.SetDirty(label);
    }
}
