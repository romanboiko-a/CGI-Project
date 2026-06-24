using UnityEngine;

public class bodycontroller : MonoBehaviour
{
    //Run parameters
    [SerializeField] public int bodyCount = 1000 * 64;
    [SerializeField] Mesh bodyMesh;
    [SerializeField] float dotmass = 1f; //body mass
    [SerializeField] float diskradius = 1f; //galaxy disk radius
    [SerializeField] float haloradius = 1f; //galaxy halo radius
    [SerializeField] string scenario = "sphererandom"; // "sphererandom", "diskspin"
    [SerializeField] ComputeShader nbodyShader;
    [SerializeField] float G = 1.0f;
    [SerializeField] float timeStep = 0.01f;
    [SerializeField] float alpha = 1.0f; // For disk distribution, higher alpha means more mass concentrated in the center
    [SerializeField] int blockDim = 256;

    public Material bodyMaterial;
    //Buffers for GPU computation and rendering
    ComputeBuffer positionsMassBuffer;
    ComputeBuffer velocitiesBuffer;
    ComputeBuffer accelerationsBuffer;
    ComputeBuffer argsBuffer;
    ComputeBuffer colorsBuffer;
    // Start is called before the first frame update
    void Start()
    {
        positionsMassBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        velocitiesBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        accelerationsBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        colorsBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);

        bodyMaterial.SetBuffer("_PositionsMass", positionsMassBuffer);
        bodyMaterial.SetBuffer("_Colors", colorsBuffer);

        Vector4[] positionsMass = new Vector4[bodyCount];
        Vector4[] velocities = new Vector4[bodyCount];
        Vector4[] accelerations = new Vector4[bodyCount];
        Vector4[] colors = new Vector4[bodyCount];
        float randexp()
        {   
            float rand = diskradius+1;
            while (rand > diskradius)
            {
                float u = Random.value;
                rand = -Mathf.Log(1f-u)/alpha;
            }
            return rand;
        }
        float randhalo()
        {
            float u = Random.value;
            float theta = (2f * Mathf.PI - Mathf.Acos(- u)) / 3f;
            return haloradius * 2f * Mathf.Cos(theta);
        }


        //Parabolic collision (Main scenario)
        if(scenario == "parabolic"){

            //Galaxy core #1
            positionsMass[0] = new Vector4(100, 0, -100, dotmass*bodyCount/10);
            velocities[0] = Vector4.zero;
            accelerations[0] = Vector4.zero;
            colors[0] = new Vector4(1, 0, 0, 1f);
            //Galaxy core #2
            positionsMass[1] = new Vector4(-100, 0, 100, dotmass*bodyCount/10);
            velocities[1] = Vector4.zero;
            accelerations[1] = Vector4.zero;
            colors[1] = new Vector4(1, 0, 0, 1f);

            //Disk #1
            for (int i = 2; i < bodyCount/4+1; i++)
            {
                float r = randexp(); // Random radius with exponential distribution
                float theta = Random.Range(0f, Mathf.PI * 2f); // Random angle
                float x = r * Mathf.Cos(theta); //transform to cartesian coordinates
                float z = r * Mathf.Sin(theta);

                positionsMass[i] = new Vector4(x + 100, 0, z - 100, dotmass);

                velocities[i] = Vector4.zero; //TODO

                accelerations[i] = Vector4.zero;
                colors[i] = new Vector4(1, 0.5f, 0, 1f);
            }

            //Disk #2
            for (int i = bodyCount/4+1; i < bodyCount/2; i++)
            {
                float r = randexp(); // Random radius with exponential distribution
                float theta = Random.Range(0f, Mathf.PI * 2f); // Random angle
                float x = r * Mathf.Cos(theta); //transform to cartesian coordinates
                float z = r * Mathf.Sin(theta);
                positionsMass[i] = new Vector4(x - 100, 0, z + 100, dotmass);

                velocities[i] = Vector4.zero; //TODO

                accelerations[i] = Vector4.zero;
                colors[i] = new Vector4(0.5f, 1, 0, 1f);
            }
            //Halo #1
            for (int i = bodyCount/2; i < bodyCount*3/4; i++)
            {
                float r = randhalo(); // Random radius with halo distribution
                Vector3 position = Random.onUnitSphere * r; // Random position
                positionsMass[i] = new Vector4(position.x + 100, position.y, position.z - 100, dotmass);

                velocities[i] = Vector4.zero;//TODO

                accelerations[i] = Vector4.zero;
                colors[i] = new Vector4(0, 1, 0.5f, 1f);
            }
            //Halo #2
            for (int i = bodyCount*3/4; i < bodyCount; i++)
            {
                float r = randhalo(); // Random radius with halo distribution
                Vector3 position = Random.onUnitSphere * r; // Random position
                positionsMass[i] = new Vector4(position.x - 100, position.y, position.z + 100, dotmass);

                velocities[i] = Vector4.zero;//TODO

                accelerations[i] = Vector4.zero;
                colors[i] = new Vector4(0, 0.5f, 1, 1f);
            }

            //Disk #1 speeds
            for (int i = 2; i < bodyCount/4+1; i++)
            {
                Vector3 pos = new Vector3(positionsMass[i].x - 100, positionsMass[i].y, positionsMass[i].z + 100); //Relative position to galaxy core #1
                Vector3 dir = new Vector3(-pos.z, 0, pos.x).normalized; // Perpendicular direction
                float innermass = 0f;
                for (int j = 2; j<bodyCount/4+1; j++) //inner mass of disk #1
                {
                    if (Mathf.Pow((positionsMass[j].x - 100), 2) + Mathf.Pow((positionsMass[j].z + 100), 2) < Mathf.Pow((pos.x), 2) + Mathf.Pow((pos.z), 2))
                    {
                        innermass += positionsMass[j].w;
                    }
                }
                for (int j = bodyCount/2; j<bodyCount*3/4; j++) //inner mass of halo #1
                {
                    if (Mathf.Pow((positionsMass[j].x - 100), 2) + Mathf.Pow((positionsMass[j].z + 100), 2) < Mathf.Pow((pos.x), 2) + Mathf.Pow((pos.z), 2))
                    {
                        innermass += positionsMass[j].w;
                    }
                }
                float speed = Mathf.Sqrt(G * innermass / pos.magnitude); // Circular orbit speed
                velocities[i] = new Vector4(dir.x * speed, dir.y * speed, dir.z * speed, 0);
            }
            //Disk #2 speeds
            for (int i = bodyCount/4+1; i < bodyCount/2; i++)
            {
                Vector3 pos = new Vector3(positionsMass[i].x + 100, positionsMass[i].y, positionsMass[i].z - 100); //Relative position to galaxy core #2
                Vector3 dir = new Vector3(-pos.z, 0, pos.x).normalized; // Perpendicular direction
                float innermass = 0f;
                for (int j = bodyCount/4+1; j < bodyCount/2; j++) //inner mass of disk #2
                {
                    if (Mathf.Pow((positionsMass[j].x + 100), 2) + Mathf.Pow((positionsMass[j].z - 100), 2) < Mathf.Pow((pos.x), 2) + Mathf.Pow((pos.z), 2))
                    {
                        innermass += positionsMass[j].w;
                    }
                }
                for (int j = bodyCount*3/4; j < bodyCount; j++) //inner mass of halo #2
                {
                    if (Mathf.Pow((positionsMass[j].x + 100), 2) + Mathf.Pow((positionsMass[j].z - 100), 2) < Mathf.Pow((pos.x), 2) + Mathf.Pow((pos.z), 2))
                    {
                        innermass += positionsMass[j].w;
                    }
                }
                float speed = Mathf.Sqrt(G * innermass / pos.magnitude); // Circular orbit speed
                velocities[i] = new Vector4(dir.x * speed, dir.y * speed, dir.z * speed, 0);    
            }
        }

        positionsMassBuffer.SetData(positionsMass);
        velocitiesBuffer.SetData(velocities);
        accelerationsBuffer.SetData(accelerations);
        colorsBuffer.SetData(colors);

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