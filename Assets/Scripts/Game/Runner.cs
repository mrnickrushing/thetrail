using TheTrail.Core;
using UnityEngine;
using Canvas = TheTrail.Core.Canvas;

namespace TheTrail.Game
{
    /// <summary>
    /// The whole Unity side of the game: owns the App, copies its 480x270 palette picture into a texture
    /// shown through the sharp-pixel shader, turns touches, mouse and keys into App input, and feeds the
    /// synth to the audio output. The scene is empty; Boot creates this at startup.
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
        // The studio logo shown over the first moments after launch, then faded out.
        Transform logo; Material logoMat; float logoT;
        const float LogoHold = 1.4f, LogoFade = 0.5f;

        // The palette as Unity colours, built once from Pal.Rgb.
        static readonly Color32[] Colours = BuildColours();

        static Color32[] BuildColours()
        {
            var c = new Color32[256];
            for (int i = 0; i < Pal.Count; i++) c[i] = new Color32(Pal.Rgb[i * 3], Pal.Rgb[i * 3 + 1], Pal.Rgb[i * 3 + 2], 255);
            for (int i = Pal.Count; i < 256; i++) c[i] = new Color32(255, 0, 255, 255);
            return c;
        }

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
            App.OpenUrl = Application.OpenURL;
            App.RequestReview = () =>
            {
#if UNITY_IOS && !UNITY_EDITOR
                if (UnityEngine.iOS.Device.RequestStoreReview()) return;
#endif
                Application.OpenURL(App.ReviewUrl);
            };
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = false;

            cam = gameObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(Pal.Rgb[0], Pal.Rgb[1], Pal.Rgb[2], 255);   // the ink, so bars beside the picture match it
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 10f;
            cam.useOcclusionCulling = false;
            cam.allowHDR = false; cam.allowMSAA = false;
            gameObject.AddComponent<AudioListener>();
            sampleRate = AudioSettings.outputSampleRate;

            tex = new Texture2D(Canvas.W, Canvas.H, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,   // the shader's sharp-bilinear sampling keeps pixels crisp
                wrapMode = TextureWrapMode.Clamp,
                name = "ArgonautsScreen",
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
            mat = new Material(Resources.Load<Shader>("Pixels")) { mainTexture = tex };
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var logoTex = Resources.Load<Texture2D>("UI/Brand/rushingtech");
            if (logoTex != null)
            {
                var lg = new GameObject("Studio logo");
                logo = lg.transform;
                logo.SetParent(transform, false);
                lg.AddComponent<MeshFilter>().sharedMesh = mesh;
                var lr = lg.AddComponent<MeshRenderer>();
                logoMat = new Material(Resources.Load<Shader>("Logo")) { mainTexture = logoTex };
                lr.sharedMaterial = logoMat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
            }

            App = new App(new UnityPlatform());
            Layout();
        }

        /// <summary>Fits the 16:9 screen inside the safe area, as large as possible.</summary>
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
            if (logo != null)
            {
                var lt = logoMat.mainTexture;
                float lh = Mathf.Min(h * 0.55f, w * 0.55f * lt.height / lt.width);
                logo.position = new Vector3(screenRect.center.x, screenRect.center.y, -1);
                logo.localScale = new Vector3(lh * lt.width / lt.height, lh, 1);
            }
        }

        void Update()
        {
            if (cam.pixelWidth != lastSize.x || cam.pixelHeight != lastSize.y) Layout();
            bool splash = false;
            if (logo != null)
            {
                // The splash: the logo over the ink, then it fades and the title shows from under it.
                logoT += Time.unscaledDeltaTime;
                float a = logoT < LogoHold ? 1f : 1f - (logoT - LogoHold) / LogoFade;
                if (a <= 0) { Destroy(logo.gameObject); Destroy(logoMat); logo = null; quad.gameObject.SetActive(true); }
                else
                {
                    splash = true;
                    logoMat.color = new Color(1, 1, 1, Mathf.Clamp01(a * 1.5f));
                    quad.gameObject.SetActive(logoT >= LogoHold);
                    if (logoT < LogoHold) return;
                }
            }
            if (!splash) ReadInput();
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
            App.HeldUp = Input.GetKey(KeyCode.UpArrow);
            App.HeldDown = Input.GetKey(KeyCode.DownArrow);
            App.HeldSpace = Input.GetKey(KeyCode.Space);
        }

        void Present()
        {
            var pal = Colours;
            var src = App.C.Px;
            for (int y = 0; y < Canvas.H; y++)
            {
                int s = y * Canvas.W, d = (Canvas.H - 1 - y) * Canvas.W;
                for (int x = 0; x < Canvas.W; x++) pixels[d + x] = pal[src[s + x]];
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
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
            var go = new GameObject("Argonauts '49");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }
    }
}
