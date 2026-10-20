using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Turns CREDITS.md (the licence checklist in the project folder) into
// Assets/Resources/Credits.txt, the text the in-game Credits screen scrolls.
// Keep filling in CREDITS.md, then run Tools > Credits > Build Credits Screen Text.
//
// Rows marked [!] (must be replaced) and rows used for nothing are left out.
// A model row with no author yet still shows its name, so nothing silently disappears.
//
// Credits.txt format (also fine to edit by hand): "!" big title, "#" section
// header, "~" small grey line, "" gap, anything else a normal line.
public static class CreditsBuilder
{
    private const string Source = "CREDITS.md";
    private const string Output = "Assets/Resources/Credits.txt";

    [MenuItem("Tools/Credits/Build Credits Screen Text")]
    public static void Build()
    {
        if (!File.Exists(Source)) { Debug.LogError("CreditsBuilder: CREDITS.md not found in the project folder."); return; }

        var models = new List<string>();
        var store = new List<string>();
        var fonts = new List<string>();
        var music = new List<string>();
        var sounds = new List<string>();
        int part = 0;

        foreach (string raw in File.ReadAllLines(Source))
        {
            string line = raw.Trim();
            if (line.StartsWith("## Part 1")) part = 1;
            else if (line.StartsWith("## Part 2")) part = 2;
            else if (line.StartsWith("## Part 3")) part = 3;
            else if (line.StartsWith("## Part 4 - Audio")) part = 4;
            else if (line.StartsWith("## Part 4")) part = 0; // the old text draft
            if (!line.StartsWith("|") || line.StartsWith("|---")) continue;

            string[] c = Cells(line);
            if (part == 1 && c.Length >= 6 && c[0] != "Status")
            {
                if (c[0].Contains("[!]") || c[3] == "-") continue;
                string folder = Clean(c[1]), what = Short(c[2]), author = Clean(c[3]), licence = Short(c[4]).TrimEnd('?');
                if (folder.StartsWith("Fonts")) { fonts.Add(Join(Short(c[2]).Replace("Font ", "").Trim('"'), author, "")); continue; }
                if (folder == "Sounds") { sounds.Add(Join("Door unlock, camera shutter", author, "")); continue; }
                models.Add(Join(what, author, licence));
            }
            else if (part == 2 && c.Length >= 3 && c[0] != "Folder")
            {
                string package = Clean(c[1]), publisher = Clean(c[2]).TrimEnd('?');
                int note = package.IndexOf(" ("); if (note > 2) package = package.Substring(0, note); // keep "ARCADE: FREE Racing Car" whole, drop "(notes)"
                if (publisher.StartsWith("Unity Technologies") && package.StartsWith("Unity packages")) continue;
                store.Add(Join(package, publisher, ""));
            }
            else if (part == 4 && c.Length >= 4 && c[0] != "Status")
            {
                if (c[0].Contains("[!]") || c[2].Contains("not used")) continue;
                string author = Clean(c[3]);
                if (author.StartsWith("(")) author = "Pixabay";
                string used = c[2].ToLowerInvariant();
                bool isMusic = used.Contains("radio") || used.Contains("main menu") || used.Contains("mission passed") || used.Contains("mission failed");
                (isMusic ? music : sounds).Add(Join(Clean(c[1]), author, ""));
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("!GRAY COUNTY").AppendLine("~Credits").AppendLine();
        sb.AppendLine("~A game by").AppendLine("#Harsh Nara").AppendLine().AppendLine();
        Section(sb, "3D Models & Art", models);
        Section(sb, "Characters & Animations", new List<string> { "Adobe Mixamo" });
        Section(sb, "Unity Asset Store", store);
        Section(sb, "Fonts", fonts);
        Section(sb, "Music", music, "All music from Pixabay");
        Section(sb, "Sound Effects", sounds, "Sounds from Pixabay");
        Section(sb, "Made With", new List<string> { "Unity", "Blender" });
        sb.AppendLine().AppendLine().AppendLine("!THANK YOU FOR PLAYING!");

        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, sb.ToString());
        AssetDatabase.ImportAsset(Output);
        Debug.Log($"Credits: {models.Count} models/art, {store.Count} store packages, {music.Count} songs, {sounds.Count} sounds -> {Output}");
    }

    private static void Section(StringBuilder sb, string title, List<string> lines, string note = null)
    {
        if (lines.Count == 0) return;
        sb.AppendLine("#" + title.ToUpper());
        if (note != null) sb.AppendLine("~" + note);
        var seen = new HashSet<string>();
        foreach (string l in lines) if (seen.Add(l)) sb.AppendLine(l);
        sb.AppendLine().AppendLine();
    }

    private static string Join(string what, string author, string licence)
    {
        string s = what;
        if (!string.IsNullOrEmpty(author) && author != "-") s += "  -  " + author;
        if (!string.IsNullOrEmpty(licence) && licence != "-" && licence.ToLowerInvariant() != "own") s += "  (" + licence + ")";
        return s;
    }

    private static string[] Cells(string line)
    {
        string[] parts = line.Trim('|').Split('|');
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        return parts;
    }

    // Markdown out: **bold**, `code`
    private static string Clean(string s) => s.Replace("**", "").Replace("`", "").Trim();

    // "Roadside billboard (Downloads: ...)" -> "Roadside billboard"; also drops "- check" notes
    private static string Short(string s)
    {
        s = Clean(s);
        s = Regex.Replace(s, @"\s*-\s*(check|not used).*$", "", RegexOptions.IgnoreCase);
        foreach (string cut in new[] { " (", ": ", " - ", ". " })
        {
            int i = s.IndexOf(cut);
            if (i > 2) s = s.Substring(0, i);
        }
        return s.Trim();
    }
}
