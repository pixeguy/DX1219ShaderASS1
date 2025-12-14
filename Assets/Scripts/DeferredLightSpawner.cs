using UnityEngine;

public class DeferredLightSpawner : MonoBehaviour
{
    public enum SpawnLightType
    {
        Spot,
        Point,
        BothRandom
    }

    [Header("Spawn Settings")]
    public int lightCount = 100;
    public Vector3 areaSize = new Vector3(30f, 5f, 30f); // XZ size, Y height
    public SpawnLightType spawnType = SpawnLightType.Spot;

    [Header("Light Settings (Common)")]
    public Color baseColor = Color.white;
    public float minIntensity = 1.0f;
    public float maxIntensity = 2.5f;

    [Range(0f, 1f)]
    public float minSmoothness = 0.4f;
    [Range(0f, 1f)]
    public float maxSmoothness = 1.0f;

    [Range(0f, 1f)]
    public float minSpecularStrength = 0.5f;
    [Range(0f, 1f)]
    public float maxSpecularStrength = 1.0f;

    [Header("Spotlight Settings (max 50°)")]
    public float minOuterAngle = 25f;
    public float maxOuterAngle = 50f;

    public float minRange = 20f;
    public float maxRange = 50f;

    void Start()
    {
        for (int i = 0; i < lightCount; i++)
        {
            GameObject go = new GameObject($"DeferredLight_{i}");
            go.transform.parent = transform;
            DefferedLighObj.Type chosenType = DefferedLighObj.Type.spot;
            // Random position inside a box centered on this spawner
            Vector3 offset;

            if (chosenType == DefferedLighObj.Type.spot)
            {
                offset = new Vector3(
                    Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                    Random.Range(0f, areaSize.y),                  
                    Random.Range(-areaSize.z * 0.5f, areaSize.z * 0.5f)
                );
            }
            else // Point light
            {
                offset = new Vector3(
                    Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                    Random.Range(-areaSize.y, 0f),                 
                    Random.Range(-areaSize.z * 0.5f, areaSize.z * 0.5f)
                );
            }

            go.transform.position = transform.position + offset;

            // Add your deferred light component
            DefferedLighObj l = go.AddComponent<DefferedLighObj>();

            // Decide type based on spawnType

            switch (spawnType)
            {
                case SpawnLightType.Spot:
                    chosenType = DefferedLighObj.Type.spot;
                    break;

                case SpawnLightType.Point:
                    chosenType = DefferedLighObj.Type.point;
                    break;

                case SpawnLightType.BothRandom:
                    chosenType = (Random.value < 0.5f)
                        ? DefferedLighObj.Type.point
                        : DefferedLighObj.Type.spot;
                    break;
            }

            l.type = chosenType;

            // Common visual settings
            Color randColor = baseColor;
            randColor.r *= Random.Range(0.7f, 1.3f);
            randColor.g *= Random.Range(0.7f, 1.3f);
            randColor.b *= Random.Range(0.7f, 1.3f);
            l.lightColor = randColor;

            l.intensity = Random.Range(minIntensity, maxIntensity);
            l.smoothness = Random.Range(minSmoothness, maxSmoothness);
            l.specularStrength = Random.Range(minSpecularStrength, maxSpecularStrength);

            // Your attenuation
            l.attenuation = new Vector3(1f, 0.027f, 0.0028f);

            // Orientation + spotlight-specific values
            if (chosenType == DefferedLighObj.Type.spot)
            {
                // Aim mostly downward with some random sideways offset
                Vector3 targetPos = go.transform.position + Vector3.down * 5f;
                Vector3 randomSide = new Vector3(
                    Random.Range(-1f, 1f),
                    0f,
                    Random.Range(-1f, 1f)
                );
                targetPos += randomSide * 2f;

                go.transform.rotation = Quaternion.LookRotation(
                    (targetPos - go.transform.position).normalized,
                    Vector3.up
                );

                float outer = Random.Range(minOuterAngle, maxOuterAngle);
                float inner = outer * 0.7f;

                l.spotLightCutOff = outer;
                l.spotLightInnerCutOff = inner;
            }
            else
            {
                // Point light – direction doesn’t really matter
                go.transform.rotation = Quaternion.identity;
                l.spotLightCutOff = 0f;
                l.spotLightInnerCutOff = 0f;

                float range = Random.Range(minRange, maxRange);
                l.range = range;
            }

            // NEW: add movement / wobble controller
            go.AddComponent<DeferredLightMover>();
        }
    }
}
