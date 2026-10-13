using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > UI > Apply Menu Button Art
//
// Puts the button pictures from Assets/Imports/image/Menu on the menu
// buttons, in the open MainMenu scene and in the pause menu (Managers
// prefab). Run it again after changing a picture or a size.
//
//   Main menu:  Continue, New Game, Settings, Quit - one column on the left
//               (the old Play button and its Continue/New Game screen are
//               switched off, not deleted)
//   Settings:   Graphics (current level shown beside it) + Controls on the
//               left (the volume sliders are in the middle), Back bottom right
//   Pause:      Resume, Settings, Main Menu, Quit (Return Home has no
//               picture yet - it keeps its plain look)
public static class MenuButtonArtSetup
{
    private const string Folder = "Assets/Imports/image/Menu/";

    // Button name -> picture name (files are <name>_normal / _unselected and <name>_highlighted)
    private static readonly Dictionary<string, string> Art = new Dictionary<string, string>
    {
        { "ContinueButton", "continue" }, { "NewGameButton", "new_game" },
        { "SettingsButton", "settings" }, { "QuitButton", "quit" },
        { "BackButton", "back" }, { "GraphicsButton", "graphics" }, { "ControlsButton", "controls" },
        { "resume", "resume" }, { "settings", "settings" }, { "mainmenu", "main_menu" }, { "quit", "quit" },
    };

    private const float MenuWidth = 470f;   // main menu column
    private const float PauseWidth = 400f;  // pause column
    private const float SettingsWidth = 380f;

    [MenuItem("Tools/UI/Apply Menu Button Art")]
    public static string Apply()
    {
        var log = new List<string>();
        FixImports(log);

        // ---- MainMenu scene (must be the open scene) ----
        var scene = EditorSceneManager.GetActiveScene();
        var controller = Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            Transform main = Find(scene, "MainPanel");
            Transform mode = Find(scene, "ModePanel");
            if (main != null && mode != null)
            {
                // One screen: Continue, New Game, Settings, Quit
                var column = new[] { "ContinueButton", "NewGameButton", "SettingsButton", "QuitButton" };
                for (int i = 0; i < column.Length; i++)
                {
                    Transform b = main.Find(column[i]) ?? mode.Find(column[i]);
                    if (b == null) continue;
                    Undo.SetTransformParent(b, main, "Menu art");
                    Place(b, new Vector2(-560f, 110f - i * 125f), MenuWidth);
                    Dress(b, log);
                }
                Transform play = main.Find("PlayButton");
                if (play != null) { Undo.RecordObject(play.gameObject, "Menu art"); play.gameObject.SetActive(false); }
            }

            Transform settings = Find(scene, "SettingsPanel");
            if (settings != null) DressSettings(settings, log);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.Add("MainMenu scene saved");
        }
        else log.Add("(open the MainMenu scene to do the main menu too)");

        // ---- Pause menu (Managers prefab) ----
        string prefabPath = "Assets/Resources/Managers.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform buttons = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "ButtonsPanel");
            if (buttons != null)
            {
                var column = new[] { "resume", "settings", "returnhome", "mainmenu", "quit" };
                for (int i = 0; i < column.Length; i++)
                {
                    Transform b = buttons.Find(column[i]);
                    if (b == null) continue;
                    bool hasArt = Art.ContainsKey(column[i]);
                    Place(b, new Vector2(-560f, 220f - i * 115f), hasArt ? PauseWidth : 0f);
                    if (hasArt) Dress(b, log);
                    else StylePlain(b, "RETURN HOME", PauseWidth);
                }
            }
            Transform pauseSettings = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SettingsPanel");
            if (pauseSettings != null) DressSettings(pauseSettings, log);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            log.Add("pause menu (Managers prefab) saved");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        string summary = "Menu button art: " + string.Join(", ", log);
        Debug.Log(summary);
        return summary;
    }

    // Graphics + Controls + Back on a Settings screen
    private static void DressSettings(Transform panel, List<string> log)
    {
        Transform back = panel.Find("BackButton");
        Transform graphics = panel.Find("GraphicsButton");
        // Bottom right, below the Controls page's key lists
        if (back != null) { Place(back, new Vector2(700f, -462f), SettingsWidth); Dress(back, log); }
        if (graphics != null)
        {
            Place(graphics, new Vector2(-560f, 60f), SettingsWidth); // left column - the sliders are in the middle
            Dress(graphics, log, keepLabel: true);
        }

        // A real Controls button (ControlsMenu would otherwise make a copy of Back)
        ControlsMenu controls = panel.GetComponent<ControlsMenu>();
        if (controls != null && back != null)
        {
            Transform existing = panel.Find("ControlsButton");
            if (existing == null)
            {
                GameObject copy = Object.Instantiate(back.gameObject, panel);
                copy.name = "ControlsButton";
                Undo.RegisterCreatedObjectUndo(copy, "Menu art");
                Button cb = copy.GetComponent<Button>();
                for (int i = cb.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(cb.onClick, i);
                existing = copy.transform;
            }
            Place(existing, new Vector2(-560f, -65f), SettingsWidth);
            Dress(existing, log);
            var so = new SerializedObject(controls);
            so.FindProperty("controlsButton").objectReferenceValue = existing.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Picture on, text off (Graphics keeps its text, moved beside the picture for the level)
    private static void Dress(Transform button, List<string> log, bool keepLabel = false)
    {
        if (!Art.TryGetValue(button.name, out string art)) return;
        Sprite normal = Load(art + "_normal") ?? Load(art + "_unselected");
        Sprite lit = Load(art + "_highlighted");
        if (normal == null) { log.Add($"no picture for {button.name}"); return; }

        var b = button.GetComponent<Button>();
        var dressed = button.GetComponent<MenuButtonArt>() ?? Undo.AddComponent<MenuButtonArt>(button.gameObject);
        Undo.RecordObject(dressed, "Menu art");
        Undo.RecordObject(b, "Menu art");
        if (b.image != null) Undo.RecordObject(b.image, "Menu art");
        dressed.Apply(normal, lit != null ? lit : normal);
        // The plain look a copy goes back to (ControlsMenu's "Reset to defaults")
        var artSo = new SerializedObject(dressed);
        artSo.FindProperty("plainSize").vector2Value = new Vector2(260f, 46f);
        artSo.ApplyModifiedPropertiesWithoutUndo();

        foreach (TMP_Text t in button.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(t.gameObject, "Menu art");
            if (!keepLabel) { t.gameObject.SetActive(false); continue; }

            // Level text just to the right of the picture
            t.gameObject.SetActive(true);
            Undo.RecordObject(t.rectTransform, "Menu art");
            Undo.RecordObject(t, "Menu art");
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.rectTransform.anchoredPosition = new Vector2(12f, 0f);
            t.rectTransform.sizeDelta = new Vector2(160f, 50f);
            t.alignment = TextAlignmentOptions.Left;
            t.enableAutoSizing = false;
            t.fontSize = 30f;
            t.fontStyle = FontStyles.Bold;
            t.color = new Color(1f, 0.72f, 0.3f, 1f); // warm amber, like the menu titles
        }
        log.Add(button.name);
    }

    // Size from the picture's shape, so the VISIBLE part is 'width' wide (some
    // pictures have lots of empty space around them, e.g. Main Menu)
    private static void Place(Transform button, Vector2 position, float width)
    {
        var rt = (RectTransform)button;
        Undo.RecordObject(rt, "Menu art");
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        if (width <= 0f) return;
        string art = Art.TryGetValue(button.name, out string a) ? a : null;
        Sprite s = art != null ? (Load(art + "_normal") ?? Load(art + "_unselected")) : null;
        float aspect = s != null ? s.rect.height / s.rect.width : 0.25f;
        float full = width / (s != null ? VisibleFraction(s) : 1f);
        rt.sizeDelta = new Vector2(full, full * aspect);
    }

    // How much of the picture's width isn't transparent (read straight from the file)
    private static float VisibleFraction(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite.texture);
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) return 1f;
        int w = tex.width, h = tex.height, minX = w, maxX = -1;
        Color32[] px = tex.GetPixels32();
        for (int y = 0; y < h; y += 2)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 40) { if (x < minX) minX = x; if (x > maxX) maxX = x; }
        Object.DestroyImmediate(tex);
        return maxX > minX ? Mathf.Clamp((maxX - minX + 1f) / w, 0.3f, 1f) : 1f;
    }

    // No picture yet: a plain button styled to sit with the pictures
    private static void StylePlain(Transform button, string text, float width)
    {
        var b = button.GetComponent<Button>();
        var rt = (RectTransform)button;
        Undo.RecordObject(rt, "Menu art");
        Undo.RecordObject(b, "Menu art");
        rt.sizeDelta = new Vector2(width, width * 0.21f);
        if (b.image != null)
        {
            Undo.RecordObject(b.image, "Menu art");
            b.image.color = Color.white;
        }
        b.transition = Selectable.Transition.ColorTint;
        ColorBlock c = b.colors;
        c.normalColor = new Color(0.11f, 0.13f, 0.12f, 0.88f);
        c.highlightedColor = new Color(0.62f, 0.09f, 0.08f, 0.95f);
        c.pressedColor = new Color(0.45f, 0.06f, 0.05f, 1f);
        c.selectedColor = c.normalColor;
        c.colorMultiplier = 1f;
        b.colors = c;
        foreach (TMP_Text t in button.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(t, "Menu art");
            t.gameObject.SetActive(true);
            t.text = text;
            t.enableAutoSizing = false;
            t.fontSize = 34f;
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 6f;
            t.color = new Color(0.88f, 0.86f, 0.8f, 1f);
            t.alignment = TextAlignmentOptions.Center;
        }
    }

    // The button pictures must be single sprites (they came in as 'Multiple' with no slices)
    private static void FixImports(List<string> log)
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!file.Contains("_normal") && !file.Contains("_unselected") && !file.Contains("_highlighted")) continue;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single && !importer.mipmapEnabled) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            changed++;
        }
        if (changed > 0) log.Add($"{changed} pictures set to Single sprite");
    }

    private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + name + ".png");

    private static Transform Find(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (GameObject r in scene.GetRootGameObjects())
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }
}
