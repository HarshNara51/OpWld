using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// GTA-style photo camera. Lives on the Managers prefab, works in every
// scene (except menus). Press the toggle key to open/close, mouse to
// look around, scroll to zoom, left click to take a photo.
//
// While open, the view moves to the player's chest (first-person), so
// you shoot from the player's eyes instead of from behind.
// Each photo: shutter sound + white flash, saves a screenshot (optional),
// and "counts" the best PhotoTarget in frame (clues, body, trucks...).
[DefaultExecutionOrder(10000)] // runs after the normal camera script, so it wins
public class CameraMode : MonoBehaviour
{
    public static CameraMode Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.active;
    public KeyCode ToggleKey => GameKeys.Get(GameAction.PhotoMode);

    [Header("Controls")]
    [SerializeField] private float shutterCooldown = 0.6f;

    [Header("First-person view")]
    [SerializeField] private bool firstPersonView = true;
    [Tooltip("Camera height above the player's feet (chest/eye level)")]
    [SerializeField] private float eyeHeight = 1.4f;
    [Tooltip("Camera height above a car's origin when shooting from inside one")]
    [SerializeField] private float vehicleEyeHeight = 1.3f;
    [Tooltip("Pushes the camera slightly forward so it's not inside the head")]
    [SerializeField] private float forwardOffset = 0.3f;
    [SerializeField] private float mouseSensitivity = 2f;

    [Header("UI")]
    [Tooltip("The camera frame overlay panel")]
    [SerializeField] private GameObject frameOverlay;
    [Tooltip("Full-screen white Image with a CanvasGroup - the flash")]
    [SerializeField] private CanvasGroup flash;
    [SerializeField] private float flashSeconds = 0.4f;

    [Header("Zoom")]
    [SerializeField] private float startFov = 45f;
    [SerializeField] private float minFov = 20f;
    [SerializeField] private float maxFov = 60f;
    [SerializeField] private float zoomSpeed = 5f;

    [Header("What counts as 'in the photo'")]
    [Tooltip("0.2 = the subject must be inside the middle 60% of the screen")]
    [Range(0f, 0.45f)][SerializeField] private float frameMargin = 0.2f;
    [SerializeField] private bool requireLineOfSight = true;
    [Tooltip("Things closer than this to the subject don't count as blocking it")]
    [SerializeField] private float lineOfSightTolerance = 2.5f;

    [Header("Saving")]
    [SerializeField] private bool saveScreenshots = true;
    [SerializeField] private string folderName = "Photos";

    [Header("Sound")]
    [SerializeField] private AudioClip shutterSound;

    [Tooltip("Scenes where camera mode can't be opened")]
    [SerializeField] private string[] blockedScenes = { "MainMenu" };

    private bool active;
    private Camera cam;
    private float originalFov;
    private Renderer[] hiddenRenderers;
    private Transform subject;
    private VehicleInteraction vehicleAtOpen;
    private float yaw, pitch;
    private float nextShotTime;
    private bool loggedFolder;

    private void Awake()
    {
        Instance = this;
        if (frameOverlay != null) frameOverlay.SetActive(false);
        if (flash != null) { flash.alpha = 0f; flash.blocksRaycasts = false; }
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The old camera/player are gone - just reset quietly
        active = false;
        hiddenRenderers = null;
        subject = null;
        if (frameOverlay != null) frameOverlay.SetActive(false);
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return; // paused or briefing

        if (IsBlockedScene())
        {
            if (active) Close();
            return;
        }

        if (GameKeys.Down(GameAction.PhotoMode))
        {
            if (active) Close();
            else Open();
        }

        if (!active) return;

        // Got in or out of a car while the camera was up - put it away
        if (VehicleInteraction.Current != vehicleAtOpen || subject == null)
        {
            Close();
            return;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0f && cam != null)
        {
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - scroll * zoomSpeed, minFov, maxFov);
        }

        if (GameKeys.Down(GameAction.TakePicture) && Time.time >= nextShotTime)
        {
            nextShotTime = Time.time + shutterCooldown;
            StartCoroutine(TakePhoto());
        }
    }

    // After the normal camera script has positioned the camera, move it
    // to chest height and aim it with the mouse (first-person).
    private void LateUpdate()
    {
        if (!active || !firstPersonView || cam == null || subject == null) return;
        if (Time.timeScale == 0f) return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -80f, 80f);

        Quaternion flatRotation = Quaternion.Euler(0f, yaw, 0f);
        float height = vehicleAtOpen != null ? vehicleEyeHeight : eyeHeight;

        cam.transform.position = subject.position + Vector3.up * height + flatRotation * Vector3.forward * forwardOffset;
        cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void Open()
    {
        cam = Camera.main;
        if (cam == null) return;

        vehicleAtOpen = VehicleInteraction.Current;
        subject = GetSubject();
        if (subject == null) return;

        active = true;
        originalFov = cam.fieldOfView;
        cam.fieldOfView = startFov;

        // Start looking the same way the camera already faces
        Vector3 euler = cam.transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        if (frameOverlay != null) frameOverlay.SetActive(true);
        HideSubject(true);
    }

    private void Close()
    {
        if (!active) return;
        active = false;

        if (cam != null) cam.fieldOfView = originalFov;
        if (frameOverlay != null) frameOverlay.SetActive(false);
        HideSubject(false);
        subject = null;
    }

    // The player on foot, or the car being driven
    private Transform GetSubject()
    {
        if (VehicleInteraction.Current != null) return VehicleInteraction.Current.transform;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    // Hides the player (or car) so they don't block the shot
    private void HideSubject(bool hide)
    {
        if (hide)
        {
            if (subject == null) return;

            var list = new List<Renderer>();
            foreach (Renderer r in subject.GetComponentsInChildren<Renderer>())
            {
                if (r.enabled) { r.enabled = false; list.Add(r); }
            }
            hiddenRenderers = list.ToArray();
        }
        else if (hiddenRenderers != null)
        {
            foreach (Renderer r in hiddenRenderers)
            {
                if (r != null) r.enabled = true;
            }
            hiddenRenderers = null;
        }
    }

    private IEnumerator TakePhoto()
    {
        // Decide what's in the shot at the moment of the click
        PhotoTarget best = FindBestTarget();

        // Save the picture without the frame overlay in it
        if (saveScreenshots)
        {
            if (frameOverlay != null) frameOverlay.SetActive(false);

            string folder = Path.Combine(Application.persistentDataPath, folderName);
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, $"Photo_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
            ScreenCapture.CaptureScreenshot(file);

            if (!loggedFolder)
            {
                loggedFolder = true;
                Debug.Log($"Photos are saved in: {folder}");
            }

            yield return new WaitForEndOfFrame();
            yield return null;

            if (active && frameOverlay != null) frameOverlay.SetActive(true);
        }

        // Feedback: shutter + flash
        if (shutterSound != null && AudioManager.Instance != null) AudioManager.Instance.PlayUI(shutterSound);
        if (flash != null) StartCoroutine(Flash());

        Debug.Log("Click! Photo taken.");
        if (best != null) best.TryPhotograph();
    }

    private IEnumerator Flash()
    {
        float t = 0f;
        while (t < flashSeconds)
        {
            t += Time.unscaledDeltaTime;
            flash.alpha = 1f - (t / flashSeconds);
            yield return null;
        }
        flash.alpha = 0f;
    }

    // The un-photographed target closest to the middle of the frame
    private PhotoTarget FindBestTarget()
    {
        if (cam == null) return null;

        PhotoTarget best = null;
        float bestScore = float.MaxValue;

        foreach (PhotoTarget target in PhotoTarget.All)
        {
            if (target == null || target.Photographed || !target.isActiveAndEnabled) continue;

            Vector3 focus = target.FocusPosition;
            Vector3 vp = cam.WorldToViewportPoint(focus);

            if (vp.z <= 0f || vp.z > target.maxDistance) continue; // behind camera or too far
            if (vp.x < frameMargin || vp.x > 1f - frameMargin) continue;
            if (vp.y < frameMargin || vp.y > 1f - frameMargin) continue;
            if (requireLineOfSight && IsBlocked(focus, target)) continue;

            float score = new Vector2(vp.x - 0.5f, vp.y - 0.5f).sqrMagnitude;
            if (score < bestScore)
            {
                bestScore = score;
                best = target;
            }
        }
        return best;
    }

    private bool IsBlocked(Vector3 focus, PhotoTarget target)
    {
        Vector3 origin = cam.transform.position;
        Vector3 toFocus = focus - origin;
        float distance = toFocus.magnitude;

        foreach (RaycastHit hit in Physics.RaycastAll(origin, toFocus.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform h = hit.transform;
            if (h.IsChildOf(target.transform)) continue;                 // the subject itself
            if (h.CompareTag("Player") || h.CompareTag("PlayerCar")) continue;
            if (subject != null && h.IsChildOf(subject)) continue;        // the player/car we're shooting from
            if (distance - hit.distance <= lineOfSightTolerance) continue; // right next to the subject

            return true; // a wall/tree/building is in the way
        }
        return false;
    }

    private bool IsBlockedScene()
    {
        string current = SceneManager.GetActiveScene().name;
        foreach (string s in blockedScenes)
        {
            if (s == current) return true;
        }
        return false;
    }
}