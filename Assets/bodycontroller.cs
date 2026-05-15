using UnityEngine;

public class bodycontroller : MonoBehaviour
{
    [SerializeField] int bodyCount = 1000 * 64;
    [SerializeField] Mesh bodyMesh;
    [SerializeField] int Radius = 10;
    [SerializeField] string scenario = "sphererandom"; // "sphererandom", "diskspin"
    public Material bodyMaterial;

    [SerializeField] ComputeShader nbodyShader;
    ComputeBuffer positionsMassBuffer;
    ComputeBuffer velocitiesBuffer;
    ComputeBuffer accelerationsBuffer;
    ComputeBuffer argsBuffer;

    [SerializeField] float G = 1.0f;
    [SerializeField] float timeStep = 0.01f;
    [SerializeField] int blockDim = 256;

    void Start()
    {
        positionsMassBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        velocitiesBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        accelerationsBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);

        bodyMaterial.SetBuffer("_PositionsMass", positionsMassBuffer);

        Vector4[] positionsMass = new Vector4[bodyCount];
        Vector4[] velocities = new Vector4[bodyCount];
        Vector4[] accelerations = new Vector4[bodyCount];

        //Scenarios for initialization:
        //1. Random distribution in a sphere with random velocities (chaotic)
        if(scenario == "sphererandom"){
            for (int i = 0; i < bodyCount; i++)
            {
                Vector3 position = Random.insideUnitSphere * Radius;
                float mass = Random.Range(0.5f, 2f);
                positionsMass[i] = new Vector4(position.x, position.y, position.z, mass);
                velocities[i] = Vector4.zero;
                velocities[i].Set(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0);
                accelerations[i] = Vector4.zero;
            }
        }
        //2. Random distribution in a disk with tangential velocities (galaxy-like)
        if(scenario == "diskspin"){
            for (int i = 0; i < bodyCount; i++)
            {
                Vector3 position = Random.insideUnitCircle * Radius;
                position.z = position.y;
                position.y = 0;
                float mass = Random.Range(0.5f, 2f);
                positionsMass[i] = new Vector4(position.x, position.y, position.z, mass);
                velocities[i] = Vector4.zero;
                velocities[i].Set(-position.z/10, Random.Range(-5f, 5f), position.x/10, 0);
                accelerations[i] = Vector4.zero;
            }
        }
        //3. Random distribution in a sphere with tangential velocities (forming galaxy-like)
        if(scenario == "spherespin"){
            for (int i = 0; i < bodyCount; i++)
            {
                Vector3 position = Random.insideUnitSphere * Radius;
                float mass = Random.Range(0.5f, 2f);
                positionsMass[i] = new Vector4(position.x, position.y, position.z, mass);
                velocities[i] = Vector4.zero;
                velocities[i].Set(-position.z/10, Random.Range(-5f, 5f), position.x/10, 0);
                accelerations[i] = Vector4.zero;
            }
        }

        positionsMassBuffer.SetData(positionsMass);
        velocitiesBuffer.SetData(velocities);
        accelerationsBuffer.SetData(accelerations);

        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        uint[] args = new uint[5] { bodyMesh.GetIndexCount(0), (uint)bodyCount, 0, 0, 0 };
        argsBuffer.SetData(args);
    }

    void Update()
    {
        int tileCount = bodyCount / blockDim;

        int accelKernel = nbodyShader.FindKernel("CSMain");
        nbodyShader.SetBuffer(accelKernel, "PositionsMass", positionsMassBuffer);
        nbodyShader.SetBuffer(accelKernel, "Accelerations", accelerationsBuffer);
        nbodyShader.SetInt("BodyCount", bodyCount);
        nbodyShader.SetFloat("DeltaTime", timeStep);
        nbodyShader.SetFloat("G", G);
        nbodyShader.Dispatch(accelKernel, tileCount, 1, 1);

        int integrateKernel = nbodyShader.FindKernel("Integrate");
        nbodyShader.SetBuffer(integrateKernel, "PositionsMass", positionsMassBuffer);
        nbodyShader.SetBuffer(integrateKernel, "Velocities", velocitiesBuffer);
        nbodyShader.SetBuffer(integrateKernel, "Accelerations", accelerationsBuffer);
        nbodyShader.SetInt("BodyCount", bodyCount);
        nbodyShader.SetFloat("DeltaTime", timeStep);
        nbodyShader.Dispatch(integrateKernel, tileCount, 1, 1);

        bodyMaterial.SetBuffer("_PositionsMass", positionsMassBuffer);

        Graphics.DrawMeshInstancedIndirect(bodyMesh, 0, bodyMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000f), argsBuffer);
    }
}
