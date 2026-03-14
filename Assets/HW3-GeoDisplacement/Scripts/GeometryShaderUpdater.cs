using Unity.VisualScripting;
using UnityEngine;

class GeometryShaderUpdater : MonoBehaviour
{
    [SerializeField] private Material material;
    [SerializeField] private SceneControls controls;

    private void OnEnable()
    {
        controls.OnTimeOrSeedChanged += OnTimeOrSeedChanged;
    }

    private void OnDisable()
    {
        controls.OnTimeOrSeedChanged -= OnTimeOrSeedChanged;
    }

    private void OnTimeOrSeedChanged(float time, int seed)
    {
        material.SetFloat("_SceneTime", time);
        material.SetInt("_SceneSeed", seed);
    }
}