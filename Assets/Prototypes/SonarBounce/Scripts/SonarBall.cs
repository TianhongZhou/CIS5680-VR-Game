using UnityEngine;


public class SonarBall : MonoBehaviour
{
    public enum BallType { Stone, Rubber, Mud, Beacon }

    [Header("Ball Config")]
    public BallType ballType = BallType.Stone;

    [Header("Stone Ball")]
    public float stonePulseRadius = 10f;

    [Header("Rubber Ball")]
    public float rubberPulseRadius = 3f;
    public int maxBounces = 5;

    [Header("Mud Ball")]
    public float mudGlowRadius = 0.5f;

    [Header("Beacon Ball")]
    public float beaconPulseRadius = 8f;
    public float beaconInterval = 3f;
    public float beaconLifetime = 60f;

    int bounceCount = 0;
    bool hasStuck = false;
    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasStuck) return;

        Vector3 hitPoint = collision.contacts[0].point;

        switch (ballType)
        {
            case BallType.Stone:
                HandleStone(hitPoint);
                break;
            case BallType.Rubber:
                HandleRubber(hitPoint);
                break;
            case BallType.Mud:
                HandleMud(hitPoint, collision);
                break;
            case BallType.Beacon:
                HandleBeacon(hitPoint, collision);
                break;
        }
    }

    void HandleStone(Vector3 hitPoint)
    {
        PulseManager.Instance.SpawnPulse(hitPoint, stonePulseRadius);
        Destroy(gameObject, 0.1f);
    }

    void HandleRubber(Vector3 hitPoint)
    {
        bounceCount++;

        PulseManager.Instance.SpawnPulse(hitPoint, rubberPulseRadius);

        if (bounceCount >= maxBounces)
        {
            Destroy(gameObject, 0.1f);
        }
    }

    void HandleMud(Vector3 hitPoint, Collision collision)
    {
        hasStuck = true;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        transform.position = hitPoint;

        PulseManager.Instance.AddGlowPoint(hitPoint, mudGlowRadius);

    }

    void HandleBeacon(Vector3 hitPoint, Collision collision)
    {
        hasStuck = true;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        transform.position = hitPoint;

        PulseManager.Instance.StartBeacon(hitPoint, beaconPulseRadius, beaconInterval, beaconLifetime);

    }
}