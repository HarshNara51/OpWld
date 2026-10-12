using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Paint jobs of a drivable car. The respray shop gives the car one of its
// Paint Jobs at random (never the one it already has).
//
// SETUP: put this on the car's PREFAB and fill Paint Jobs - every scene then
// uses the same list. Cars without it still work: it adds itself to every
// car with VehicleInteraction when a scene loads, with a default colour list.
//
// PREVIEW: right-click the component -> "Preview Next Paint Job" to see the
// jobs on the car in the editor. Changing a colour updates the preview live.
// "Clear Preview" puts the factory paint back (nothing is saved either way).
//
// How it paints: it finds the paint colour in the car's texture (e.g. the
// red of Prometheus) and swaps only that. Stripes = the non-paint pixels
// that sit between/inside the paint (Prometheus' white, the racer's black).
// Wheels (parts named Wheel/Tire/Tyre/Rim) can get a rim colour of their
// own: the light greys of the wheel turn into it, the tyre stays black.
// Glass, tyres and lights stay as they are.
//
// The chosen job is SAVED per car (by name), so the car keeps it in every
// scene and after restarting the game.
public class CarPaint : MonoBehaviour
{
    [Serializable]
    public class PaintJob
    {
        public Color body = Color.red;
        [Tooltip("Leave the stripes their original colour")]
        public bool keepStripes;
        public Color stripes = Color.white;
        [Tooltip("Paint the rims too (tyres stay black)")]
        public bool paintWheels;
        public Color wheels = new Color(0.1f, 0.1f, 0.1f);

        public PaintJob() { }
        public PaintJob(Color body) { this.body = body; keepStripes = true; }
        public PaintJob(Color body, Color stripes) { this.body = body; this.stripes = stripes; }
    }

    [Tooltip("Untick to stop the respray shop painting this car")]
    [SerializeField] private bool allowRespray = true;

    [Tooltip("The car's original paint is one of the jobs the shop can give")]
    [SerializeField] private bool includeFactoryPaint = true;

    [Tooltip("The shop picks one of these at random. Empty = default colour list.")]
    [SerializeField] private PaintJob[] paintJobs = new PaintJob[0];

    [Tooltip("Save key - leave empty to use the car's name (same car = same name in every scene)")]
    [SerializeField] private string saveId = "";

    [Header("Paint detection (only if the auto guess is wrong)")]
    [Tooltip("Hue of the paint in the texture, 0-360 (red 0, yellow 50, green 120, blue 220). -1 = guess")]
    [SerializeField] private float paintHue = -1f;
    [Tooltip("How far (degrees) a pixel's hue may be from the paint hue and still count as paint")]
    [SerializeField] private float hueTolerance = 18f;
    [Tooltip("Pixels greyer than this are never paint (keeps stripes, tyres, chrome)")]
    [Range(0f, 1f)] [SerializeField] private float minSaturation = 0.35f;
    [Tooltip("Widest stripe, as a fraction of the texture width. Gaps in the paint narrower than this count as stripes.")]
    [Range(0f, 0.2f)] [SerializeField] private float maxStripeWidth = 0.1f;

    [Header("Wheels (only if the auto guess is wrong)")]
    [Tooltip("Wheel meshes. Empty = every part named Wheel / Tire / Tyre / Rim.")]
    [SerializeField] private Renderer[] wheelRenderers = new Renderer[0];
    [Tooltip("Grey parts of the wheel brighter than this are the rim (darker = tyre)")]
    [Range(0f, 1f)] [SerializeField] private float rimMinBrightness = 0.3f;

    private static readonly PaintJob[] DefaultJobs =
    {
        new PaintJob(new Color(0.75f, 0.06f, 0.06f)), // red
        new PaintJob(new Color(0.08f, 0.25f, 0.75f)), // blue
        new PaintJob(new Color(0.92f, 0.92f, 0.92f)), // white
        new PaintJob(new Color(0.07f, 0.07f, 0.08f)), // black
        new PaintJob(new Color(0.95f, 0.72f, 0.05f)), // yellow
        new PaintJob(new Color(0.10f, 0.50f, 0.20f)), // green
        new PaintJob(new Color(0.55f, 0.57f, 0.60f)), // silver
        new PaintJob(new Color(0.40f, 0.10f, 0.55f)), // purple
    };

    private const int Factory = -1;
    private const string SavedKeysKey = "CarPaintJob_AllKeys"; // every car that has a saved paint job

    public bool AllowRespray => allowRespray;

    private string Key => "CarPaintJob_" + (string.IsNullOrEmpty(saveId) ? CleanName(name) : saveId);
    private PaintJob[] Jobs => paintJobs != null && paintJobs.Length > 0 ? paintJobs : DefaultJobs;

    private readonly List<(Renderer renderer, int slot)> bodySlots = new List<(Renderer, int)>();
    private readonly List<(Renderer renderer, int slot)> wheelSlots = new List<(Renderer, int)>();
    private Texture2D originalTex;
    private string texProperty;
    private Texture2D paintedTex;
    private Texture2D wheelTex;     // wheels get their own copy when the rims are painted
    private MaterialPropertyBlock block;
    private Color32[] sourcePixels;
    private float[] paintMask;     // 0-1 per pixel: how much it is paint
    private float[] stripeShade;   // < 0 = not a stripe, else brightness factor for the stripe colour
    private float paintH = -1f;    // 0-1
    private float paintRefV;       // typical brightness of the paint
    private bool ready, failed;
    private int current = Factory;
    private int previewIndex = -2; // editor preview: -2 = off

    // ================= auto-add to every drivable car =================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (VehicleInteraction car in root.GetComponentsInChildren<VehicleInteraction>(true))
                For(car.gameObject);
    }

    // The car's CarPaint, added if it doesn't have one yet
    public static CarPaint For(GameObject car)
    {
        CarPaint p = car.GetComponent<CarPaint>();
        return p != null ? p : car.AddComponent<CarPaint>();
    }

    private void Start()
    {
        int saved = PlayerPrefs.GetInt(Key, Factory);
        if (saved != Factory && saved < Jobs.Length) Apply(saved);
    }

    private void OnDestroy()
    {
        if (paintedTex != null) SafeDestroy(paintedTex);
        if (wheelTex != null) SafeDestroy(wheelTex);
    }

    // Every car back to its factory paint (New Game). Cars already in the scene keep their look until reloaded.
    public static void ResetAllSaved()
    {
        foreach (string key in PlayerPrefs.GetString(SavedKeysKey, "").Split('|'))
            if (key.Length > 0) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.DeleteKey(SavedKeysKey);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Car Paint/Reset Saved Paint (factory colours)")]
    private static void ResetSavedMenu()
    {
        ResetAllSaved();
        Debug.Log("Car Paint: saved paint jobs cleared - every car starts in its factory paint next time you press Play.");
    }
#endif

    // ================= respray =================

    // A different paint job at random, saved for every scene. False if it can't be painted.
    public bool Respray()
    {
        if (!allowRespray || !Prepare()) return false;

        var choices = new List<int>();
        if (includeFactoryPaint) choices.Add(Factory);
        for (int i = 0; i < Jobs.Length; i++) choices.Add(i);
        choices.Remove(current);
        if (choices.Count == 0) return false;

        int pick = choices[UnityEngine.Random.Range(0, choices.Count)];
        if (!Apply(pick)) return false;

        PlayerPrefs.SetInt(Key, pick);
        string keys = PlayerPrefs.GetString(SavedKeysKey, "");
        if (Array.IndexOf(keys.Split('|'), Key) < 0)
            PlayerPrefs.SetString(SavedKeysKey, keys.Length == 0 ? Key : keys + "|" + Key);
        PlayerPrefs.Save();
        return true;
    }

    private bool Apply(int job)
    {
        if (!Prepare()) return false;
        current = job;

        if (job == Factory)
        {
            SetTexture(bodySlots, null);
            SetTexture(wheelSlots, null);
            return true;
        }

        PaintJob paintJob = Jobs[job];
        Color32[] pixels = Recolor(paintJob);
        Upload(ref paintedTex, pixels, "_Paint");
        SetTexture(bodySlots, paintedTex);

        if (paintJob.paintWheels)
        {
            Upload(ref wheelTex, RecolorRims(pixels, paintJob.wheels), "_Wheels");
            SetTexture(wheelSlots, wheelTex);
        }
        else SetTexture(wheelSlots, paintedTex);
        return true;
    }

    private void Upload(ref Texture2D tex, Color32[] pixels, string suffix)
    {
        if (tex == null)
        {
            tex = new Texture2D(originalTex.width, originalTex.height, TextureFormat.RGBA32, true)
            {
                name = originalTex.name + suffix,
                filterMode = originalTex.filterMode,
                wrapMode = originalTex.wrapMode,
                anisoLevel = originalTex.anisoLevel,
                hideFlags = HideFlags.DontSave,
            };
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
    }

    // Swap the texture only on these paint slots (the shared material is untouched)
    private void SetTexture(List<(Renderer renderer, int slot)> slots, Texture tex)
    {
        if (block == null) block = new MaterialPropertyBlock();
        foreach (var (r, slot) in slots)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block, slot);
            if (tex != null) block.SetTexture(texProperty, tex);
            else block.Clear();
            r.SetPropertyBlock(block, slot);
        }
    }

    private Color32[] Recolor(PaintJob job)
    {
        var result = new Color32[sourcePixels.Length];
        for (int i = 0; i < sourcePixels.Length; i++)
        {
            Color32 p = sourcePixels[i];
            if (!job.keepStripes && stripeShade[i] >= 0f)
            {
                result[i] = Shaded(job.stripes, stripeShade[i], p.a);
                continue;
            }
            float w = paintMask[i];
            if (w <= 0f) { result[i] = p; continue; }

            Color.RGBToHSV(p, out _, out _, out float v);
            result[i] = Color.Lerp(p, Shaded(job.body, v / paintRefV, p.a), w);
        }
        return result;
    }

    // Wheel copy of the texture: its light greys (the rims) become the wheel colour
    private Color32[] RecolorRims(Color32[] pixels, Color color)
    {
        var result = new Color32[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            Color.RGBToHSV(sourcePixels[i], out _, out float s, out float v);
            float w = Mathf.InverseLerp(0.3f, 0.15f, s) * Mathf.InverseLerp(rimMinBrightness - 0.05f, rimMinBrightness + 0.05f, v);
            result[i] = w <= 0f ? p : Color.Lerp(p, Shaded(color, v / 0.7f, p.a), w);
        }
        return result;
    }

    // Keep the shading: darker parts stay darker in the new colour
    private static Color Shaded(Color c, float shade, byte alpha)
    {
        float lift = Mathf.Max(0f, shade - 1f) * 0.5f;
        return new Color(Mathf.Clamp01(c.r * shade + lift), Mathf.Clamp01(c.g * shade + lift),
                         Mathf.Clamp01(c.b * shade + lift), alpha / 255f);
    }

    // ================= find the paint (once) =================

    private bool Prepare()
    {
        if (ready) return true;
        if (failed) return false;

        // Biggest visible mesh = the body; its biggest part = the paint material
        Renderer body = null;
        float biggest = 0f;
        var renderers = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            renderers.Add(r);
            float size = r.bounds.size.sqrMagnitude;
            if (size > biggest) { biggest = size; body = r; }
        }
        if (body == null) return Fail("no body mesh");

        Material paintMat = body.sharedMaterials[BiggestSubMesh(body)];
        if (paintMat == null) return Fail("no paint material");

        originalTex = paintMat.mainTexture as Texture2D;
        if (originalTex == null) return Fail($"paint material '{paintMat.name}' has no texture");
        texProperty = paintMat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";

        // Every part using the same material gets the new texture; wheels can get their own
        bodySlots.Clear();
        wheelSlots.Clear();
        foreach (Renderer r in renderers)
        {
            List<(Renderer, int)> list = IsWheel(r) ? wheelSlots : bodySlots;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == paintMat) list.Add((r, i));
        }

        sourcePixels = ReadPixels(originalTex);
        if (!FindPaint()) return Fail("couldn't find a paint colour in the texture");
        FindStripes(originalTex.width, originalTex.height);

        ready = true;
        return true;
    }

    private bool IsWheel(Renderer r)
    {
        if (wheelRenderers != null && wheelRenderers.Length > 0)
            return Array.IndexOf(wheelRenderers, r) >= 0;
        string n = r.name.ToLowerInvariant();
        return n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre") || n.Contains("rim");
    }

    private static int BiggestSubMesh(Renderer r)
    {
        Mesh mesh = null;
        if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
        else if (r.TryGetComponent(out MeshFilter mf)) mesh = mf.sharedMesh;

        int best = 0;
        if (mesh != null)
        {
            uint most = 0;
            int count = Mathf.Min(mesh.subMeshCount, r.sharedMaterials.Length);
            for (int i = 0; i < count; i++)
                if (mesh.GetIndexCount(i) > most) { most = mesh.GetIndexCount(i); best = i; }
        }
        return best;
    }

    // Copy any texture (even ones not marked Read/Write) through the GPU
    private static Color32[] ReadPixels(Texture2D tex)
    {
        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;

        var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        copy.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] pixels = copy.GetPixels32();
        SafeDestroy(copy);
        return pixels;
    }

    // The paint is the strongest, most common colourful hue in the texture
    private bool FindPaint()
    {
        if (paintHue >= 0f) paintH = Mathf.Repeat(paintHue, 360f) / 360f;
        else
        {
            const int bins = 36;
            var weight = new float[bins];
            foreach (Color32 p in sourcePixels)
            {
                Color.RGBToHSV(p, out float h, out float s, out float v);
                if (s < minSaturation || v < 0.12f) continue;
                weight[Mathf.Min(bins - 1, (int)(h * bins))] += s * v * v;
            }

            int best = -1;
            float bestW = 0f;
            for (int i = 0; i < bins; i++)
            {
                // Count neighbours too, so a gradient across two bins still wins
                float w = weight[i] + 0.5f * (weight[(i + 1) % bins] + weight[(i + bins - 1) % bins]);
                if (w > bestW) { bestW = w; best = i; }
            }
            if (best < 0) return false;
            paintH = (best + 0.5f) / bins;
        }

        // Typical paint pixel, refined once around the average hue
        float tol = hueTolerance / 360f;
        for (int pass = 0; pass < 2; pass++)
        {
            float sumV = 0f, hueX = 0f, hueY = 0f;
            int n = 0;
            foreach (Color32 p in sourcePixels)
            {
                Color.RGBToHSV(p, out float h, out float s, out float v);
                if (s < minSaturation || v < 0.12f || HueDistance(h, paintH) > tol) continue;
                sumV += v;
                hueX += Mathf.Cos(h * Mathf.PI * 2f);
                hueY += Mathf.Sin(h * Mathf.PI * 2f);
                n++;
            }
            if (n == 0) return false;
            if (paintHue < 0f) paintH = Mathf.Repeat(Mathf.Atan2(hueY, hueX) / (Mathf.PI * 2f), 1f);
            paintRefV = Mathf.Max(0.05f, sumV / n);
        }

        // Soft mask: 1 = clearly paint, 0 = leave alone
        paintMask = new float[sourcePixels.Length];
        for (int i = 0; i < sourcePixels.Length; i++)
        {
            Color.RGBToHSV(sourcePixels[i], out float h, out float s, out float v);
            paintMask[i] = Mathf.InverseLerp(tol, tol * 0.6f, HueDistance(h, paintH))
                         * Mathf.InverseLerp(minSaturation - 0.1f, minSaturation + 0.1f, s)
                         * Mathf.InverseLerp(0.04f, 0.12f, v);
        }
        return true;
    }

    // Stripes = gaps in the paint narrower than maxStripeWidth (a morphological "close")
    private void FindStripes(int w, int h)
    {
        int n = w * h;
        var paint = new bool[n];
        for (int i = 0; i < n; i++) paint[i] = paintMask[i] > 0.5f;

        int r = Mathf.Max(2, Mathf.RoundToInt(w * maxStripeWidth * 0.5f));
        bool[] closed = Erode(Dilate(paint, w, h, r), w, h, r);

        stripeShade = new float[n];
        for (int i = 0; i < n; i++)
        {
            stripeShade[i] = -1f;
            if (!closed[i] || paintMask[i] > 0.1f) continue;

            // Light the stripe like the paint next to it (works for white AND black stripes)
            int x = i % w, y = i / w;
            float v = NearestPaintV(x, y, w, h, r, paint);
            stripeShade[i] = v / paintRefV;
        }
    }

    private float NearestPaintV(int x, int y, int w, int h, int r, bool[] paint)
    {
        for (int d = 1; d <= r; d++)
        {
            int[] xs = { x - d, x + d, x, x };
            int[] ys = { y, y, y - d, y + d };
            for (int k = 0; k < 4; k++)
            {
                if (xs[k] < 0 || xs[k] >= w || ys[k] < 0 || ys[k] >= h) continue;
                int j = ys[k] * w + xs[k];
                if (!paint[j]) continue;
                Color.RGBToHSV(sourcePixels[j], out _, out _, out float v);
                return v;
            }
        }
        return paintRefV;
    }

    private static bool[] Dilate(bool[] m, int w, int h, int r) => Box(m, w, h, r, true);
    private static bool[] Erode(bool[] m, int w, int h, int r) => Box(m, w, h, r, false);

    // Square dilate (any set pixel within r) or erode (all pixels within r set), done row then column
    private static bool[] Box(bool[] m, int w, int h, int r, bool dilate)
    {
        bool[] Pass(bool[] src, bool horizontal)
        {
            var dst = new bool[src.Length];
            int lines = horizontal ? h : w, len = horizontal ? w : h;
            for (int line = 0; line < lines; line++)
            {
                int Idx(int k) => horizontal ? line * w + k : k * w + line;
                int count = 0; // set pixels in the window
                for (int k = 0; k < Mathf.Min(r, len); k++) if (src[Idx(k)]) count++;
                for (int k = 0; k < len; k++)
                {
                    int add = k + r, remove = k - r - 1;
                    if (add < len && src[Idx(add)]) count++;
                    if (remove >= 0 && src[Idx(remove)]) count--;
                    int window = Mathf.Min(len - 1, k + r) - Mathf.Max(0, k - r) + 1;
                    dst[Idx(k)] = dilate ? count > 0 : count == window;
                }
            }
            return dst;
        }
        return Pass(Pass(m, true), false);
    }

    private bool Fail(string why)
    {
        Debug.LogWarning($"CarPaint on {name}: can't paint - {why}.");
        failed = true; // don't try again
        return false;
    }

    // ================= editor preview =================

    [ContextMenu("Preview Next Paint Job")]
    private void PreviewNext()
    {
        Forget();
        if (!Prepare()) return;
        int first = includeFactoryPaint ? Factory : 0;
        previewIndex = previewIndex < first || previewIndex + 1 >= Jobs.Length ? first : previewIndex + 1;
        Apply(previewIndex);
        Debug.Log(previewIndex == Factory
            ? $"{name}: previewing factory paint"
            : $"{name}: previewing paint job {previewIndex} (Element {previewIndex} in Paint Jobs)");
    }

    [ContextMenu("Clear Preview")]
    private void ClearPreview()
    {
        previewIndex = -2;
        if (!Prepare()) return;
        SetTexture(bodySlots, null);
        SetTexture(wheelSlots, null);
    }

    // Forget what was worked out, so changed detection settings take effect
    private void Forget()
    {
        if (Application.isPlaying) return;
        ready = failed = false;
    }

#if UNITY_EDITOR
    // Live preview: editing a colour repaints the car (edit mode only)
    private void OnValidate()
    {
        if (Application.isPlaying || previewIndex == -2) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || previewIndex == -2) return;
            Forget();
            if (previewIndex >= Jobs.Length) previewIndex = includeFactoryPaint ? Factory : 0;
            Apply(previewIndex);
        };
    }
#endif

    // ================= helpers =================

    private static void SafeDestroy(UnityEngine.Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    private static float HueDistance(float a, float b)
    {
        float d = Mathf.Abs(a - b);
        return Mathf.Min(d, 1f - d);
    }

    // "Prometheus (1)" -> "Prometheus"
    private static string CleanName(string n)
    {
        int i = n.IndexOf(" (", StringComparison.Ordinal);
        return i > 0 ? n.Substring(0, i) : n;
    }
}
