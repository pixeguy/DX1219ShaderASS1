using UnityEngine;

public class DeferredLightMover : MonoBehaviour
{
    public float spotWobbleAngle = 15f;     // how much the cone can tilt
    public float spotWobbleSpeed = 0.5f;    // how fast it wiggles

    public float pointMoveSpeed = 3f;       // units per second
    public float pointPauseTime = 0.5f;     // optional small pause at each point

    DefferedLighObj lightData;
    Collider areaCollider;                  // parent area
    Vector3 baseDir;                        // starting direction for spot
    Vector3 targetPos;                      // wander target for point light
    float phaseX, phaseZ;                   // random wobble phases
    float pauseTimer = 0f;

    void Awake()
    {
        lightData = GetComponent<DefferedLighObj>();
        // assume parent has collider defining the area
        areaCollider = GetComponentInParent<Collider>();

        // remember initial forward as base spotlight direction
        baseDir = transform.forward;

        // randomize wobble phases so all lights don't sync
        phaseX = Random.value * Mathf.PI * 2f;
        phaseZ = Random.value * Mathf.PI * 2f;

        if (lightData.type == DefferedLighObj.Type.point)
        {
            PickNewTarget();
        }
    }

    void Update()
    {
        if (lightData == null) return;

        if (lightData.type == DefferedLighObj.Type.spot)
        {
            UpdateSpotlightWobble();
        }
        else if (lightData.type == DefferedLighObj.Type.point)
        {
            UpdatePointMovement();
        }
    }

    void UpdateSpotlightWobble()
    {
        float t = Time.time;

        // small wobble around base direction
        float wobX = Mathf.Sin(t * spotWobbleSpeed + phaseX) * spotWobbleAngle;
        float wobZ = Mathf.Sin(t * (spotWobbleSpeed * 0.8f) + phaseZ) * spotWobbleAngle;

        Quaternion wobble = Quaternion.Euler(wobX, 0f, wobZ);

        // keep it generally "down-ish" by blending towards Vector3.down
        Vector3 dir = wobble * baseDir;
        dir = Vector3.Slerp(dir, Vector3.down, 0.3f); // 0.3 = how strongly bias towards down

        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    void UpdatePointMovement()
    {
        if (targetPos == Vector3.zero)
            PickNewTarget();

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            pointMoveSpeed * Time.deltaTime
        );

        if ((transform.position - targetPos).sqrMagnitude < 0.1f)
        {
            pauseTimer = pointPauseTime;
            PickNewTarget();
        }
    }

    void PickNewTarget()
    {
        // Move within parent's collider bounds if it exists
        if (areaCollider != null)
        {
            Bounds b = areaCollider.bounds;
            targetPos = new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                Random.Range(b.min.z, b.max.z)
            );
        }
        else
        {
            // fallback: simple sphere around parent using light range
            Vector3 center = transform.parent ? transform.parent.position : Vector3.zero;
            float r = Mathf.Max(lightData.range, 1f);

            targetPos = center + new Vector3(
                Random.Range(-r, r),
                Random.Range(0f, r),
                Random.Range(-r, r)
            );
        }
    }
}