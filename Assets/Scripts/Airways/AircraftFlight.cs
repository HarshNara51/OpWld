using System;
using UnityEngine;

// Put this on the ROOT of each aircraft prefab (helicopter, plane).
// The AirTrafficSpawner gives it a path (start, two curve handles, end);
// it flies along it - straight, arcing, or turning mid-flight - banks
// into turns, then removes itself at the far side.
public class AircraftFlight : MonoBehaviour
{
    [Header("Flight")]
    [Tooltip("Units per second. Helicopter ~35, plane ~90")]
    public float speed = 60f;

    [Tooltip("Height range above the spawner object (min, max)")]
    public Vector2 altitudeRange = new Vector2(120f, 200f);

    [Tooltip("0 = prefers straight paths, 1 = prefers big curves and turns")]
    [Range(0f, 1f)] public float curviness = 0.3f;

    [Header("Attitude")]
    [Tooltip("Max roll (degrees) when turning")]
    public float maxBankAngle = 25f;

    [Tooltip("How strongly it banks into turns")]
    public float bankStrength = 3f;

    [Tooltip("Constant nose-down tilt in degrees - helicopters look right at ~8")]
    public float cruisePitch = 0f;

    [Tooltip("Rotates the model if its nose doesn't point along +Z (try Y = 90, -90 or 180)")]
    public Vector3 modelRotationFix;

    private Vector3 p0, p1, p2, p3;   // cubic Bezier: start, handle, handle, end
    private float length;
    private float t;
    private float roll;
    private Vector3 lastTangent;
    private bool flying;
    private Action onFinished;

    public void Fly(Vector3 start, Vector3 handle1, Vector3 handle2, Vector3 end, Action finished)
    {
        p0 = start; p1 = handle1; p2 = handle2; p3 = end;
        onFinished = finished;
        length = Mathf.Max(1f, ApproxLength());
        t = 0f;
        roll = 0f;
        lastTangent = Tangent(0f);
        flying = true;

        transform.position = start;
        ApplyRotation(lastTangent);
    }

    private void Update()
    {
        if (!flying) return;

        // Advance by real distance, so speed stays even through turns
        float speedFactor = Mathf.Max(0.01f, Tangent(t).magnitude);
        t += speed * Time.deltaTime / speedFactor;

        if (t >= 1f)
        {
            flying = false;
            onFinished?.Invoke();
            Destroy(gameObject);
            return;
        }

        Vector3 tangent = Tangent(t);

        // Turn rate (degrees/sec around the up axis) drives the bank
        float turnRate = Vector3.SignedAngle(Flat(lastTangent), Flat(tangent), Vector3.up)
                         / Mathf.Max(Time.deltaTime, 0.0001f);
        float targetRoll = Mathf.Clamp(-turnRate * bankStrength, -maxBankAngle, maxBankAngle);
        roll = Mathf.Lerp(roll, targetRoll, 2f * Time.deltaTime);
        lastTangent = tangent;

        transform.position = Point(t);
        ApplyRotation(tangent);
    }

    private void ApplyRotation(Vector3 tangent)
    {
        if (tangent.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up)
                             * Quaternion.Euler(cruisePitch, 0f, roll)
                             * Quaternion.Euler(modelRotationFix);
    }

    // Cubic Bezier curve
    private Vector3 Point(float u)
    {
        float m = 1f - u;
        return m * m * m * p0 + 3f * m * m * u * p1 + 3f * m * u * u * p2 + u * u * u * p3;
    }

    private Vector3 Tangent(float u)
    {
        float m = 1f - u;
        return 3f * m * m * (p1 - p0) + 6f * m * u * (p2 - p1) + 3f * u * u * (p3 - p2);
    }

    private float ApproxLength()
    {
        float total = 0f;
        Vector3 prev = p0;
        for (int i = 1; i <= 30; i++)
        {
            Vector3 p = Point(i / 30f);
            total += Vector3.Distance(prev, p);
            prev = p;
        }
        return total;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}