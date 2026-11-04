using TheTrail.Core;
using UnityEngine;
using Canvas = TheTrail.Core.Canvas;

namespace TheTrail.Game
{
    /// <summary>
    /// The whole Unity side of the game: owns the App, copies its 384x240 picture into a texture shown
    /// through the CRT shader, turns touches, mouse and keys into App input, and feeds the beeper to
    /// the audio output. The scene is empty; Boot creates this at startup.
    /// </summary>
    public sealed class Runner : MonoBehaviour
    {
        public static Runner Instance { get; private set; }
        public App App { get; private set; }

        Texture2D tex;
        Color32[] pixels;
        Material mat;
        Transform quad;
        Camera cam;
        Rect screenRect;
        int sampleRate;
        Vector2Int lastSize;

        // Phosphor palettes: index 0 is the glass, 5 the hottest highlight.
        static readonly Color32[][] Palettes =
        {
            new[] { C(5, 12, 7), C(14, 54, 26), C(26, 104, 46), C(46, 168, 74), C(84, 238, 114), C(206, 255, 210) },     // green P1
            new[] { C(12, 7, 2), C(60, 32, 6), C(118, 66, 10), C(188, 112, 18), C(255, 176, 36), C(255, 232, 170) },      // amber P3
            new[] { C(8, 9, 11), C(46, 50, 56), C(96, 102, 110), C(160, 166, 174), C(222, 228, 234), C(255, 255, 255) },  // white P4
        };

        static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        sealed class UnityPlatform : IPlatform
        {
            public string Load(string key) => PlayerPrefs.GetString(key, "");
            public void Save(string key, string value) { PlayerPrefs.SetString(key, value); PlayerPrefs.Save(); }
            public string ToJson(object o) => JsonUtility.ToJson(o);
            public T FromJson<T>(string json) => JsonUtility.FromJson<T>(json);
            public bool Touch => Application.isMobilePlatform;
        }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = false;

            cam = gameObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 10f;
            cam.useOcclusionCulling = false;
            cam.allowHDR = false; cam.allowMSAA = false;
            gameObject.AddComponent<AudioListener>();
            sampleRate = AudioSettings.outputSampleRate;

            tex = new Texture2D(Canvas.W, Canvas.H, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,   // the shader's sharp-bilinear sampling keeps pixels crisp
                wrapMode = TextureWrapMode.Clamp,
                name = "TrailScreen",
            };
            pixels = new Color32[Canvas.W * Canvas.H];

            // A unit quad facing the camera; Layout() scales it to the screen rectangle.
            var go = new GameObject("Screen");
            quad = go.transform;
            quad.SetParent(transform, false);
            var mesh = new Mesh { name = "ScreenQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1, 1, 1));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mat = new Material(Resources.Load<Shader>("Crt")) { mainTexture = tex };
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            App = new App(new UnityPlatform());
            Layout();
        }

        /// <summary>Fits the 8:5 screen inside the safe area, as large as possible.</summary>
        void Layout()
        {
            // The camera's real pixel size (on a device the same as Screen; in the Editor's Game view it can differ).
            int sw = cam.pixelWidth, sh = cam.pixelHeight;
            lastSize = new Vector2Int(sw, sh);
            var safe = Screen.safeArea;
            if (Screen.width != sw || Screen.height != sh || safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, sw, sh);
            float margin = Mathf.Round(Mathf.Min(safe.width, safe.height) * 0.02f);
            safe = new Rect(safe.x + margin, safe.y + margin, safe.width - margin * 2, safe.height - margin * 2);
            float scale = Mathf.Min(safe.width / Canvas.W, safe.height / Canvas.H);
            float w = Canvas.W * scale, h = Canvas.H * scale;
            screenRect = new Rect(safe.center.x - w / 2, safe.center.y - h / 2, w, h);

            cam.orthographicSize = sh / 2f;
            transform.position = new Vector3(sw / 2f, sh / 2f, -5);
            quad.position = new Vector3(screenRect.center.x, screenRect.center.y, 0);
            quad.localScale = new Vector3(w, h, 1);
            mat.SetVector("_Res", new Vector4(Canvas.W, Canvas.H, 0, 0));
            mat.SetVector("_Px", new Vector4(w, h, 0, 0));
        }

        void Update()
        {
            if (cam.pixelWidth != lastSize.x || cam.pixelHeight != lastSize.y) Layout();
            ReadInput();
            App.Tick(Time.unscaledDeltaTime);
            App.Draw();
            Present();
        }

        Vector2Int ToVirtual(Vector2 p)
        {
            float x = (p.x - screenRect.x) / screenRect.width * Canvas.W;
            float y = (1f - (p.y - screenRect.y) / screenRect.height) * Canvas.H;
            return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(x), 0, Canvas.W - 1), Mathf.Clamp(Mathf.FloorToInt(y), 0, Canvas.H - 1));
        }

        void ReadInput()
        {
            // Taps: a new touch, or a mouse click.
            bool down = false; Vector2 pos = default;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                pos = t.position;
                down = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                if (t.phase == TouchPhase.Began) { var v = ToVirtual(t.position); App.Tap(v.x, v.y); }
            }
            else if (Input.mousePresent)
            {
                pos = Input.mousePosition;
                down = Input.GetMouseButton(0);
                if (Input.GetMouseButtonDown(0)) { var v = ToVirtual(pos); App.Tap(v.x, v.y); }
            }
            var pv = ToVirtual(pos);
            App.PointerDown = down; App.PointerX = pv.x; App.PointerY = pv.y;

            // Keys: typed characters, then the special keys.
            foreach (char ch in Input.inputString)
            {
                if (ch == '\r' || ch == '\n') App.Key(Keys.Enter);
                else if (ch == '\b') App.Key(Keys.Back);
                else if (ch >= ' ' && ch < 127) App.Key(ch);
            }
            if (Input.GetKeyDown(KeyCode.Escape)) App.Key(Keys.Escape);
            if (Input.GetKeyDown(KeyCode.UpArrow)) App.Key(Keys.Up);
            if (Input.GetKeyDown(KeyCode.DownArrow)) App.Key(Keys.Down);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) App.Key(Keys.Left);
            if (Input.GetKeyDown(KeyCode.RightArrow)) App.Key(Keys.Right);
            App.HeldLeft = Input.GetKey(KeyCode.LeftArrow);
            App.HeldRight = Input.GetKey(KeyCode.RightArrow);
        }

        void Present()
        {
            var pal = Palettes[Mathf.Clamp(App.Settings.monitor, 0, Palettes.Length - 1)];
            var src = App.C.Px;
            for (int y = 0; y < Canvas.H; y++)
            {
                int s = y * Canvas.W, d = (Canvas.H - 1 - y) * Canvas.W;
                for (int x = 0; x < Canvas.W; x++) pixels[d + x] = pal[src[s + x]];
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
            mat.SetFloat("_Crt", App.Settings.crt ? 1f : 0f);
        }

        // The beeper mixes into the listener's output on the audio thread.
        void OnAudioFilterRead(float[] data, int channels)
        {
            var app = App;
            if (app == null) return;
            app.Synth.Render(data, channels, sampleRate);
        }

        void OnApplicationPause(bool paused) { if (paused) App?.SaveGame(); }
        void OnApplicationQuit() => App?.SaveGame();
    }

    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Runner.Instance != null) return;
            var go = new GameObject("The Trail");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }
    }
}
