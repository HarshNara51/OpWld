using UnityEditor;
using UnityEngine;

// Tools > Minimap > Set Up Icons
//
// Fills the Minimap on the Managers prefab with the map icons from
// Assets/Imports/Icons/Map: the fixed places (home, hospital, police, fire,
// tow yard), the faint area signs, and the rules for gizmos (which title gets
// which icon, which are hidden). Running it again overwrites the icon lists,
// so tweak the positions here (or in the prefab afterwards and don't re-run).
public static class MinimapIconSetup
{
    private const string ManagersPath = "Assets/Resources/Managers.prefab";
    private const string IconFolder = "Assets/Imports/Icons/Map/";

    private static readonly string[] Diamonds =   // go here
    {
        "Park Here", "Something's Wrong", "Hide Here", "Police Station", "Hotel Entrance", "Hotel Exit",
        "Bedroom Entrance", "Bedroom Exit", "Vantage Point", "Cargo Drop-off",
    };

    private static readonly string[] Rings =      // do something here
    {
        "Photo Spot", "Call the Cops", "Radio", "Stolen Item", "Cargo 1", "Cargo 2", "Cargo 3", "Cargo 4", "Cargo 5",
        "TARGET CAR", "Bomb",
    };

    private static readonly string[] Hidden =
    {
        "Clue",        // Farm detective work - the player finds them
        "Tow Truck",   // the payphones (the tow yard has its own place icon)
        "Return Home", // it's at the house, which has the home icon
    };

    [MenuItem("Tools/Minimap/Set Up Icons")]
    private static void SetUp()
    {
        Sprite diamond = Icon("icon_diamond"), ring = Icon("icon_ring");
        if (diamond == null) { EditorUtility.DisplayDialog("Set Up Icons", "No icons found in " + IconFolder, "OK"); return; }

        var rules = new System.Collections.Generic.List<Minimap.IconRule>
        {
            new Minimap.IconRule { title = "Respray", icon = Icon("icon_respray") },
            new Minimap.IconRule { title = "Fuel", icon = Icon("icon_fuel") },
        };
        foreach (string t in Diamonds) rules.Add(new Minimap.IconRule { title = t, icon = diamond, tint = true });
        foreach (string t in Rings) rules.Add(new Minimap.IconRule { title = t, icon = ring, tint = true });
        foreach (string t in Hidden) rules.Add(new Minimap.IconRule { title = t, hide = true });

        Minimap.Place[] places =
        {
            Place("Home", "icon_home", 460, 331),
            Place("Hospital", "icon_hospital", 675, 381),
            Place("Police Station", "icon_police", 503, 443),
            Place("Fire Station", "icon_fire", 501, 380),
            Place("Tow Yard", "icon_tow", 509, 186),
            Area("Farm", "icon_area_farm", 777, 528),
            Area("Shopping Center", "icon_area_shopping", 160, 575),
            Area("Construction Site", "icon_area_construction", 822, 405),
            Area("Luxury Hotels", "icon_area_hotels", 320, 455),
            Area("Railway", "icon_area_railway", 190, 870),
        };

        GameObject prefab = PrefabUtility.LoadPrefabContents(ManagersPath);
        try
        {
            Minimap map = prefab.GetComponent<Minimap>();
            if (map == null) map = prefab.AddComponent<Minimap>();
            map.SetIcons(Icon("icon_star"), diamond, rules.ToArray(), places,
                AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Imports/Fonts/SDF/TulpenOne SDF.asset"));
            PrefabUtility.SaveAsPrefabAsset(prefab, ManagersPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
        Debug.Log($"Minimap: icons set up ({places.Length} places, {rules.Count} gizmo rules).");
    }

    private static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + name + ".png");

    private static Minimap.Place Place(string name, string icon, float x, float z) =>
        new Minimap.Place { name = name, icon = Icon(icon), position = new Vector2(x, z) };

    private static Minimap.Place Area(string name, string icon, float x, float z) =>
        new Minimap.Place { name = name, icon = Icon(icon), position = new Vector2(x, z), area = true };
}
