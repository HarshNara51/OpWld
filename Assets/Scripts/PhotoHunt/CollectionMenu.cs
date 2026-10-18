using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The "Collection" screen: every photo-hunt item as a slot. Found items show the first photo
// taken of them (PhotoCollectionManager keeps one picture per item, so retakes never add
// duplicates); the rest show "???". Click a photo to view it full screen - scroll to zoom,
// drag to pan, right click or the X to close. Built in code on its own overlay canvas when
// opened (CollectionButton), and destroyed again on close, so it costs nothing otherwise.
public class CollectionMenu : MonoBehaviour
{
    private static readonly Color Amber = new Color(1f, 0.72f, 0.3f);
    private static readonly Color Plate = new Color(0.04f, 0.04f, 0.06f, 1f);

    private TMP_FontAsset titleFont, bodyFont;
    private Sprite uiSprite;
    private readonly List<Texture2D> loaded = new List<Texture2D>();
    private GameObject viewer;

    public static CollectionMenu Open(TMP_FontAsset titleFont, TMP_FontAsset bodyFont, Sprite uiSprite)
    {
        var go = new GameObject("CollectionMenu", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // above the pause / settings menus
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        var menu = go.AddComponent<CollectionMenu>();
        menu.titleFont = titleFont; menu.bodyFont = bodyFont; menu.uiSprite = uiSprite;
        menu.Build();
        return menu;
    }

    public void Close()
    {
        foreach (var t in loaded) if (t != null) Destroy(t);
        loaded.Clear();
        Destroy(gameObject);
    }

    // ---------------- building ----------------

    private RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private Image Panel(RectTransform r, Color c)
    {
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = uiSprite; img.type = Image.Type.Sliced; img.color = c;
        return img;
    }

    private TextMeshProUGUI Text(RectTransform r, TMP_FontAsset font, float size, Color c, TextAlignmentOptions align, string text)
    {
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSize = size; t.color = c; t.alignment = align; t.text = text; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    private Button TextButton(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var r = Rect(label + "Button", parent);
        r.anchorMin = r.anchorMax = anchor; r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = size;
        var img = Panel(r, new Color(0.1f, 0.1f, 0.12f, 1f));
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors; cb.highlightedColor = new Color(1f, 0.85f, 0.55f); cb.pressedColor = Amber; b.colors = cb;
        b.onClick.AddListener(onClick);
        var t = Rect("Label", r); t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one; t.offsetMin = t.offsetMax = Vector2.zero;
        var tx = Text(t, bodyFont, 26f, Amber, TextAlignmentOptions.Center, label); tx.fontStyle = FontStyles.Bold;
        return b;
    }

    private void Build()
    {
        var hunt = PhotoCollectionManager.Instance;
        var items = hunt != null ? hunt.Items : new PhotoCollectionManager.Item[0];

        // Dim the game / menu behind
        var dim = Rect("Dim", transform); dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.offsetMin = dim.offsetMax = Vector2.zero;
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);

        var card = Rect("Card", transform);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f); card.sizeDelta = new Vector2(1560f, 960f);
        Panel(card, Plate);
        var bar = Rect("AccentBar", card); bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = new Vector2(1f, 1f); bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(-48f, 5f); bar.anchoredPosition = new Vector2(0f, -18f);
        bar.gameObject.AddComponent<Image>().color = Amber;

        var title = Rect("Title", card); title.anchorMin = new Vector2(0f, 1f); title.anchorMax = new Vector2(1f, 1f); title.pivot = new Vector2(0.5f, 1f);
        title.sizeDelta = new Vector2(0f, 100f); title.anchoredPosition = new Vector2(0f, -30f);
        Text(title, titleFont, 92f, Amber, TextAlignmentOptions.Center, "COLLECTION").characterSpacing = 10f;

        TextButton(card, "BACK", new Vector2(1f, 1f), new Vector2(-30f, -34f), new Vector2(170f, 60f), Close);

        // Grid of slots
        const int cols = 6; const float slotW = 226f, slotH = 127f, capH = 44f, gapX = 18f, gapY = 8f;
        float startY = -140f;
        int found = 0;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            int c = i % cols, row = i / cols;
            int inRow = Mathf.Min(cols, items.Length - row * cols);              // centre a short last row
            float rowW = inRow * slotW + (inRow - 1) * gapX;
            float x = -rowW * 0.5f + c * (slotW + gapX) + slotW * 0.5f;
            float y = startY - row * (slotH + capH + gapY);

            var slot = Rect("Slot_" + item.id, card);
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 1f); slot.pivot = new Vector2(0.5f, 1f);
            slot.sizeDelta = new Vector2(slotW, slotH); slot.anchoredPosition = new Vector2(x, y);
            var frame = Panel(slot, new Color(0.11f, 0.11f, 0.14f, 1f));

            bool have = hunt != null && hunt.IsCollected(item.id);
            string path = hunt != null ? hunt.CollectionImagePath(item.id) : null;
            Texture2D tex = have && path != null && File.Exists(path) ? LoadPicture(path) : null;
            string label = string.IsNullOrEmpty(item.displayName) ? item.id : item.displayName;

            if (tex != null)
            {
                found++;
                var pic = Rect("Photo", slot); pic.anchorMin = Vector2.zero; pic.anchorMax = Vector2.one; pic.offsetMin = new Vector2(5f, 5f); pic.offsetMax = new Vector2(-5f, -5f);
                var raw = pic.gameObject.AddComponent<RawImage>(); raw.texture = tex; raw.raycastTarget = false;
                FitCrop(raw, tex, pic.rect.size.x > 0 ? pic.rect.size : new Vector2(slotW - 10f, slotH - 10f));
                var btn = slot.gameObject.AddComponent<Button>(); btn.targetGraphic = frame;
                var cb = btn.colors; cb.highlightedColor = Amber; cb.pressedColor = Amber; btn.colors = cb;
                Texture2D t = tex; string l = label;
                btn.onClick.AddListener(() => OpenViewer(t, l));
            }
            else
            {
                if (have) found++; // collected before the Collection existed - counted, but no picture to show
                var q = Rect("Unknown", slot); q.anchorMin = Vector2.zero; q.anchorMax = Vector2.one; q.offsetMin = q.offsetMax = Vector2.zero;
                Text(q, have ? bodyFont : titleFont, have ? 26f : 64f, have ? new Color(0.6f, 0.75f, 0.6f) : new Color(0.35f, 0.35f, 0.4f), TextAlignmentOptions.Center, have ? "FOUND" : "???");
            }

            var cap = Rect("Caption", card); cap.anchorMin = cap.anchorMax = new Vector2(0.5f, 1f); cap.pivot = new Vector2(0.5f, 1f);
            cap.sizeDelta = new Vector2(slotW, capH); cap.anchoredPosition = new Vector2(x, y - slotH - 2f);
            var ct = Text(cap, bodyFont, 19f, have ? new Color(0.93f, 0.91f, 0.87f) : new Color(0.45f, 0.45f, 0.5f), TextAlignmentOptions.Center, have ? label : "???");
            ct.textWrappingMode = TextWrappingModes.Normal; ct.overflowMode = TextOverflowModes.Ellipsis; // long names: two lines, shrink a little
            ct.enableAutoSizing = true; ct.fontSizeMin = 14f; ct.fontSizeMax = 19f; ct.alignment = TextAlignmentOptions.Top; ct.lineSpacing = -12f;
        }

        // Footer
        int total = items.Length;
        int captured = hunt != null ? hunt.CollectedCount : found;
        var foot = Rect("Footer", card); foot.anchorMin = new Vector2(0f, 0f); foot.anchorMax = new Vector2(1f, 0f); foot.pivot = new Vector2(0.5f, 0f);
        foot.sizeDelta = new Vector2(0f, 44f); foot.anchoredPosition = new Vector2(0f, 26f);
        var ft = Text(foot, bodyFont, 28f, new Color(0.93f, 0.91f, 0.87f), TextAlignmentOptions.Center,
            $"Photos captured - <color=#FFB84D><b>{captured}</b></color>      |      Photos remaining - <color=#FFB84D><b>{Mathf.Max(0, total - captured)}</b></color>");
        var hint = Rect("Hint", card); hint.anchorMin = new Vector2(0f, 0f); hint.anchorMax = new Vector2(1f, 0f); hint.pivot = new Vector2(0.5f, 0f);
        hint.sizeDelta = new Vector2(0f, 26f); hint.anchoredPosition = new Vector2(0f, 76f);
        Text(hint, bodyFont, 18f, new Color(0.55f, 0.55f, 0.6f), TextAlignmentOptions.Center, captured > 0 ? "Click a photo to view it" : "Find and photograph the hidden things around the world");
    }

    private Texture2D LoadPicture(string path)
    {
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, true);
            if (!tex.LoadImage(File.ReadAllBytes(path))) { Destroy(tex); return null; }
            tex.wrapMode = TextureWrapMode.Clamp;
            loaded.Add(tex);
            return tex;
        }
        catch (System.Exception) { return null; }
    }

    // Fill the box without stretching: crop the photo's edges to the box's shape
    private static void FitCrop(RawImage raw, Texture2D tex, Vector2 box)
    {
        float texAspect = tex.width / (float)tex.height, boxAspect = box.x / box.y;
        if (texAspect > boxAspect) { float w = boxAspect / texAspect; raw.uvRect = new UnityEngine.Rect((1f - w) * 0.5f, 0f, w, 1f); }
        else { float h = texAspect / boxAspect; raw.uvRect = new UnityEngine.Rect(0f, (1f - h) * 0.5f, 1f, h); }
    }

    // ---------------- full-screen viewer ----------------

    private void OpenViewer(Texture2D tex, string label)
    {
        if (viewer != null) Destroy(viewer);
        var root = Rect("Viewer", transform); root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        viewer = root.gameObject;
        root.gameObject.AddComponent<Image>().color = Color.black;

        var area = Rect("Area", root); area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = new Vector2(60f, 110f); area.offsetMax = new Vector2(-60f, -90f);
        area.gameObject.AddComponent<RectMask2D>();
        var pic = Rect("Photo", area); pic.anchorMin = pic.anchorMax = new Vector2(0.5f, 0.5f);
        float maxW = 1800f, maxH = 880f, aspect = tex.width / (float)tex.height;
        pic.sizeDelta = aspect > maxW / maxH ? new Vector2(maxW, maxW / aspect) : new Vector2(maxH * aspect, maxH);
        var raw = pic.gameObject.AddComponent<RawImage>(); raw.texture = tex;
        var zoom = area.gameObject.AddComponent<CollectionZoom>(); zoom.Init(pic, this);
        area.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0); // catches drags / scroll
        pic.SetAsLastSibling();

        var cap = Rect("Caption", root); cap.anchorMin = new Vector2(0f, 0f); cap.anchorMax = new Vector2(1f, 0f); cap.pivot = new Vector2(0.5f, 0f);
        cap.sizeDelta = new Vector2(0f, 50f); cap.anchoredPosition = new Vector2(0f, 40f);
        Text(cap, bodyFont, 30f, Amber, TextAlignmentOptions.Center, label).fontStyle = FontStyles.Bold;
        var help = Rect("Help", root); help.anchorMin = new Vector2(0f, 1f); help.anchorMax = new Vector2(1f, 1f); help.pivot = new Vector2(0.5f, 1f);
        help.sizeDelta = new Vector2(0f, 30f); help.anchoredPosition = new Vector2(0f, -30f);
        Text(help, bodyFont, 18f, new Color(0.6f, 0.6f, 0.65f), TextAlignmentOptions.Center, "Scroll to zoom  -  drag to move  -  right click to close");
        TextButton(root, "X", new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(70f, 60f), CloseViewer);
    }

    public void CloseViewer()
    {
        if (viewer != null) Destroy(viewer);
        viewer = null;
    }
}

// Zoom (mouse wheel, toward the cursor) and drag-to-pan for the Collection's full-screen viewer.
public class CollectionZoom : MonoBehaviour, IDragHandler, IScrollHandler, IPointerClickHandler
{
    private RectTransform pic;
    private CollectionMenu menu;
    private float zoom = 1f;

    public void Init(RectTransform picture, CollectionMenu owner) { pic = picture; menu = owner; }

    public void OnScroll(PointerEventData e)
    {
        float old = zoom;
        zoom = Mathf.Clamp(zoom * (e.scrollDelta.y > 0 ? 1.15f : 1f / 1.15f), 1f, 5f);
        // keep the point under the cursor in place
        RectTransformUtility.ScreenPointToLocalPointInRectangle(pic, e.position, e.pressEventCamera, out Vector2 local);
        pic.localScale = Vector3.one * zoom;
        pic.anchoredPosition -= local * (zoom - old);
        Clamp();
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || zoom <= 1.001f) return;
        var canvas = GetComponentInParent<Canvas>();
        pic.anchoredPosition += e.delta / (canvas != null ? canvas.scaleFactor : 1f);
        Clamp();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Right && menu != null) menu.CloseViewer();
    }

    private void Clamp()
    {
        if (zoom <= 1.001f) { pic.anchoredPosition = Vector2.zero; return; }
        Vector2 half = pic.sizeDelta * (zoom - 1f) * 0.5f;
        pic.anchoredPosition = new Vector2(Mathf.Clamp(pic.anchoredPosition.x, -half.x, half.x), Mathf.Clamp(pic.anchoredPosition.y, -half.y, half.y));
    }
}
