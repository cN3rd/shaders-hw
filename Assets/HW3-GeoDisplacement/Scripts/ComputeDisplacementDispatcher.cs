using UnityEngine;
using UnityEngine.Rendering;

class ComputeDisplacementDispatcher : MonoBehaviour
{
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private SceneControls controls;

    private Mesh _mesh;
    private GraphicsBuffer _sourcePositions;
    private GraphicsBuffer _meshVertexBuffer;
    private int _triangleCount;
    private int _kernel;

    private void Awake()
    {
        computeShader = Instantiate(computeShader);

        _mesh = GetComponent<MeshFilter>().mesh;

        // Unindex: give each triangle its own 3 vertices so compute threads
        // never race on shared vertices — mirrors how a geometry shader works.
        int[] tris = _mesh.triangles;
        Vector3[] srcVerts = _mesh.vertices;
        int flatCount = tris.Length;
        Vector3[] flat = new Vector3[flatCount];
        for (int i = 0; i < flatCount; i++)
            flat[i] = srcVerts[tris[i]];

        int[] seqIndices = new int[flatCount];
        for (int i = 0; i < flatCount; i++) seqIndices[i] = i;

        _mesh.vertices = flat;
        _mesh.triangles = seqIndices;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        _mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

        _sourcePositions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, flatCount, 3 * sizeof(float));
        _sourcePositions.SetData(flat);

        _meshVertexBuffer = _mesh.GetVertexBuffer(0);
        _triangleCount = flatCount / 3;

        _kernel = computeShader.FindKernel("CSMain");
        computeShader.SetInt("_VertexStride", _mesh.GetVertexBufferStride(0) / sizeof(float));
        computeShader.SetBuffer(_kernel, "_SourcePositions", _sourcePositions);
        computeShader.SetBuffer(_kernel, "_Vertices", _meshVertexBuffer);
    }

    private void OnEnable() => controls.OnTimeOrSeedChanged += OnTimeOrSeedChanged;
    private void OnDisable() => controls.OnTimeOrSeedChanged -= OnTimeOrSeedChanged;

    private void OnDestroy()
    {
        _sourcePositions?.Dispose();
        _meshVertexBuffer?.Dispose();
    }

    private void OnTimeOrSeedChanged(float time, int seed)
    {
        computeShader.SetFloat("_SceneTime", time);
        computeShader.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);
        computeShader.SetMatrix("_WorldToObject", transform.worldToLocalMatrix);
        int groups = (_triangleCount + 63) / 64;
        computeShader.Dispatch(_kernel, groups, 1, 1);
        _mesh.RecalculateBounds();
    }
}