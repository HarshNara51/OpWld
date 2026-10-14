using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A health bar over the head of every enemy - anything with a Health
// component that isn't the player. Builds itself the first time an enemy
// appears in a scene (Health registers itself); nothing to wire.
// One screen-space bar per enemy, so they stay the same size on screen.
// Damage leaves a pale "trail" that drains after the red part.
public class EnemyHealthBars : MonoBehaviour
{
    private const float ShowDistance = 60f;     // further than this: no bar
    private const float HeadClearance = 0.45f;  // metres above the head bone
    private static readonly Vector2 BarSize = new Vector2(110f, 11f);

    private static readonly Color BackColor = new Color(0.05f, 0.05f, 0.06f, 0.75f);
    private static readonly Color FillColor = new Color(0.86f, 0.12f, 0.1f, 1f);
    private static readonly Color TrailColor = new Color(1f, 0.85f, 0.6f, 0.9f);

    private class Bar
    {
        public Health health;
        public Transform anchor;   // head bone, or the root
        public float offset;       // metres above the anchor
        public RectTransform root;
        public RectTransform fill;
        public RectTransform trail;
        public CanvasGroup group;
        public float trailPercent = 1f;
        public float deadFade = 1f;
    }

    private static EnemyHealthBars instance;
    private readonly List<Bar> bars = new List<Bar>();
    private Transform canvasRoot;
    private Camera cam;

    // Called by Health.Start on every non-player
    public static void Register(Health health)
    {
        if (instance == null)
        {
            // Lives in the current scene, so it goes away with it
            instance = new GameObject("EnemyHealthBars").AddComponent<EnemyHealthBars>();
            instance.BuildCanvas();
        }
        instance.Add(health);
    }

    private void BuildCanvas()
    {
        var go = new GameObject("Canvas");
        go.transform.SetParent(transform, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10; // under the notes, crosshair and menus
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasRoot = go.transform;
    }

    private void Add(Health health)
    {
        var bar = new Bar { health = health, anchor = health.transform };

        Animator animator = health.GetComponentInChildren<Animator>();
        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head != null) { bar.anchor = head; bar.offset = HeadClearance; }
        else
        {
            // No humanoid rig: just above the top of its model
            float top = health.transform.position.y + 2f;
            foreach (Renderer r in health.GetComponentsInChildren<Renderer>())
                if (r is MeshRenderer || r is SkinnedMeshRenderer) top = Mathf.Max(top, r.bounds.max.y);
            bar.offset = top - health.transform.position.y + 0.3f;
        }

        bar.root = NewRect("Bar_" + health.name, canvasRoot, BackColor);
        bar.root.sizeDelta = BarSize + new Vector2(4f, 4f); // dark border around the fill
        bar.group = bar.root.gameObject.AddComponent<CanvasGroup>();
        bar.group.blocksRaycasts = false;
        bar.trail = NewRect("Trail", bar.root, TrailColor);
        bar.fill = NewRect("Fill", bar.root, FillColor);
        foreach (RectTransform rt in new[] { bar.trail, bar.fill })
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(2f, 2f);
            rt.offsetMax = new Vector2(-2f, -2f);
        }
        bars.Add(bar);
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        for (int i = bars.Count - 1; i >= 0; i--)
        {
            Bar bar = bars[i];
            if (bar.health == null || bar.anchor == null)
            {
                if (bar.root != null) Destroy(bar.root.gameObject);
                bars.RemoveAt(i);
                continue;
            }

            float percent = Mathf.Clamp01(bar.health.Percent01);
            if (bar.health.IsDead) bar.deadFade = Mathf.MoveTowards(bar.deadFade, 0f, Time.deltaTime * 2f);

            Vector3 world = bar.anchor.position + Vector3.up * bar.offset;
            Vector3 screen = cam.WorldToScreenPoint(world);
            float distance = Vector3.Distance(cam.transform.position, world);
            bool visible = screen.z > 0f && distance <= ShowDistance && bar.deadFade > 0f &&
                           bar.health.gameObject.activeInHierarchy;

            if (bar.root.gameObject.activeSelf != visible) bar.root.gameObject.SetActive(visible);
            if (!visible) continue;

            bar.root.position = screen;
            bar.root.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, distance / ShowDistance);
            bar.group.alpha = bar.deadFade * Mathf.Clamp01((ShowDistance - distance) / 8f); // fades out at the edge of range

            // Red part jumps to the new health; the pale trail catches up after it
            bar.trailPercent = percent > bar.trailPercent ? percent : Mathf.MoveTowards(bar.trailPercent, percent, Time.deltaTime * 0.6f);
            SetWidth(bar.fill, percent);
            SetWidth(bar.trail, bar.trailPercent);
        }
    }

    private static void SetWidth(RectTransform rt, float percent)
    {
        rt.anchorMax = new Vector2(percent, 1f);
    }

    private static RectTransform NewRect(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }
}
