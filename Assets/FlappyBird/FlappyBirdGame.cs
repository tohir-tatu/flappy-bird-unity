using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Flappy Bird (2D, mobil) — o'yin boshqaruvchisi.
/// Barcha obyektlar (qush, yer, bulutlar, quvur prefabi, UI) sahnada turadi va
/// Inspector'da tahrirlanadi. Ular "Flappy Bird → Sahnani qayta yaratish" menyusi orqali yaratilgan.
///
/// Telefonda: ekranga bosish — sakrash.
/// Kompyuterda: SPACE (yoki sichqoncha chap tugmasi) — sakrash.
/// </summary>
public class FlappyBirdGame : MonoBehaviour
{
    [Header("Sahna obyektlari")]
    public Transform bird;
    public SpriteRenderer ground;
    public Transform[] clouds;
    [Tooltip("Quvur prefabi: ichida Bottom, BottomCap, Top, TopCap bolalari bo'lishi kerak")]
    public GameObject pipePrefab;
    [Tooltip("Sahnadagi namuna quvur — o'yin boshlanganda yashiriladi")]
    public GameObject previewPipe;

    [Header("UI")]
    public TapArea tapArea;
    public Text scoreText;
    public GameObject readyPanel;
    public Text readyBestText;
    public GameObject gameOverPanel;
    public Text gameOverScoreText;
    public GameObject restartHint;
    public Image flashImage;

    [Header("Audio")]
    public AudioSource sfx;
    public AudioClip flapClip;
    public AudioClip scoreClip;
    public AudioClip hitClip;
    [Tooltip("AudioMixer'dagi SFX guruhi (ixtiyoriy)")]
    public AudioMixerGroup sfxMixerGroup;

    [Header("Qush")]
    public float gravity = -26f;
    public float flapVelocity = 8.2f;
    public float maxFallSpeed = -12f;
    public float birdRadius = 0.28f;

    [Header("Quvurlar")]
    public float pipeSpeed = 3f;
    public float pipeDistance = 3.6f;
    public float gapSize = 2.7f;
    public float pipeWidth = 1.1f;

    [Header("Yer va ekran")]
    public float groundHeight = 1.3f;
    public Color skyColor = new Color(0.44f, 0.77f, 0.80f);
    public bool forcePortrait = true;

    public const float CapHeight = 0.45f;

    enum State { Ready, Playing, Dead }

    class Pipe
    {
        public Transform root;
        public float gapY;
        public bool scored;
    }

    State state = State.Ready;
    Camera cam;
    readonly List<Pipe> pipes = new List<Pipe>();

    float birdX, birdY, birdVel, birdAngle;
    float groundTileWidth, groundOffset;
    int score, best;
    float deadTimer, flash;
    bool tapQueued;

    float CamSize => cam.orthographicSize;
    float HalfWidth => cam.orthographicSize * cam.aspect;
    float GroundTop => -CamSize + groundHeight;
    float HomeX => -HalfWidth * 0.35f;

    // ------------------------------------------------------------------ setup

    void Awake()
    {
        cam = Camera.main;
        if (bird == null || ground == null || pipePrefab == null)
        {
            Debug.LogError("FlappyBirdGame: sahna obyektlari ulanmagan. Menyudan 'Flappy Bird → Sahnani qayta yaratish' ni bosing.");
            enabled = false;
            return;
        }

        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = skyColor;

        if (forcePortrait && Application.isMobilePlatform)
            Screen.orientation = ScreenOrientation.Portrait;
        Application.targetFrameRate = 60;

        best = PlayerPrefs.GetInt("FlappyBest", 0);
        if (previewPipe != null) previewPipe.SetActive(false);

        if (sfx == null) sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        if (sfxMixerGroup != null) sfx.outputAudioMixerGroup = sfxMixerGroup;

        if (ground.sprite != null) groundTileWidth = ground.sprite.bounds.size.x;
        ground.drawMode = SpriteDrawMode.Tiled;

        if (tapArea != null) tapArea.Pressed += () => tapQueued = true;

        ResetGame();
    }

    // ------------------------------------------------------------------ loop

    void Update()
    {
        float dt = Time.deltaTime;

        // Ekranga bosish (TapArea) yoki klaviatura
        bool tap = tapQueued || KeyPressed();
        tapQueued = false;

        switch (state)
        {
            case State.Ready:
                birdX = HomeX;
                birdY = 0.5f + Mathf.Sin(Time.time * 6f) * 0.15f;
                birdAngle = 0f;
                ScrollWorld(dt);
                if (tap)
                {
                    state = State.Playing;
                    Flap();
                }
                break;

            case State.Playing:
                if (tap) Flap();
                birdVel = Mathf.Max(birdVel + gravity * dt, maxFallSpeed);
                birdY += birdVel * dt;
                if (birdY > CamSize - 0.3f) { birdY = CamSize - 0.3f; birdVel = Mathf.Min(birdVel, 0); }

                ScrollWorld(dt);
                UpdatePipes(dt);

                if (birdY - birdRadius <= GroundTop || HitsPipe())
                    Die();
                break;

            case State.Dead:
                deadTimer += dt;
                if (birdY - birdRadius > GroundTop)
                {
                    birdVel = Mathf.Max(birdVel + gravity * dt, maxFallSpeed);
                    birdY = Mathf.Max(birdY + birdVel * dt, GroundTop + birdRadius);
                }
                if (restartHint != null) restartHint.SetActive(deadTimer > 0.6f);
                if (tap && deadTimer > 0.6f) ResetGame();
                break;
        }

        if (state != State.Ready)
        {
            float target = Mathf.Clamp(birdVel * 5f, -90f, 25f);
            birdAngle = Mathf.Lerp(birdAngle, target, (birdVel > 0 ? 20f : 6f) * dt);
        }
        bird.position = new Vector3(birdX, birdY, bird.position.z);
        bird.rotation = Quaternion.Euler(0, 0, birdAngle);

        flash = Mathf.Max(0, flash - dt * 3f);
        if (flashImage != null) flashImage.color = new Color(1, 1, 1, flash);
        if (scoreText != null) scoreText.text = score.ToString();
    }

    bool KeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) return true;
        // TapArea bo'lmasa — ekran/sichqonchani to'g'ridan-to'g'ri o'qish
        if (tapArea == null)
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || (tapArea == null && Input.GetMouseButtonDown(0));
#endif
    }

    void Flap()
    {
        birdVel = flapVelocity;
        Play(flapClip, 0.6f);
    }

    void Play(AudioClip clip, float volume)
    {
        if (clip != null && sfx != null) sfx.PlayOneShot(clip, volume);
    }

    void ScrollWorld(float dt)
    {
        if (groundTileWidth > 0f)
        {
            groundOffset = (groundOffset + pipeSpeed * dt) % groundTileWidth;
            ground.size = new Vector2(Mathf.Ceil(HalfWidth * 2f / groundTileWidth + 3) * groundTileWidth, groundHeight);
            ground.transform.position = new Vector3(-groundOffset, GroundTop, ground.transform.position.z);
        }

        if (clouds == null) return;
        foreach (var c in clouds)
        {
            if (c == null) continue;
            c.position += Vector3.left * (pipeSpeed * 0.15f * dt);
            if (c.position.x < -HalfWidth - 3f)
                c.position = new Vector3(HalfWidth + 3f, Random.Range(0.5f, 4f), c.position.z);
        }
    }

    void UpdatePipes(float dt)
    {
        float spawnX = HalfWidth + pipeWidth;
        if (pipes.Count == 0)
            SpawnPipe(spawnX + 1f);
        else if (pipes[pipes.Count - 1].root.position.x < spawnX - pipeDistance)
            SpawnPipe(pipes[pipes.Count - 1].root.position.x + pipeDistance);

        for (int i = pipes.Count - 1; i >= 0; i--)
        {
            var p = pipes[i];
            p.root.position += Vector3.left * (pipeSpeed * dt);
            float x = p.root.position.x;

            if (!p.scored && x + pipeWidth * 0.5f < birdX)
            {
                p.scored = true;
                score++;
                Play(scoreClip, 0.5f);
            }
            if (x < -HalfWidth - pipeWidth * 2f)
            {
                Destroy(p.root.gameObject);
                pipes.RemoveAt(i);
            }
        }
    }

    void SpawnPipe(float x)
    {
        const float margin = 0.8f;
        float minY = GroundTop + gapSize * 0.5f + margin;
        float maxY = CamSize - gapSize * 0.5f - margin;
        float gapY = Random.Range(minY, maxY);

        var root = Instantiate(pipePrefab).transform;
        root.name = "Pipe";
        root.position = new Vector3(x, 0, 0);
        LayoutPipe(root, gapY, gapSize, pipeWidth, GroundTop, CamSize + 1f);
        pipes.Add(new Pipe { root = root, gapY = gapY });
    }

    /// <summary>Quvur bo'laklarini bo'shliq (gap) atrofida joylashtiradi. Editor ham shuni ishlatadi.</summary>
    public static void LayoutPipe(Transform root, float gapY, float gapSize, float pipeWidth, float groundTop, float topLimit)
    {
        float bottomTop = gapY - gapSize * 0.5f;
        float topBottom = gapY + gapSize * 0.5f;
        float hb = Mathf.Max(0.01f, bottomTop - groundTop);
        float ht = Mathf.Max(0.01f, topLimit - topBottom);
        // Tana sprite'i 1x1 birlik, qopqoq sprite'i 1x0.5 birlik
        Place(root, "Bottom", new Vector3(0, groundTop + hb * 0.5f, 0), new Vector3(pipeWidth, hb, 1));
        Place(root, "BottomCap", new Vector3(0, bottomTop - CapHeight * 0.5f, 0), new Vector3(pipeWidth + 0.2f, CapHeight / 0.5f, 1));
        Place(root, "Top", new Vector3(0, topBottom + ht * 0.5f, 0), new Vector3(pipeWidth, ht, 1));
        Place(root, "TopCap", new Vector3(0, topBottom + CapHeight * 0.5f, 0), new Vector3(pipeWidth + 0.2f, CapHeight / 0.5f, 1));
    }

    static void Place(Transform root, string child, Vector3 pos, Vector3 scale)
    {
        var t = root.Find(child);
        if (t == null) return;
        t.localPosition = pos;
        t.localScale = scale;
    }

    bool HitsPipe()
    {
        var c = new Vector2(birdX, birdY);
        float r = birdRadius;
        foreach (var p in pipes)
        {
            float x = p.root.position.x;
            float hw = (pipeWidth + 0.2f) * 0.5f;
            if (c.x + r < x - hw || c.x - r > x + hw) continue;

            float bottomTop = p.gapY - gapSize * 0.5f;
            float topBottom = p.gapY + gapSize * 0.5f;
            if (CircleRect(c, r, x - hw, -100f, x + hw, bottomTop)) return true;
            if (CircleRect(c, r, x - hw, topBottom, x + hw, 100f)) return true;
        }
        return false;
    }

    static bool CircleRect(Vector2 c, float r, float xMin, float yMin, float xMax, float yMax)
    {
        float nx = Mathf.Clamp(c.x, xMin, xMax);
        float ny = Mathf.Clamp(c.y, yMin, yMax);
        float dx = c.x - nx, dy = c.y - ny;
        return dx * dx + dy * dy < r * r;
    }

    void Die()
    {
        state = State.Dead;
        deadTimer = 0;
        flash = 1f;
        birdVel = Mathf.Min(birdVel, 2f);
        Play(hitClip, 0.8f);
        if (score > best)
        {
            best = score;
            PlayerPrefs.SetInt("FlappyBest", best);
            PlayerPrefs.Save();
        }
        if (gameOverScoreText != null) gameOverScoreText.text = "Hisob: " + score + "\nRekord: " + best;
        if (restartHint != null) restartHint.SetActive(false);
        SetPanels();
    }

    void ResetGame()
    {
        foreach (var p in pipes) Destroy(p.root.gameObject);
        pipes.Clear();
        score = 0;
        birdX = HomeX;
        birdY = 0.5f;
        birdVel = 0;
        birdAngle = 0;
        state = State.Ready;
        if (readyBestText != null) readyBestText.text = "Rekord: " + best;
        SetPanels();
    }

    void SetPanels()
    {
        if (readyPanel != null) readyPanel.SetActive(state == State.Ready);
        if (gameOverPanel != null) gameOverPanel.SetActive(state == State.Dead);
        if (scoreText != null) scoreText.gameObject.SetActive(state == State.Playing);
    }

    void LateUpdate()
    {
        // Ready -> Playing o'tishida panelni yangilash
        if (state == State.Playing && readyPanel != null && readyPanel.activeSelf) SetPanels();
    }
}
