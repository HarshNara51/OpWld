using UnityEngine;
using UnityEngine.UI;

// Darkens the edges of whatever UI is behind it - a vignette for menus,
// title screens, panels. Put it on a full-screen RawImage that sits ABOVE
// your background image (but below buttons/text). Generates its own
// texture, no art needed. Tweak values and it updates in the editor.
[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class UIVignette : MonoBehaviour
{
    [Tooltip("How dark the edges get (0 = none, 1 = fully black)")]
    [Range(0f, 1f)] [SerializeField] private float intensity = 0.75f;

    [Tooltip("How far from the center the darkening starts (0 = center, 1 = edges)")]
    [Range(0f, 1f)] [SerializeField] private float startRadius = 0.45f;

    [Tooltip("Softness of the fade - higher = smoother")]
    [Range(0.05f, 1.5f)] [SerializeField] private float softness = 0.6f;

    [SerializeField] private Color color = Color.black;

    private Texture2D texture;

    private void OnEnable() => Build();
    private void OnValidate() => Build();

    private void Build()
    {
        const int size = 256;
        if (texture == null)
        {
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
        }

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance from center: 0 in the middle, ~1 at the edges, ~1.41 in corners
                float dx = (x / (size - 1f)) * 2f - 1f;
                float dy = (y / (size - 1f)) * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float t = Mathf.Clamp01((d - startRadius) / Mathf.Max(0.01f, softness));
                t = t * t * (3f - 2f * t); // smooth curve
                pixels[y * size + x] = new Color(color.r, color.g, color.b, t * intensity);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        RawImage img = GetComponent<RawImage>();
        img.texture = texture;
        img.color = Color.white;
        img.raycastTarget = false; // never blocks clicks
    }

    private void OnDestroy()
    {
        if (texture != null)
        {
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
        }
    }
}
