using UnityEngine;

public class PassAmbientLight : MonoBehaviour
{
    public float ambientLightStrength;
    public Color ambientLightCol;

    private void Update()
    {
        Shader.SetGlobalColor("_ambientLightCol",ambientLightCol);
        Shader.SetGlobalFloat("_ambientLightStrength",ambientLightStrength);
    }
}
