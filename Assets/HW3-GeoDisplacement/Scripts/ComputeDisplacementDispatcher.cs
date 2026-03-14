using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[StructLayout(LayoutKind.Sequential)]
struct TriangleData
{
    public Vector3 offset;
    public Vector3 velocity;
    public float lifetime;
    public float scale;
}

class ComputeDisplacementDispatcher : MonoBehaviour
{
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private SceneControls controls;

    private Mesh _mesh;
    private GraphicsBuffer _sourcePositions;
    private GraphicsBuffer _meshVertexBuffer;
    private ComputeBuffer _triangleBuffer;
    private int _triangleCount;
    private int _kernel;
    private float _lastTime = -1f;
    private int _lastSeed;

    // Cached mesh data for re-initialization
    private Vector3[] _flatVerts;

    private void Awake()
    {
        computeShader = Instantiate(computeShader);

        _mesh = GetComponent<MeshFilter>().mesh;

        // Unindex: give each triangle its own 3 vertices so compute threads
        // never race on shared vertices — mirrors how a geometry shader works.
        int[] tris = _mesh.triangles;
        Vector3[] srcVerts = _mesh.vertices;
        int flatCount = tris.Length;
        _flatVerts = new Vector3[flatCount];
        for (int i = 0; i < flatCount; i++)
            _flatVerts[i] = srcVerts[tris[i]];

        int[] seqIndices = new int[flatCount];
        for (int i = 0; i < flatCount; i++) seqIndices[i] = i;

        _mesh.vertices = _flatVerts;
        _mesh.triangles = seqIndices;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        _mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

        _sourcePositions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, flatCount, 3 * sizeof(float));
        _sourcePositions.SetData(_flatVerts);

        _meshVertexBuffer = _mesh.GetVertexBuffer(0);
        _triangleCount = flatCount / 3;

        _triangleBuffer = new ComputeBuffer(_triangleCount, Marshal.SizeOf<TriangleData>());

        _kernel = computeShader.FindKernel("CSMain");
        computeShader.SetInt("_VertexStride", _mesh.GetVertexBufferStride(0) / sizeof(float));
        computeShader.SetBuffer(_kernel, "_SourcePositions", _sourcePositions);
        computeShader.SetBuffer(_kernel, "_Vertices", _meshVertexBuffer);
        computeShader.SetBuffer(_kernel, "_TriangleBuffer", _triangleBuffer);
    }

    private void OnEnable() => controls.OnTimeOrSeedChanged += OnTimeOrSeedChanged;
    private void OnDisable() => controls.OnTimeOrSeedChanged -= OnTimeOrSeedChanged;

    private void OnDestroy()
    {
        _sourcePositions?.Dispose();
        _meshVertexBuffer?.Dispose();
        _triangleBuffer?.Release();
    }

    private void InitBuffer(int seed)
    {
        Random.InitState(seed);
        Matrix4x4 ltw = transform.localToWorldMatrix;
        TriangleData[] data = new TriangleData[_triangleCount];

        for (int i = 0; i < _triangleCount; i++)
        {
            Vector3 v0 = _flatVerts[i * 3];
            Vector3 v1 = _flatVerts[i * 3 + 1];
            Vector3 v2 = _flatVerts[i * 3 + 2];

            // Face normal in world space
            Vector3 normalOS = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            Vector3 normalWS = ltw.MultiplyVector(normalOS).normalized;

            float speed = Random.Range(10f, 16f);
            Vector3 spread = Random.insideUnitSphere * 3f;

            data[i] = new TriangleData
            {
                offset = Vector3.zero,
                velocity = normalWS * speed + spread,
                lifetime = Random.Range(1.0f, 1.5f),
                scale = 1f
            };
        }

        _triangleBuffer.SetData(data);
    }

    private void OnTimeOrSeedChanged(float time, int seed)
    {
        // Reset on seed change or time rewinding (loop / scrub backward)
        if (seed != _lastSeed || time < _lastTime)
        {
            InitBuffer(seed);
            _lastSeed = seed;
            _lastTime = time;
            return;
        }

        float dt = time - _lastTime;
        _lastTime = time;
        if (dt <= 0f) return;

        computeShader.SetFloat("_DeltaTime", dt);
        computeShader.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);
        computeShader.SetMatrix("_WorldToObject", transform.worldToLocalMatrix);
        int groups = (_triangleCount + 63) / 64;
        computeShader.Dispatch(_kernel, groups, 1, 1);
        _mesh.RecalculateBounds();
    }
}