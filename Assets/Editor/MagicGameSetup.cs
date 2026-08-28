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

        var ui = go.AddComponent<OnlineUI2D>();

        // ไว้บนตัวเดียวกับ NetworkManager เพราะมันตามข้ามซีนไปด้วย
        // เสียงและเอฟเฟกต์จึงตั้งค่าครั้งเดียวใช้ได้ทั้งห้องรอและสนามรบ
        var audioLibrary = go.AddComponent<SpellAudioLibrary>();
        var vfxLibrary = go.AddComponent<SpellVfxLibrary>();
        WireAssetLibraries(audioLibrary, vfxLibrary);

        BuildMenuCanvas(ui, go.transform);

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
    private static readonly Color TextColor = new Color(0.88f, 0.90f, 0.94f);
    private static readonly Color ButtonColor = new Color(0.20f, 0.24f, 0.34f);

    /// <summary>
    /// สร้าง Canvas ของเมนูแล้วผูก reference เข้ากับ OnlineUI2D
    ///
    /// วางเป็นลูกของ NetworkManager เพราะ Netcode สั่ง DontDestroyOnLoad ให้
    /// object นั้น Canvas จึงข้ามซีนไปด้วย ถ้าวางแยกไว้ในซีนเมนู มันจะถูกทำลาย
    /// ตอนโหลดสนามรบ แล้วแถบย่อที่ควรโชว์รหัสห้องจะหายไป
    /// </summary>
    private static void BuildMenuCanvas(OnlineUI2D ui, Transform parent)
    {
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

        GameObject joinPanel = CreatePanel(canvasGo.transform, "JoinPanel", new Vector2(560f, 520f));
        GameObject roomPanel = CreatePanel(canvasGo.transform, "RoomPanel", new Vector2(560f, 620f));

        // ---- หน้าเข้าห้อง ----
        CreateText(joinPanel.transform, "Title", "วงเวทออนไลน์", 46, AccentColor, FontStyle.Bold);
        CreateText(joinPanel.transform, "Subtitle",
            "สร้างห้องแล้วส่งรหัสให้เพื่อน หรือใส่รหัสที่ได้รับ", 20, TextColor);

        Button hostButton = CreateButton(joinPanel.transform, "HostButton", "สร้างห้องใหม่");
        CreateText(joinPanel.transform, "OrLabel", "— หรือ —", 20, TextColor);
        InputField codeInput = CreateInput(joinPanel.transform, "CodeInput", "ใส่รหัสห้อง");
        Button joinButton = CreateButton(joinPanel.transform, "JoinButton", "เข้าห้องด้วยรหัส");

        // ---- หน้าในห้อง ----
        Text roleText = CreateText(roomPanel.transform, "RoleText", "คุณเป็นเจ้าของห้อง", 26, AccentColor, FontStyle.Bold);
        CreateText(roomPanel.transform, "CodeCaption", "รหัสห้อง", 20, TextColor);

        // รหัสห้องตัวใหญ่พิเศษ เพราะเป็นข้อความที่ต้องอ่านให้เพื่อนฟังทางโทรศัพท์
        Text roomCodeText = CreateText(roomPanel.transform, "RoomCodeText", "ABCDEF", 52, Color.white, FontStyle.Bold);

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
        Text statusText = CreateText(canvasGo.transform, "StatusText", "", 22, TextColor);
        var statusRect = statusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0f);
        statusRect.anchorMax = new Vector2(0.5f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 40f);
        statusRect.sizeDelta = new Vector2(900f, 40f);

        WireMenuUI(ui, joinPanel, roomPanel, compactPanel, codeInput,
            hostButton, joinButton, roleText, roomCodeText, playersText, waitText,
            copyButton, startButton, leaveButton, compactText, compactLeave, statusText);
    }

    private static void WireMenuUI(
        OnlineUI2D ui,
        GameObject joinPanel, GameObject roomPanel, GameObject compactPanel,
        InputField codeInput, Button hostButton, Button joinButton,
        Text roleText, Text roomCodeText, Text playersText, Text waitText,
        Button copyButton, Button startButton, Button leaveButton,
        Text compactText, Button compactLeave, Text statusText)
    {
        var so = new SerializedObject(ui);

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
    /// การ์ดกลางจอ ใช้ VerticalLayoutGroup เรียงของข้างในให้อัตโนมัติ
    /// จะได้ไม่ต้องมานั่งคำนวณตำแหน่งทีละชิ้น และเพิ่ม/ลบชิ้นได้โดยไม่ต้องขยับของอื่น
    /// </summary>
    private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(Image), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);

        go.GetComponent<Image>().color = PanelColor;

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
        layout.childControlHeight = false;

        return go;
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

        // ฟอนต์ตั้งตอนรันไทม์โดย OnlineUI2D เพราะต้องหาฟอนต์ที่มีอักขระไทย
        // จากเครื่องผู้เล่น ฟอนต์เริ่มต้นของ uGUI ไม่มีภาษาไทย

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
        image.color = ButtonColor;

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

        Text text = CreateText(go.transform, "Label", label, 22, Color.white);
        StretchToParent(text.GetComponent<RectTransform>());
        Object.DestroyImmediate(text.GetComponent<LayoutElement>());

        return button;
    }

    private static InputField CreateInput(Transform parent, string name, string placeholder)
    {
        var go = new GameObject(name, typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);

        go.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.26f);

        var element = go.AddComponent<LayoutElement>();
        element.minHeight = 60f;
        element.preferredHeight = 60f;

        Text placeholderText = CreateText(go.transform, "Placeholder", placeholder, 24,
            new Color(0.55f, 0.58f, 0.66f));
        Text valueText = CreateText(go.transform, "Text", "", 28, Color.white, FontStyle.Bold);

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
