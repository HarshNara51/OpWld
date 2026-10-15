using UnityEngine;

// Put this on any object that's part of the photo hunt (a dinosaur, a
// statue, a weird sign...). Give it the same ID as on the list in
// PhotoCollectionManager. Photograph it in Camera Mode to collect it.
// Only counts in free roam - never while a mission is running.
public class PhotoCollectible : PhotoTarget
{
    [Header("Photo hunt")]
    [Tooltip("Must match an ID on PhotoCollectionManager's list (Managers prefab)")]
    public string collectibleId;

    // New collectibles start without the 'worth a photo' hint - keeps them secret
    private void Reset()
    {
        proximityHint = "";
        maxDistance = 40f;
    }

    protected override bool CanBePhotographed()
    {
        PhotoCollectionManager hunt = PhotoCollectionManager.Instance;
        return hunt != null
               && hunt.IsOnList(collectibleId)
               && !hunt.IsCollected(collectibleId)
               && !MissionStatus.IsMissionInProgress();
    }

    protected override void HandlePhotographed()
    {
        PhotoCollectionManager.Instance.Collect(collectibleId);
    }
}
