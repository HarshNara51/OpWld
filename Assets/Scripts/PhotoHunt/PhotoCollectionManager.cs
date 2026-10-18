using System;
using System.Collections;
using UnityEngine;

// The photo hunt. Lives on the Managers prefab (so it exists in every
// scene). Holds the list of things to photograph, remembers which ones
// you've got (saved between sessions), and unlocks the reward car when
// the list is complete.
//
// Each item on the list is just an ID + a display name. In the scenes,
// put a PhotoCollectible on the real object with the SAME ID.
public class PhotoCollectionManager : MonoBehaviour
{
    public static PhotoCollectionManager Instance { get; private set; }

    [Serializable]
    public class Item
    {
        [Tooltip("Unique ID - must match the PhotoCollectible's ID in the scene, e.g. dino_trex")]
        public string id;
        [Tooltip("Name shown when you photograph it, e.g. 'The T-Rex'")]
        public string displayName;
    }

    [SerializeField] private Item[] items;

    [TextArea(2, 4)]
    [SerializeField] private string completeMessage =
        "You photographed them all! A brand new car is waiting for you in the garage.";

    // Fires once when the last photo is taken (reward cars listen for this)
    public static event Action RewardUnlocked;

    private const string ItemPrefix = "PhotoItem_";
    private const string RewardKey = "PhotoRewardUnlocked";

    public int Total => items != null ? items.Length : 0;
    public bool IsRewardUnlocked => PlayerPrefs.GetInt(RewardKey, 0) == 1;

    private void Awake()
    {
        Instance = this;
    }

    public bool IsOnList(string id) => Find(id) != null;

    public bool IsCollected(string id) => PlayerPrefs.GetInt(ItemPrefix + id, 0) == 1;

    public int CollectedCount
    {
        get
        {
            int count = 0;
            if (items != null)
                foreach (Item item in items)
                    if (item != null && IsCollected(item.id)) count++;
            return count;
        }
    }

    // Called by PhotoCollectible when it's photographed
    public void Collect(string id)
    {
        Item item = Find(id);
        if (item == null)
        {
            Debug.LogWarning($"PhotoCollection: '{id}' isn't on the list in PhotoCollectionManager.");
            return;
        }
        if (IsCollected(id)) return;

        PlayerPrefs.SetInt(ItemPrefix + id, 1);
        PlayerPrefs.Save();
        StartCoroutine(CopyToCollection(id, CameraMode.LastPhotoPath)); // first shot of this item = its Collection picture

        int got = CollectedCount;
        string label = string.IsNullOrEmpty(item.displayName) ? id : item.displayName;
        NotePopup.Show($"<b>Photo collection {got} / {Total}</b>\n{label}", 3.5f);

        if (got >= Total && !IsRewardUnlocked)
        {
            PlayerPrefs.SetInt(RewardKey, 1);
            PlayerPrefs.Save();
            NotePopup.Show(completeMessage, 6f);
            RewardUnlocked?.Invoke();
        }
    }

    // For New Game: wipe the hunt and lock the reward again
    public void ResetAll()
    {
        if (items != null)
            foreach (Item item in items)
                if (item != null)
                {
                    PlayerPrefs.DeleteKey(ItemPrefix + item.id);
                    string img = CollectionImagePath(item.id);
                    try { if (System.IO.File.Exists(img)) System.IO.File.Delete(img); } catch (Exception) { }
                }
        PlayerPrefs.DeleteKey(RewardKey);
        PlayerPrefs.Save();
    }

    // ---------- Collection (the in-game photo album) ----------

    // The list in order, for the Collection screen
    public Item[] Items => items ?? new Item[0];

    public static string CollectionFolder => System.IO.Path.Combine(Application.persistentDataPath, "Collection");

    // One picture per item: <id>.png. Exists only once the item has been collected with a saved photo.
    public string CollectionImagePath(string id) => System.IO.Path.Combine(CollectionFolder, id + ".png");

    // The screenshot is written at the end of the frame - wait for it, then keep a copy as this item's picture.
    // Only the FIRST photo of an item is kept; later shots of it never replace it.
    private IEnumerator CopyToCollection(string id, string source)
    {
        if (string.IsNullOrEmpty(source)) yield break;
        string target = CollectionImagePath(id);
        if (System.IO.File.Exists(target)) yield break;

        float giveUp = Time.realtimeSinceStartup + 5f;
        while (!System.IO.File.Exists(source) && Time.realtimeSinceStartup < giveUp) yield return null;
        yield return new WaitForSecondsRealtime(0.25f); // let the write finish
        try
        {
            System.IO.Directory.CreateDirectory(CollectionFolder);
            if (System.IO.File.Exists(source) && !System.IO.File.Exists(target)) System.IO.File.Copy(source, target);
        }
        catch (Exception e) { Debug.LogWarning("Collection: couldn't save the picture for " + id + ": " + e.Message); }
    }

    private Item Find(string id)
    {
        if (items == null || string.IsNullOrEmpty(id)) return null;
        foreach (Item item in items)
            if (item != null && item.id == id) return item;
        return null;
    }
}
