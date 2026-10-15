using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Flappy Bird sahnasini yaratadi: qush, yer, bulutlar, quvur prefabi va UI.
/// FlappyBird sahnasi ochiq bo'lsa va obyektlar hali yo'q bo'lsa, avtomatik ishga tushadi.
/// Qo'lda: menyu "Flappy Bird → Sahnani qayta yaratish".
/// </summary>
[InitializeOnLoad]
public static class FlappyBirdSetup
{
    const string Root = "Assets/FlappyBird";
    const string ScenePath = Root + "/FlappyBird.unity";
    const string PrefabPath = Root + "/Prefabs/Pipe.prefab";

    static FlappyBirdSetup()
    {
        EditorApplication.delayCall += AutoSetup;
    }

    static void AutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;
        var game = Object.FindFirstObjectByType<FlappyBirdGame>();
        if (game != null && game.bird != null) return;
        BuildScene();
        Debug.Log("Flappy Bird: sahna obyektlari yaratildi. Endi ularni Hierarchy'da tahrirlashingiz mumkin.");
    }

    [MenuItem("Flappy Bird/Sahnani qayta yaratish")]
    static void RebuildMenu()
    {
        if (!EditorUtility.DisplayDialog("Flappy Bird",
                "Sahnadagi 'World' va 'UI' obyektlari o'chirilib, qaytadan yaratiladi. Davom etilsinmi?",
                "Ha", "Yo'q")) return;

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        BuildScene();
    }

    // ------------------------------------------------------------------

    static void BuildScene()
    {
        const float groundHeight = 1.3f;
        const float camSize = 5f;
        const float groundTop = -camSize + groundHeight;

        // 1) Sprite'larni import sozlamalari
        var birdSprite = ImportSprite("Bird", 40f, FilterMode.Point, new Vector2(0.5f, 0.5f));
        var bodySprite = ImportSprite("PipeBody", 32f, FilterMode.Bilinear, new Vector2(0.5f, 0.5f));
        var capSprite = ImportSprite("PipeCap", 32f, FilterMode.Point, new Vector2(0.5f, 0.5f));
        var cloudSprite = ImportSprite("Cloud", 32f, FilterMode.Point, new Vector2(0.5f, 0.5f));
        var groundSprite = ImportSprite("Ground", 32f / groundHeight, FilterMode.Point, new Vector2(0.5f, 1f));

        // 2) Eski obyektlarni tozalash
        DestroyIfExists("World");
        DestroyIfExists("UI");

        // 3) Kamera
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = camSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.44f, 0.77f, 0.80f);
            EditorUtility.SetDirty(cam);
        }

        // 4) Dunyo
        var world = new GameObject("World").transform;

        var bird = NewSprite("Bird", birdSprite, 20, world);
        bird.transform.position = new Vector3(-0.8f, 0.5f, 0);

        var ground = NewSprite("Ground", groundSprite, 10, world);
        ground.drawMode = SpriteDrawMode.Tiled;
        ground.tileMode = SpriteTileMode.Continuous;
        ground.size = new Vector2(24f, groundHeight);
        ground.transform.position = new Vector3(0, groundTop, 0);

        var cloudsRoot = new GameObject("Clouds").transform;
        cloudsRoot.SetParent(world, false);
        Vector3[] cloudPos = { new Vector3(-5, 3.2f), new Vector3(-1.5f, 1.6f), new Vector3(2.5f, 3.8f), new Vector3(5.5f, 2.2f), new Vector3(8.5f, 3.4f) };
        float[] cloudScale = { 1.2f, 0.9f, 1.4f, 1.0f, 1.3f };
        var clouds = new Transform[cloudPos.Length];
        for (int i = 0; i < cloudPos.Length; i++)
        {
            var c = NewSprite("Cloud", cloudSprite, -10, cloudsRoot);
            c.transform.position = cloudPos[i];
            c.transform.localScale = Vector3.one * cloudScale[i];
            clouds[i] = c.transform;
        }

        // 5) Quvur prefabi
        var pipePrefab = BuildPipePrefab(bodySprite, capSprite, groundTop, camSize);
        var preview = (GameObject)PrefabUtility.InstantiatePrefab(pipePrefab);
        preview.name = "Pipe (namuna)";
        preview.transform.SetParent(world, false);
        preview.transform.position = new Vector3(2.2f, 0, 0);

        // 6) UI
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasGo = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        var ui = canvasGo.transform;

        // Butun ekran — bosilganda sakrash
        var tap = NewUI("TapArea", ui);
        Stretch(tap);
        var tapImg = tap.gameObject.AddComponent<Image>();
        tapImg.color = new Color(0, 0, 0, 0);
        var tapArea = tap.gameObject.AddComponent<TapArea>();

        // Hisob
        var scoreText = NewText(ui, "ScoreText", "0", 150, new Vector2(0.5f, 0.88f), new Vector2(600, 200), font);

        // Boshlash paneli
        var ready = NewUI("ReadyPanel", ui);
        Stretch(ready);
        NewText(ready, "Title", "FLAPPY BIRD", 130, new Vector2(0.5f, 0.80f), new Vector2(1000, 200), font);
        NewText(ready, "Hint", "Sakrash uchun ekranga bosing\n(kompyuterda: SPACE)", 48, new Vector2(0.5f, 0.62f), new Vector2(950, 200), font);
        var readyBest = NewText(ready, "Best", "Rekord: 0", 56, new Vector2(0.5f, 0.54f), new Vector2(800, 100), font);

        // O'yin tugadi paneli
        var over = NewUI("GameOverPanel", ui);
        Stretch(over);
        NewText(over, "Title", "O'YIN TUGADI", 120, new Vector2(0.5f, 0.76f), new Vector2(1000, 200), font);
        var overScore = NewText(over, "Score", "Hisob: 0\nRekord: 0", 64, new Vector2(0.5f, 0.64f), new Vector2(800, 200), font);
        var hint = NewText(over, "RestartHint", "Qayta boshlash uchun bosing", 52, new Vector2(0.5f, 0.54f), new Vector2(950, 100), font);
        over.gameObject.SetActive(false);

        // Urilganda oq chaqnash
        var flash = NewUI("Flash", ui);
        Stretch(flash);
        var flashImg = flash.gameObject.AddComponent<Image>();
        flashImg.color = new Color(1, 1, 1, 0);
        flashImg.raycastTarget = false;

        // EventSystem
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        // 7) O'yin komponenti
        var game = Object.FindFirstObjectByType<FlappyBirdGame>();
        if (game == null) game = new GameObject("FlappyBirdGame").AddComponent<FlappyBirdGame>();
        var src = game.GetComponent<AudioSource>();
        if (src == null) src = game.gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;

        game.bird = bird.transform;
        game.ground = ground;
        game.clouds = clouds;
        game.pipePrefab = pipePrefab;
        game.previewPipe = preview;
        game.tapArea = tapArea;
        game.scoreText = scoreText;
        game.readyPanel = ready.gameObject;
        game.readyBestText = readyBest;
        game.gameOverPanel = over.gameObject;
        game.gameOverScoreText = overScore;
        game.restartHint = hint.gameObject;
        game.flashImage = flashImg;
        game.sfx = src;
        game.flapClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Flap.wav");
        game.scoreClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Score.wav");
        game.hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Hit.wav");
        game.groundHeight = groundHeight;
        EditorUtility.SetDirty(game);

        var sceneNow = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(sceneNow);
        EditorSceneManager.SaveScene(sceneNow);
        Selection.activeGameObject = game.gameObject;
    }

    // ------------------------------------------------------------------ helpers

    static GameObject BuildPipePrefab(Sprite body, Sprite cap, float groundTop, float camSize)
    {
        if (!AssetDatabase.IsValidFolder(Root + "/Prefabs"))
            AssetDatabase.CreateFolder(Root, "Prefabs");

        var temp = new GameObject("Pipe");
        NewSprite("Bottom", body, 0, temp.transform);
        NewSprite("BottomCap", cap, 1, temp.transform);
        NewSprite("Top", body, 0, temp.transform);
        NewSprite("TopCap", cap, 1, temp.transform);
        FlappyBirdGame.LayoutPipe(temp.transform, 0.5f, 2.7f, 1.1f, groundTop, camSize + 1f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    static Sprite ImportSprite(string name, float ppu, FilterMode filter, Vector2 pivot)
    {
        string path = Root + "/Sprites/" + name + ".png";
        AssetDatabase.ImportAsset(path);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
        {
            Debug.LogError("Flappy Bird: rasm topilmadi: " + path);
            return null;
        }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMode = (int)SpriteImportMode.Single;
        s.spritePixelsPerUnit = ppu;
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = pivot;
        s.filterMode = filter;
        s.mipmapEnabled = false;
        s.wrapMode = TextureWrapMode.Clamp;
        ti.SetTextureSettings(s);
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static SpriteRenderer NewSprite(string name, Sprite sprite, int order, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static Text NewText(Transform parent, string name, string text, int size, Vector2 anchor, Vector2 box, Font font)
    {
        var r = NewUI(name, parent);
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = box;
        var t = r.gameObject.AddComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var o = r.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.15f, 0.1f, 0.05f);
        o.effectDistance = new Vector2(5, -5);
        return t;
    }

    static void DestroyIfExists(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name) Object.DestroyImmediate(root);
    }
}
