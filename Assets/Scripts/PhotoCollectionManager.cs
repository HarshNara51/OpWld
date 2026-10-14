using System;
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

        int got = CollectedCount;
        string label = string.IsNullOrEmpty(item.displayName) ? id : item.displayName;
        Debug.Log($"Photo collection: {got}/{Total} - {label}");
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
                if (item != null) PlayerPrefs.DeleteKey(ItemPrefix + item.id);
        PlayerPrefs.DeleteKey(RewardKey);
        PlayerPrefs.Save();
    }

    private Item Find(string id)
    {
        if (items == null || string.IsNullOrEmpty(id)) return null;
        foreach (Item item in items)
            if (item != null && item.id == id) return item;
        return null;
    }
}
