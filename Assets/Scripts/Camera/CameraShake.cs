using UnityEngine;

// Put this on Main Camera, alongside CameraOrbit.
// IMPORTANT: in Project Settings > Script Execution Order, add this
// script AFTER CameraOrbit. Otherwise CameraOrbit's own positioning
// (also in LateUpdate) will overwrite the shake offset every frame
// and you won't see anything happen.
public class CameraShake : MonoBehaviour
{
    [SerializeField] private float defaultDuration = 0.4f;
    [SerializeField] private float defaultMagnitude = 0.3f;

    private float remaining;
    private float duration;
    private float magnitude;

    public void Shake()
    {
        Shake(defaultDuration, defaultMagnitude);
    }

    public void Shake(float shakeDuration, float shakeMagnitude)
    {
        duration = shakeDuration;
        magnitude = shakeMagnitude;
        remaining = shakeDuration;
    }

    private void LateUpdate()
    {
        if (remaining <= 0f) return;

        // Fades out as it runs, rather than stopping abruptly
        Vector3 offset = Random.insideUnitSphere * magnitude * (remaining / duration);
        transform.localPosition += offset;

        remaining -= Time.deltaTime;
    }
}
