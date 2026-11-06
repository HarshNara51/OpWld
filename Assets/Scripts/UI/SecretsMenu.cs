using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The "Secrets" screen: the two hidden reward cars, what unlocks them and live progress.
// LOCKED cards are dim amber with a hint, UNLOCKED cards turn green and say where the car is.
// Built in code on its own overlay canvas when opened (SecretsButton) and destroyed on close.
// Esc / right click / BACK to close.
public class SecretsMenu : MonoBehaviour
{
    private static readonly Color Amber = new Color(1f, 0.72f, 0.3f);
    private static readonly Color AmberDim = new Color(1f, 0.72f, 0.3f, 0.45f);
    private static readonly Color Green = new Color(0.45f, 0.9f, 0.5f);
    private static readonly Color Body = new Color(0.93f, 0.91f, 0.87f);
    private static readonly Color Dim = new Color(0.6f, 0.6f, 0.65f);
    private static readonly Color Plate = new Color(0.04f, 0.04f, 0.06f, 1f);
    private static readonly Color CardPlate = new Color(0.08f, 0.08f, 0.1f, 1f);
    private static readonly Color BarBack = new Color(1f, 1f, 1f, 0.08f);

    private static SecretsMenu open;
    private TMP_FontAsset titleFont, bodyFont;
    private Sprite uiSprite;
    private Card blaze, taxi;
    private float nextRefresh;

    // One reward card; its texts and bars are refreshed while the page is open
    private class Card
    {
        public Image frame, badge;
        public TextMeshProUGUI badgeText, info;
        public Progress photos, missions;
    }

    private class Progress
    {
        public RectTransform fill;
        public Image fillImage;
        public TextMeshProUGUI label;
    }

    public static SecretsMenu Open(TMP_FontAsset titleFont, TMP_FontAsset bodyFont, Sprite uiSprite)
    {
        if (open != null) return open;
        var go = new GameObject("SecretsMenu", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // above the pause / settings menus
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        open = go.AddComponent<SecretsMenu>();
        open.titleFont = titleFont; open.bodyFont = bodyFont; open.uiSprite = uiSprite;
        open.Build();
        return open;
    }

    // Esc in the pause menu / main menu closes this first (returns true if it did)
    public static bool CloseOpenPage()
    {
        if (open == null) return false;
        open.Close();
        return true;
    }

    public void Close()
    {
        if (open == this) open = null;
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
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    // r placed inside parent at (x, y) from its top-left, w x h
    private RectTransform Place(string name, Transform parent, float x, float y, float w, float h)
    {
        var r = Rect(name, parent);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
        return r;
    }

    private void Build()
    {
        // Dim the game / menu behind
        var dim = Rect("Dim", transform); dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.offsetMin = dim.offsetMax = Vector2.zero;
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);

        var card = Rect("Card", transform);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f); card.sizeDelta = new Vector2(1400f, 760f);
        Panel(card, Plate);
        var bar = Rect("AccentBar", card); bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = new Vector2(1f, 1f); bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(-48f, 5f); bar.anchoredPosition = new Vector2(0f, -18f);
        bar.gameObject.AddComponent<Image>().color = Amber;

        Text(Place("Title", card, 0f, 34f, 1400f, 110f), titleFont, 96f, Amber, TextAlignmentOptions.Center, "SECRETS").characterSpacing = 12f;
        Text(Place("Subtitle", card, 0f, 140f, 1400f, 40f), bodyFont, 26f, Dim, TextAlignmentOptions.Center,
            "Two hidden cars are waiting for you. Here's how to earn them.");

        blaze = BuildCard(card, 60f, "BLAZE", "The fastest car in the game",
            photos: true, missions: false);
        taxi = BuildCard(card, 720f, "BONUS TAXI", "The near-indestructible Railway taxi",
            photos: true, missions: true);

        BackButton(card);
        Refresh();
    }

    private Card BuildCard(RectTransform parent, float x, string name, string tagline, bool photos, bool missions)
    {
        var c = new Card();
        var r = Place(name, parent, x, 210f, 620f, 470f);
        c.frame = Panel(r, CardPlate);
        var outline = r.gameObject.AddComponent<Outline>(); outline.effectDistance = new Vector2(2f, -2f); outline.effectColor = AmberDim;

        Text(Place("Name", r, 34f, 26f, 400f, 70f), titleFont, 64f, Amber, TextAlignmentOptions.Left, name).characterSpacing = 6f;
        Text(Place("Tagline", r, 36f, 96f, 550f, 34f), bodyFont, 22f, Dim, TextAlignmentOptions.Left, tagline).fontStyle = FontStyles.Italic;

        var badge = Place("Badge", r, 420f, 36f, 170f, 46f);
        c.badge = Panel(badge, AmberDim);
        c.badgeText = Text(Place("Label", badge, 0f, 0f, 170f, 46f), bodyFont, 22f, Color.black, TextAlignmentOptions.Center, "LOCKED");
        c.badgeText.fontStyle = FontStyles.Bold;

        float y = 150f;
        if (missions) { c.missions = ProgressBar(r, "Missions passed", y); y += 80f; }
        if (photos) { c.photos = ProgressBar(r, "Photo hunt items", y); y += 80f; }

        c.info = Text(Place("Info", r, 36f, y + 10f, 550f, 470f - y - 30f), bodyFont, 25f, Body, TextAlignmentOptions.TopLeft, "");
        c.info.lineSpacing = 8f;
        return c;
    }

    private Progress ProgressBar(RectTransform card, string label, float y)
    {
        var p = new Progress();
        p.label = Text(Place("ProgressLabel", card, 36f, y, 550f, 32f), bodyFont, 22f, Body, TextAlignmentOptions.Left, label);
        var track = Place("Track", card, 36f, y + 38f, 550f, 18f);
        Panel(track, BarBack);
        p.fill = Rect("Fill", track);
        p.fill.anchorMin = Vector2.zero; p.fill.anchorMax = new Vector2(0f, 1f); p.fill.pivot = new Vector2(0f, 0.5f);
        p.fill.offsetMin = p.fill.offsetMax = Vector2.zero;
        p.fillImage = Panel(p.fill, Amber);
        p.label.name = label; // keep the base text to rebuild "Missions passed  3 / 5"
        return p;
    }

    private void SetProgress(Progress p, int have, int total, bool done)
    {
        if (p == null) return;
        float f = total > 0 ? Mathf.Clamp01((float)have / total) : 0f;
        p.fill.anchorMax = new Vector2(f, 1f);
        p.fillImage.color = done ? Green : Amber;
        p.label.text = p.label.name + "   <b>" + have + " / " + total + "</b>";
    }

    private void SetState(Card c, bool unlocked, string lockedInfo, string unlockedInfo)
    {
        c.badge.color = unlocked ? Green : AmberDim;
        c.badgeText.text = unlocked ? "UNLOCKED" : "LOCKED";
        c.frame.GetComponent<Outline>().effectColor = unlocked ? Green : AmberDim;
        c.info.text = unlocked ? unlockedInfo : lockedInfo;
        c.info.color = unlocked ? Green : Body;
    }

    private void BackButton(RectTransform card)
    {
        var r = Rect("BackButton", card);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f); r.anchoredPosition = new Vector2(-40f, -40f); r.sizeDelta = new Vector2(170f, 60f);
        var img = Panel(r, new Color(0.1f, 0.1f, 0.12f, 1f));
        var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = img;
        var cb = b.colors; cb.highlightedColor = new Color(1f, 0.85f, 0.55f); cb.pressedColor = Amber; b.colors = cb;
        b.onClick.AddListener(Close);
        var tx = Text(Place("Label", r, 0f, 0f, 170f, 60f), bodyFont, 26f, Amber, TextAlignmentOptions.Center, "BACK");
        tx.fontStyle = FontStyles.Bold;
    }

    // ---------------- live progress ----------------

    private void Refresh()
    {
        var hunt = PhotoCollectionManager.Instance;
        var game = GameManager.Instance;
        int photos = hunt != null ? hunt.CollectedCount : 0, photoTotal = hunt != null ? hunt.Total : 0;
        bool huntDone = hunt != null && hunt.IsRewardUnlocked;
        int passed = 0, missionTotal = game != null ? game.missionSceneNames.Length : 0;
        if (game != null) foreach (string m in game.missionSceneNames) if (game.IsMissionComplete(m)) passed++;
        bool allMissions = game != null && game.AllMissionsComplete;

        SetProgress(blaze.photos, photos, photoTotal, huntDone);
        SetState(blaze, huntDone,
            "Photograph every item on the photo hunt list - the dinosaurs and the street art hidden around the map. " +
            "Open Collection to see what you're still missing.",
            "Parked in your garage, in every scene. Go take it for a spin!");

        SetProgress(taxi.missions, passed, missionTotal, allMissions);
        SetProgress(taxi.photos, photos, photoTotal, huntDone);
        SetState(taxi, allMissions && huntDone,
            "Reach 100%: pass all five missions AND finish the photo hunt.",
            "Parked near the repair shop, in every scene. Thank you for playing!");
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1)) { Close(); return; }
        // Unscaled time: the pause menu stops game time
        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.5f; Refresh(); }
    }
}
