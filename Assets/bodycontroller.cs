using UnityEngine;

public class bodycontroller : MonoBehaviour
{
    //Run parameters
    public Material bodyMaterial;
    [SerializeField] public int bodyCount = 1000 * 64;
    [SerializeField] Mesh bodyMesh;

    [SerializeField] string scenario = "sphererandom"; // "sphererandom", "diskspin"
    [SerializeField] ComputeShader nbodyShader;
    [SerializeField] int blockDim = 256;
    [SerializeField] float dotmass = 1f; //body mass
    [SerializeField] float diskradius = 1f; //galaxy disk radius
    [SerializeField] float haloradius = 1f; //galaxy halo radius
    [SerializeField] float G = 1.0f;
    [SerializeField] float eps = 1f; //softening length
    [SerializeField] float timeStep = 0.01f;
    [SerializeField] float alpha = 1.0f; // For disk distribution, higher alpha means more mass concentrated in the center
    [SerializeField] float xoffset = 0f; //x offset of the galaxies
    [SerializeField] float zoffset = 0f; //z offset of the galaxies

    //Buffers for GPU computation and rendering
    ComputeBuffer positionsMassBuffer;
    ComputeBuffer velocitiesBuffer;
    ComputeBuffer accelerationsBuffer;
    ComputeBuffer argsBuffer;
    ComputeBuffer colorsBuffer;
    ComputeBuffer potentialsBuffer;

    // Start is called before the first frame update
    void Start()
    {
        positionsMassBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        velocitiesBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        accelerationsBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        colorsBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        potentialsBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);

        bodyMaterial.SetBuffer("_PositionsMass", positionsMassBuffer);
        bodyMaterial.SetBuffer("_Colors", colorsBuffer);

        Vector4[] positionsMass = new Vector4[bodyCount];
        Vector4[] velocities = new Vector4[bodyCount];
        Vector4[] accelerations = new Vector4[bodyCount];
        Vector4[] colors = new Vector4[bodyCount];
        Vector4[] potentials = new Vector4[bodyCount];

        float randexp()
        {
            float rand = diskradius + 1;
            while (rand > diskradius)
            {
                float u = Random.value;
                rand = -Mathf.Log(1f - u) / alpha;
            }
            return rand;
        }
        float randhalo()
        {
            float u = Random.value;
            float theta = (2f * Mathf.PI - Mathf.Acos(-u)) / 3f;
            return haloradius * 2f * Mathf.Cos(theta);
        }


        //Parabolic collision (Main scenario)
        if (scenario == "parabolic")
        {

            //Galaxy core #1
            positionsMass[0] = new Vector4(xoffset, 0, -zoffset, dotmass * bodyCount / 10);
            velocities[0] = Vector4.zero;
            accelerations[0] = Vector4.zero;
            colors[0] = new Vector4(1, 0, 0, 1f);
            //Galaxy core #2
            positionsMass[1] = new Vector4(-xoffset, 0, zoffset, dotmass * bodyCount / 10);
            velocities[1] = Vector4.zero;
            accelerations[1] = Vector4.zero;
            colors[1] = new Vector4(1, 0, 0, 1f);

            //Disk #1
            for (int i = 2; i < bodyCount / 4 + 1; i++)
            {
                float r = randexp(); // Random radius with exponential distribution
                float theta = Random.Range(0f, Mathf.PI * 2f); // Random angle
                float x = r * Mathf.Cos(theta); //transform to cartesian coordinates
                float z = r * Mathf.Sin(theta);

                positionsMass[i] = new Vector4(x + xoffset, 0, z - zoffset, dotmass);

                velocities[i] = Vector4.zero; //TODO

                accelerations[i] = Vector4.zero;
                colors[i] = new Vector4(1, 0.5f, 0, 1f);
            }

            //Disk #2
            for (int i = bodyCount / 4 + 1; i < bodyCount / 2; i++)
            {
                float r = randexp(); // Random radius with exponential distribution
                float theta = Random.Range(0f, Mathf.PI * 2f); // Random angle
                float x = r * Mathf.Cos(theta); //transform to cartesian coordinates
                float z = r * Mathf.Sin(theta);
                positionsMass[i] = new Vector4(x - xoffset, 0, z + zoffset, dotmass);

                velocities[i] = Vector4.zero; //TODO

                accelerations[i] = Vector4.zero;
                colors[i] = new Vector4(0.5f, 1, 0, 1f);
            }
            //Halo #1
            for (int i = bodyCount / 2; i < bodyCount * 3 / 4; i++)
            {
                float r = randhalo(); // Random radius with halo distribution
                Vector3 position = Random.onUnitSphere * r; // Random position
                positionsMass[i] = new Vector4(position.x + xoffset, position.y, position.z - zoffset, dotmass);

                velocities[i] = Vector4.zero;//TODO

                accelerations[i] = Vector4.zero;
                colors[i] = new Vector4(0, 1, 0.5f, 1f);
            }
            //Halo #2
            for (int i = bodyCount * 3 / 4; i < bodyCount; i++)
            {
                float r = randhalo(); // Random radius with halo distribution
                Vector3 position = Random.onUnitSphere * r; // Random position
                positionsMass[i] = new Vector4(position.x - xoffset, position.y, position.z + zoffset, dotmass);

                velocities[i] = Vector4.zero;//TODO

                accelerations[i] = Vector4.zero;
                colors[i] = new Vector4(0, 0.5f, 1, 1f);
            }
            positionsMassBuffer.SetData(positionsMass);
            velocitiesBuffer.SetData(velocities);
            accelerationsBuffer.SetData(accelerations);
            potentialsBuffer.SetData(potentials);
            colorsBuffer.SetData(colors);

            argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            uint[] args = new uint[5] { bodyMesh.GetIndexCount(0), (uint)bodyCount, 0, 0, 0 };
            argsBuffer.SetData(args);

            int tileCount = bodyCount / blockDim;

            int accelKernel = nbodyShader.FindKernel("CSMain"); //Static acceleration calculation for initial velocities
            nbodyShader.SetBuffer(accelKernel, "PositionsMass", positionsMassBuffer);
            nbodyShader.SetBuffer(accelKernel, "Accelerations", accelerationsBuffer);
            nbodyShader.SetInt("BodyCount", bodyCount);
            nbodyShader.SetFloat("DeltaTime", timeStep);
            nbodyShader.SetFloat("G", G);
            nbodyShader.SetFloat("EPS", eps);
            nbodyShader.Dispatch(accelKernel, tileCount, 1, 1);

            accelerationsBuffer.GetData(accelerations); //Get accelerations from GPU

            int potKernel = nbodyShader.FindKernel("Potential"); //Static potential calculation for initial velocities
            nbodyShader.SetBuffer(potKernel, "PositionsMass", positionsMassBuffer);
            nbodyShader.SetBuffer(potKernel, "Potentials", potentialsBuffer);
            nbodyShader.SetInt("BodyCount", bodyCount);
            nbodyShader.SetFloat("DeltaTime", timeStep);
            nbodyShader.SetFloat("G", G);
            nbodyShader.SetFloat("EPS", eps);
            nbodyShader.Dispatch(potKernel, tileCount, 1, 1);

            potentialsBuffer.GetData(potentials); //Get potentials from GPU

            //Disk #1 speeds
            for (int i = 2; i < bodyCount / 4 + 1; i++)
            {
                Vector3 pos = new Vector3(positionsMass[i].x - xoffset, positionsMass[i].y, positionsMass[i].z + zoffset); //Relative position to galaxy core #1
                Vector3 dir = new Vector3(-pos.z, 0, pos.x).normalized; // Perpendicular direction
                float arad = -Vector3.Dot(accelerations[i], pos / pos.magnitude); // Radial acceleration
                float speed = Mathf.Sqrt(arad * pos.magnitude); // Circular orbit speed
                velocities[i] = new Vector4(dir.x * speed, dir.y * speed, dir.z * speed, 0);
            }
            //Disk #2 speeds
            for (int i = bodyCount / 4 + 1; i < bodyCount / 2; i++)
            {
                Vector3 pos = new Vector3(positionsMass[i].x + xoffset, positionsMass[i].y, positionsMass[i].z - zoffset); //Relative position to galaxy core #2
                Vector3 dir = new Vector3(-pos.z, 0, pos.x).normalized; // Perpendicular direction
                float arad = -Vector3.Dot(accelerations[i], pos / pos.magnitude); // Radial acceleration
                float speed = Mathf.Sqrt(arad * pos.magnitude); // Circular orbit speed
                velocities[i] = new Vector4(dir.x * speed, dir.y * speed, dir.z * speed, 0);
            }
            //Halo #1 speeds
            for (int i = bodyCount / 2; i < bodyCount * 3 / 4; i++)
            {
                float Ui = potentials[i].x; //local potential
                float speed = Mathf.Sqrt(0.5f * Ui); // Circular orbit speed
                Vector3 pos = new Vector3(positionsMass[i].x - xoffset, positionsMass[i].y, positionsMass[i].z + zoffset); //Relative position to galaxy core #1
                Vector3 dir = Random.onUnitSphere; // Random direction
                velocities[i] = new Vector4(dir.x * speed, dir.y * speed, dir.z * speed, 0);
            }
            //Halo #2 speeds
            for (int i = bodyCount * 3 / 4; i < bodyCount; i++)
            {
                float Ui = potentials[i].x; //local potential
                float speed = Mathf.Sqrt(0.5f * Ui); // Circular orbit speed
                Vector3 pos = new Vector3(positionsMass[i].x + xoffset, positionsMass[i].y, positionsMass[i].z - zoffset); //Relative position to galaxy core #2
                Vector3 dir = Random.onUnitSphere; // Random direction
                velocities[i] = new Vector4(dir.x * speed, dir.y * speed, dir.z * speed, 0);
            }
            

            for (int i = 0; i < bodyCount; i++)
            {
                if(float.IsNaN(velocities[i].x))
                {
                    velocities[i] = Vector4.zero;
                }
            }


            velocitiesBuffer.SetData(velocities);
        }


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
        nbodyShader.SetFloat("EPS", eps);
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