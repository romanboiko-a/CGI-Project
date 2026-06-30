using UnityEngine;

public class bodycontroller : MonoBehaviour
{
    //Run parameters
    public Material bodyMaterial;
    [SerializeField] public int bodyCount = 1000 * 64;
    [SerializeField] Mesh bodyMesh;

    [SerializeField] ComputeShader nbodyShader; //shader assignment
    [SerializeField] int blockDim = 256; //block dimension for GPU computation
    [SerializeField] float dotmass = 1f; //body mass
    [SerializeField] float diskradius = 1f; //galaxy disk radius
    [SerializeField] float haloradius = 1f; //galaxy halo radius
    [SerializeField] float halodisk_ratio = 1f; //ratio of halo mass to disk mass
    [SerializeField] float G = 1.0f; //gravitational constant
    [SerializeField] float eps = 1f; //softening length
    [SerializeField] float timeStep = 0.01f; //time step for integration
    [SerializeField] float alpha = 1.0f; // For disk distribution, higher alpha means more mass concentrated in the center
    [SerializeField] float xoffset = 0f; //x offset of the galaxies
    [SerializeField] float zoffset = 0f; //z offset of the galaxies
    [SerializeField] float yoffset = 0f; //y offset of the galaxies
    [SerializeField] float xangle1 = 0f; //rotation angle of the galaxy 1 around the x axis
    [SerializeField] float zangle1 = 0f; //rotation angle of the galaxy 1 around the z axis
    [SerializeField] float xangle2 = 0f; //rotation angle of the galaxy 2 around the x axis
    [SerializeField] float zangle2 = 0f; //rotation angle of the galaxy 2 around the z axis
    [SerializeField] float initialvelocity = 0f; //initial velocity of the galaxies
    [SerializeField] int natural = 0; //toggle natural colors

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
        //Separation points for arrays
        int seppoint1 = (int)((bodyCount-2)/(halodisk_ratio+1)+2);
        int seppoint2 = (int)((2+seppoint1)/2);
        int seppoint3 = (int)((bodyCount+seppoint1)/2);

        //Buffer size assignment
        positionsMassBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        velocitiesBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        accelerationsBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        colorsBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);
        potentialsBuffer = new ComputeBuffer(bodyCount, sizeof(float) * 4);

        //Buffer assignment for rendering
        bodyMaterial.SetBuffer("_PositionsMass", positionsMassBuffer);
        bodyMaterial.SetBuffer("_Colors", colorsBuffer);

        //Arrays for initialization
        Vector4[] positionsMass = new Vector4[bodyCount];
        Vector4[] velocities = new Vector4[bodyCount];
        Vector4[] accelerations = new Vector4[bodyCount];
        Vector4[] colors = new Vector4[bodyCount];
        Vector4[] potentials = new Vector4[bodyCount];

        //Random number generator for disk distribution
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

        //Random number generator for halo distribution
        float randhalo()
        {
            float u = Random.value;
            float theta = (2f * Mathf.PI - Mathf.Acos(-u)) / 3f;
            return haloradius * 2f * Mathf.Cos(theta);
        }

        //offset for initial positions to avoid overlap and interaction, default 1000, set to 0 for 1 galaxy
        int inoffset = 1000; 

        //_______Positions and masses_______

        //Galaxy core #1
        positionsMass[0] = new Vector4(inoffset, 0, -inoffset, dotmass * bodyCount / 20);
        velocities[0] = Vector4.zero;
        accelerations[0] = Vector4.zero;
        colors[0] = new Vector4(1, 0, 0, 1f);
        //Galaxy core #2
        positionsMass[1] = new Vector4(-inoffset, 0, inoffset, dotmass * bodyCount / 20);
        velocities[1] = Vector4.zero;
        accelerations[1] = Vector4.zero;
        colors[1] = new Vector4(1, 0, 0, 1f);

        //Disk #1
        for (int i = 2; i < seppoint2; i++)
        {
            float r = randexp(); // Random radius with exponential distribution
            float theta = Random.Range(0f, Mathf.PI * 2f); // Random angle
            float x = r * Mathf.Cos(theta); //transform to cartesian coordinates
            float z = r * Mathf.Sin(theta);

            positionsMass[i] = new Vector4(x + inoffset, 0, z - inoffset, dotmass);
            accelerations[i] = Vector4.zero;
            colors[i] = new Vector4(1, 0.5f, 0, 1f);
        }

        //Disk #2
        for (int i = seppoint2; i < seppoint1; i++)
        {
            float r = randexp(); // Random radius with exponential distribution
            float theta = Random.Range(0f, Mathf.PI * 2f); // Random angle
            float x = r * Mathf.Cos(theta); //transform to cartesian coordinates
            float z = r * Mathf.Sin(theta);
            positionsMass[i] = new Vector4(x - inoffset, 0, z + inoffset, dotmass);
            accelerations[i] = Vector4.zero;
            colors[i] = new Vector4(0.5f, 1, 0, 1f);
        }

        //Halo #1
        for (int i = seppoint1; i < seppoint3; i++)
        {
            float r = randhalo(); // Random radius with halo distribution
            Vector3 position = Random.onUnitSphere * r; // Random direction
            positionsMass[i] = new Vector4(position.x + inoffset, position.y, position.z - inoffset, dotmass);
            accelerations[i] = Vector4.zero;
            colors[i] = new Vector4(0, 1, 0.5f, 1f);
        }
        //Halo #2
        for (int i = seppoint3; i < bodyCount; i++)
        {
            float r = randhalo(); // Random radius with halo distribution
            Vector3 position = Random.onUnitSphere * r; // Random direction
            positionsMass[i] = new Vector4(position.x - inoffset, position.y, position.z + inoffset, dotmass);
            accelerations[i] = Vector4.zero;
            colors[i] = new Vector4(0, 0.5f, 1, 1f);
        }

        //_____Velocities_____

        //Initial buffer assignment for velocity and potential calculations
        positionsMassBuffer.SetData(positionsMass);
        velocitiesBuffer.SetData(velocities);
        accelerationsBuffer.SetData(accelerations);
        potentialsBuffer.SetData(potentials);
        colorsBuffer.SetData(colors);

        //Arguments for shader
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        uint[] args = new uint[5] { bodyMesh.GetIndexCount(0), (uint)bodyCount, 0, 0, 0 };
        argsBuffer.SetData(args);

        int tileCount = bodyCount / blockDim;

        //Static acceleration calculation for initial velocities
        int accelKernel = nbodyShader.FindKernel("CSMain"); 
        nbodyShader.SetBuffer(accelKernel, "PositionsMass", positionsMassBuffer);
        nbodyShader.SetBuffer(accelKernel, "Accelerations", accelerationsBuffer);
        nbodyShader.SetInt("BodyCount", bodyCount);
        nbodyShader.SetFloat("DeltaTime", timeStep);
        nbodyShader.SetFloat("G", G);
        nbodyShader.SetFloat("EPS", eps);
        nbodyShader.Dispatch(accelKernel, tileCount, 1, 1);

        accelerationsBuffer.GetData(accelerations); //Get accelerations from GPU
        
        //Static potential calculation for initial velocities
        int potKernel = nbodyShader.FindKernel("Potential"); 
        nbodyShader.SetBuffer(potKernel, "PositionsMass", positionsMassBuffer);
        nbodyShader.SetBuffer(potKernel, "Potentials", potentialsBuffer);
        nbodyShader.SetInt("BodyCount", bodyCount);
        nbodyShader.SetFloat("DeltaTime", timeStep);
        nbodyShader.SetFloat("G", G);
        nbodyShader.SetFloat("EPS", eps);
        nbodyShader.Dispatch(potKernel, tileCount, 1, 1);

        potentialsBuffer.GetData(potentials); //Get potentials from GPU

        //Disk #1 speeds
        for (int i = 2; i < seppoint2; i++)
        {
            Vector3 pos = new Vector3(positionsMass[i].x - inoffset, positionsMass[i].y, positionsMass[i].z + inoffset); //Relative position to galaxy core #1
            Vector3 dir = new Vector3(-pos.z, 0, pos.x).normalized; // Perpendicular direction
            float arad = -Vector3.Dot(accelerations[i], pos / pos.magnitude); // Radial acceleration
            float speed = Mathf.Sqrt(arad * pos.magnitude); // Circular orbit speed
            velocities[i] = new Vector4(dir.x * speed, dir.y * speed, dir.z * speed, 0);
        }

        //Disk #2 speeds
        for (int i = seppoint2; i < seppoint1; i++)
        {
            Vector3 pos = new Vector3(positionsMass[i].x + inoffset, positionsMass[i].y, positionsMass[i].z - inoffset); //Relative position to galaxy core #2
            Vector3 dir = new Vector3(-pos.z, 0, pos.x).normalized; // Perpendicular direction
            float arad = -Vector3.Dot(accelerations[i], pos / pos.magnitude); // Radial acceleration
            float speed = Mathf.Sqrt(arad * pos.magnitude); // Circular orbit speed
            velocities[i] = new Vector4(dir.x * speed, dir.y * speed, dir.z * speed, 0);
        }
        //Halo #1 speeds
        for (int i = seppoint1; i < seppoint3; i++)
        {
            float Ui = potentials[i].x; //local potential
            float speed = Mathf.Sqrt(0.5f * Ui); // Circular orbit speed
            Vector3 pos = new Vector3(positionsMass[i].x - inoffset, positionsMass[i].y, positionsMass[i].z + inoffset); //Relative position to galaxy core #1
            Vector3 dir = Random.onUnitSphere; // Random direction
            velocities[i] = new Vector4(dir.x * speed, dir.y * speed, dir.z * speed, 0);
        }
        //Halo #2 speeds
        for (int i = seppoint3; i < bodyCount; i++)
        {
            float Ui = potentials[i].x; //local potential
            float speed = Mathf.Sqrt(0.5f * Ui); // Circular orbit speed
            Vector3 pos = new Vector3(positionsMass[i].x + inoffset, positionsMass[i].y, positionsMass[i].z - inoffset); //Relative position to galaxy core #2
            Vector3 dir = Random.onUnitSphere; // Random direction
            velocities[i] = new Vector4(dir.x * speed, dir.y * speed, dir.z * speed, 0);
        }
        
        //removing random NaN values from velocities, roughy 1 in 4000
        for (int i = 0; i < bodyCount; i++) 
        {
            if(float.IsNaN(velocities[i].x))
            {
                velocities[i] = Vector4.zero;
            }
        }


        //_____Translating and rotating the galaxies to the correct position and orientation_____

        //input angle conversion to radians
        xangle1 = xangle1 * Mathf.Deg2Rad;
        zangle1 = zangle1 * Mathf.Deg2Rad;
        xangle2 = xangle2 * Mathf.Deg2Rad;
        zangle2 = zangle2 * Mathf.Deg2Rad;

        //Galaxy cores
        positionsMass[0].x += xoffset-inoffset;
        positionsMass[0].y += yoffset;
        positionsMass[0].z += zoffset+inoffset;
        positionsMass[1].x += -xoffset+inoffset;
        positionsMass[1].y += -yoffset;
        positionsMass[1].z += -zoffset-inoffset;
        velocities[0].x += initialvelocity;
        velocities[1].x += -initialvelocity;

        //Galaxy #1
        for (int i = 2; i < seppoint3; i++)
        {
            //Positions
            //Removal of initial offset
            positionsMass[i].x -= inoffset;
            positionsMass[i].z += inoffset;
            //Rotation around the x axis
            float y = positionsMass[i].y * Mathf.Cos(xangle1) - positionsMass[i].z * Mathf.Sin(xangle1);
            float z = positionsMass[i].y * Mathf.Sin(xangle1) + positionsMass[i].z * Mathf.Cos(xangle1);
            positionsMass[i].y = y;
            positionsMass[i].z = z;

            //Rotation around the z axis
            float x = positionsMass[i].x * Mathf.Cos(zangle1) - positionsMass[i].y * Mathf.Sin(zangle1);
            y = positionsMass[i].x * Mathf.Sin(zangle1) + positionsMass[i].y * Mathf.Cos(zangle1);
            positionsMass[i].x = x;
            positionsMass[i].y = y;

            //Translation
            positionsMass[i].x += xoffset;
            positionsMass[i].y += yoffset;
            positionsMass[i].z += zoffset;

            //Velocities

            //Rotation around the x axis
            y = velocities[i].y * Mathf.Cos(xangle1) - velocities[i].z * Mathf.Sin(xangle1);
            z = velocities[i].y * Mathf.Sin(xangle1) + velocities[i].z * Mathf.Cos(xangle1);
            velocities[i].y = y;
            velocities[i].z = z;

            //Rotation around the z axis
            x = velocities[i].x * Mathf.Cos(zangle1) - velocities[i].y * Mathf.Sin(zangle1);
            y = velocities[i].x * Mathf.Sin(zangle1) + velocities[i].y * Mathf.Cos(zangle1);
            velocities[i].x = x;
            velocities[i].y = y;

            //Translation
            velocities[i].x += initialvelocity;
            if (i == seppoint2)
            {
                i = seppoint1-1; //skip to the halo #1
            }
        }
        //Galaxy #2
        for (int i = seppoint2; i < bodyCount; i++)
        {
            //Positions
            //Removal of initial offset
            positionsMass[i].x += inoffset;
            positionsMass[i].z -= inoffset;
            //Rotation around the x axis
            float y = positionsMass[i].y * Mathf.Cos(-xangle2) - positionsMass[i].z * Mathf.Sin(-xangle2);
            float z = positionsMass[i].y * Mathf.Sin(-xangle2) + positionsMass[i].z * Mathf.Cos(-xangle2);
            positionsMass[i].y = y;
            positionsMass[i].z = z;

            //Rotation around the z axis
            float x = positionsMass[i].x * Mathf.Cos(-zangle2) - positionsMass[i].y * Mathf.Sin(-zangle2);
            y = positionsMass[i].x * Mathf.Sin(-zangle2) + positionsMass[i].y * Mathf.Cos(-zangle2);
            positionsMass[i].x = x;
            positionsMass[i].y = y;

            //Translation
            positionsMass[i].x += -xoffset;
            positionsMass[i].y += -yoffset;
            positionsMass[i].z += -zoffset;

            //Velocities
            //Rotation around the x axis
            y = velocities[i].y * Mathf.Cos(-xangle2) - velocities[i].z * Mathf.Sin(-xangle2);
            z = velocities[i].y * Mathf.Sin(-xangle2) + velocities[i].z * Mathf.Cos(-xangle2);
            velocities[i].y = y;
            velocities[i].z = z;
            //Rotation around the z axis
            x = velocities[i].x * Mathf.Cos(-zangle2) - velocities[i].y * Mathf.Sin(-zangle2);
            y = velocities[i].x * Mathf.Sin(-zangle2) + velocities[i].y * Mathf.Cos(-zangle2);
            velocities[i].x = x;
            velocities[i].y = y;
            //Translation
            velocities[i].x += -initialvelocity;
            if (i == seppoint1)
            {
                i = seppoint3 - 1; //skip to the halo #2
            }
        }

        //_____Natural coloring_____

        if(natural == 1){
            //Color list
            Vector4[] colorListdisk = new Vector4[10]
            {
                new Vector4(1.00f, 0.95f, 0.55f, 1f),
                new Vector4(1.00f, 0.85f, 0.35f, 1f),
                new Vector4(1.00f, 0.70f, 0.25f, 1f), 
                new Vector4(1.00f, 0.55f, 0.20f, 1f),
                new Vector4(0.95f, 0.45f, 0.30f, 1f),
                new Vector4(0.90f, 0.40f, 0.50f, 1f),
                new Vector4(0.85f, 0.30f, 0.70f, 1f),
                new Vector4(0.80f, 0.25f, 0.80f, 1f),
                new Vector4(0.95f, 0.90f, 0.80f, 1f),
                new Vector4(1.00f, 0.75f, 0.60f, 1f)};
            Vector4[] colorListhalo = new Vector4[10]
            {
                new Vector4(0.60f, 0.75f, 1.00f, 1f),
                new Vector4(0.40f, 0.60f, 1.00f, 1f),
                new Vector4(0.20f, 0.50f, 1.00f, 1f),
                new Vector4(0.20f, 0.70f, 0.90f, 1f),
                new Vector4(0.30f, 0.85f, 0.80f, 1f),
                new Vector4(0.45f, 0.95f, 0.75f, 1f),
                new Vector4(0.55f, 0.90f, 0.60f, 1f),
                new Vector4(0.55f, 0.40f, 0.95f, 1f),
                new Vector4(0.65f, 0.30f, 0.90f, 1f),
                new Vector4(0.80f, 0.80f, 0.95f, 1f)
            };
            //Disk 1
            for (int i = 2; i < seppoint2; i++)
            {
                colors[i] = colorListdisk[Random.Range(0, colorListdisk.Length)];
                float r = new Vector3(positionsMass[i].x - positionsMass[0].x, positionsMass[i].y - positionsMass[0].y, positionsMass[i].z - positionsMass[0].z).magnitude;
                colors[i].x = Mathf.Clamp(colors[i].x *100/r, 0f, 1f); //Closer bodies get yellower
                colors[i].y = Mathf.Clamp(colors[i].y * 100/r, 0f, 1f);
            }
            //Disk 2
            for (int i = seppoint2; i < seppoint1; i++)
            {
                colors[i] = colorListdisk[Random.Range(0, colorListdisk.Length)];
                float r = new Vector3(positionsMass[i].x - positionsMass[1].x, positionsMass[i].y - positionsMass[1].y, positionsMass[i].z - positionsMass[1].z).magnitude;
                colors[i].x = Mathf.Clamp(colors[i].x *100/r, 0f, 1f); //Closer bodies get yellower
                colors[i].y = Mathf.Clamp(colors[i].y * 100/r, 0f, 1f);
            }
            //Halo 1
            for (int i = seppoint1; i < seppoint3; i++)
            {
                colors[i] = colorListhalo[Random.Range(0, colorListhalo.Length)];
                float r = new Vector3(positionsMass[i].x - positionsMass[0].x, positionsMass[i].y - positionsMass[0].y, positionsMass[i].z - positionsMass[0].z).magnitude;
                colors[i].x = Mathf.Clamp(colors[i].x * 100/r, 0f, 1f);
                colors[i].y = Mathf.Clamp(colors[i].y * 100/r, 0f, 1f); //Closer bodies get whiter
                colors[i].z = Mathf.Clamp(colors[i].z * 70/r, 0f, 1f);
            }
            //Halo 2
            for (int i = seppoint3; i < bodyCount; i++)
            {
                colors[i] = colorListhalo[Random.Range(0, colorListhalo.Length)];
                float r = new Vector3(positionsMass[i].x - positionsMass[1].x, positionsMass[i].y - positionsMass[1].y, positionsMass[i].z - positionsMass[1].z).magnitude;
                colors[i].x = Mathf.Clamp(colors[i].x * 100/r, 0f, 1f);
                colors[i].y = Mathf.Clamp(colors[i].y * 100/r, 0f, 1f); //Closer bodies get whiter
                colors[i].z = Mathf.Clamp(colors[i].z * 70/r, 0f, 1f);
            }
        }
        

        colorsBuffer.SetData(colors);
        positionsMassBuffer.SetData(positionsMass);
        velocitiesBuffer.SetData(velocities);        


    }
    
    // Update is called before every new frame
    void Update()
    {
        int tileCount = bodyCount / blockDim;

        //Acceleration calculation
        int accelKernel = nbodyShader.FindKernel("CSMain");
        nbodyShader.SetBuffer(accelKernel, "PositionsMass", positionsMassBuffer);
        nbodyShader.SetBuffer(accelKernel, "Accelerations", accelerationsBuffer);
        nbodyShader.SetInt("BodyCount", bodyCount);
        nbodyShader.SetFloat("DeltaTime", timeStep);
        nbodyShader.SetFloat("G", G);
        nbodyShader.SetFloat("EPS", eps);
        nbodyShader.Dispatch(accelKernel, tileCount, 1, 1);

        //Integration
        int integrateKernel = nbodyShader.FindKernel("Integrate");
        nbodyShader.SetBuffer(integrateKernel, "PositionsMass", positionsMassBuffer);
        nbodyShader.SetBuffer(integrateKernel, "Velocities", velocitiesBuffer);
        nbodyShader.SetBuffer(integrateKernel, "Accelerations", accelerationsBuffer);
        nbodyShader.SetInt("BodyCount", bodyCount);
        nbodyShader.SetFloat("DeltaTime", timeStep);
        nbodyShader.Dispatch(integrateKernel, tileCount, 1, 1);

        //Rendering
        bodyMaterial.SetBuffer("_PositionsMass", positionsMassBuffer);

        Graphics.DrawMeshInstancedIndirect(bodyMesh, 0, bodyMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000f), argsBuffer);
    }
}