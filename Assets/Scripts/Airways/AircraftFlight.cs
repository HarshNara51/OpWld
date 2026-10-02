using System;
using UnityEngine;

// Put this on the ROOT of each aircraft prefab (helicopter, plane).
// The AirTrafficSpawner tells it where to start and end; it flies a
// gentle curve between them, banks into turns, then removes itself.
public class AircraftFlight : MonoBehaviour
{
    [Header("Flight")]
    [Tooltip("Units per second. Helicopter ~35, plane ~90")]
    public float speed = 60f;

    [Tooltip("Height range above the spawner object (min, max)")]
    public Vector2 altitudeRange = new Vector2(120f, 200f);

    [Tooltip("0 = dead straight, 1 = big sweeping curve")]
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

    private Vector3 a, b, c;      // start, curve control, end
    private float length;
    private float t;
    private float roll;
    private Vector3 lastTangent;
    private bool flying;
    private Action onFinished;

    public void Fly(Vector3 start, Vector3 control, Vector3 end, Action finished)
    {
        a = start; b = control; c = end;
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

        t += speed * Time.deltaTime / length;

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

    // Quadratic Bezier curve
    private Vector3 Point(float u)
    {
        float m = 1f - u;
        return m * m * a + 2f * m * u * b + u * u * c;
    }

    private Vector3 Tangent(float u)
    {
        return 2f * (1f - u) * (b - a) + 2f * u * (c - b);
    }

    private float ApproxLength()
    {
        float total = 0f;
        Vector3 prev = a;
        for (int i = 1; i <= 20; i++)
        {
            Vector3 p = Point(i / 20f);
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
