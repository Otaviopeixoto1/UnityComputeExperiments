using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// The algorithm's structure was based on: https://on-demand.gputechconf.com/gtc/2014/presentations/S4117-fast-fixed-radius-nearest-neighbor-gpu.pdf
/// </summary>

public class GPUBoidController : MonoBehaviour
{
    [System.Serializable]
    struct BoidData
    {
        public Vector3 velocity;
        public float pad0;
        public Vector3 position;
        public float pad1;

        public static int Size()
        {
            return 8 * sizeof(float);
        }
    }

    [Header("Setup")]
    [SerializeField] private ComputeShader boidGridComp;
    [SerializeField] private ComputeShader prefixSumComp;
    [SerializeField] private ComputeShader boidBehaviorComp;

    private ComputeBuffer gridBuffer;
    private ComputeBuffer boidBuffer0;
    private ComputeBuffer boidBuffer1;
    private ComputeBuffer offsetsBuffer;
    private ComputeBuffer sumsBuffer;
    private ComputeBuffer totalSumBuffer;
    private Vector3Int gridDimensions;

    private int maximumGridIds;

    [Header("Rendering")]
    [SerializeField] private Material boidMaterial;
    private Mesh boidMesh;

    [SerializeField]
	private UnityEngine.Rendering.ShadowCastingMode shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    [SerializeField] private bool receiveShadows = false;
    


    [Header("Simulation Parameters")]
    [Range(1, 1000000)]
    [SerializeField] private int boidsCount = 5000;
    [Range(0.1f,10.0f)]
    [SerializeField] private float scale = 1.0f;
    [SerializeField] private float minSpeed = 0.1f;
    [SerializeField] private float maxSpeed = 2.0f;
    [SerializeField] private float turnSpeed = 1.0f;
    [SerializeField] private float cohesionFactor = 2.0f;
    [SerializeField] private float separationFactor = 1.0f;
    [SerializeField] private float alignmentFactor = 5.0f;

    [SerializeField] private float visualRange = .5f;
    private float visualRangeSq => visualRange * visualRange;
    [SerializeField] private float minDistance = 0.15f;
    private float minDistanceSq => minDistance * minDistance;
    [SerializeField] private Vector3 gridBounds = Vector3.one * 10f;
    private Bounds drawBounds;
    [Range(0.1f,10.0f)]
    [SerializeField] private float gridCellScale = 1.0f;
    private float gridCellSize;

    //Shader variables:
    private static readonly int 
        boidCountID = Shader.PropertyToID("boidCount"),
        scaleId = Shader.PropertyToID("boidScale"),
        cellSizeID = Shader.PropertyToID("gridCellSize"),
        totalCellsID = Shader.PropertyToID("totalGridCells"),
        xboundID = Shader.PropertyToID("xBound"),
        yboundID = Shader.PropertyToID("yBound"),
        zboundID = Shader.PropertyToID("zBound"),
        xDimID = Shader.PropertyToID("gridDimX"),
        yDimID = Shader.PropertyToID("gridDimY"),
        zDimID = Shader.PropertyToID("gridDimZ"),
        
        minSpeedID = Shader.PropertyToID("minSpeed"),
        maxSpeedID = Shader.PropertyToID("maxSpeed"),
        timeID = Shader.PropertyToID("time"),

        deltaTimeID = Shader.PropertyToID("deltaTime"),
        turnSpeedID = Shader.PropertyToID("turnSpeed"),
        cohesionFactorID = Shader.PropertyToID("cohesionFactor"),
        separationFactorID = Shader.PropertyToID("separationFactor"),
        alignmentFactorID = Shader.PropertyToID("alignmentFactor"),
        visualRangeSqID = Shader.PropertyToID("visualRangeSq"),
        minDistanceSqID = Shader.PropertyToID("minDistanceSq"),

        gridBufferID = Shader.PropertyToID("gridBuffer"),
        boidBufferInID = Shader.PropertyToID("boidsIn"),
        boidBufferOutID = Shader.PropertyToID("boidsOut"),
        offsetsID = Shader.PropertyToID("offsetBuffer"),
		sumsInID = Shader.PropertyToID("sumsBufferIn"),
        sumsOutID = Shader.PropertyToID("sumsBufferOut"),

        randSeedID = Shader.PropertyToID("randSeed");
    
    private int boidBlockCount;
    private int gridBlockCount;

    void OnValidate()
    {

    }

    public void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(drawBounds.center, drawBounds.extents * 2);

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, 2.0f*gridBounds);
    }

    private Mesh GenerateBoidMesh()
    {
        Mesh mesh = new Mesh();
        float width = 0.3f;
        float height = 0.6f;

        Vector3[] vertices = {
            new Vector3(-width, -height, 0),
            new Vector3(0, height, 0),
            new Vector3(width, -height, 0),
        };
        mesh.vertices = vertices;

        int[] tris = {0, 1, 2};
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        return mesh;
    }

    void Awake () 
    {
        boidMesh = GenerateBoidMesh();

        gridBuffer = new ComputeBuffer(boidsCount, 2* sizeof(uint));
        boidBuffer0 = new ComputeBuffer(boidsCount, BoidData.Size());
        boidBuffer1 = new ComputeBuffer(boidsCount, BoidData.Size());


        gridCellSize = visualRange * gridCellScale;
        Debug.Log("cell size: " + gridCellSize);
        gridDimensions.x = Mathf.FloorToInt(gridBounds.x * 2 / gridCellSize) + 2;
        gridDimensions.y = Mathf.FloorToInt(gridBounds.y * 2 / gridCellSize) + 2;
        gridDimensions.z = Mathf.FloorToInt(gridBounds.z * 2 / gridCellSize) + 2;

        Debug.Log("cell dimensions: " + gridDimensions);

        ///////////////////////////////////////////////////////////////////////////////////////
        // The highest amount of grid ids must be 1024 * 1024 (surpassing that will cause bugs)
        ///////////////////////////////////////////////////////////////////////////////////////

        //  (total number of cell ids must be an integer multiple of 1024) 
        maximumGridIds = Mathf.CeilToInt(gridDimensions.x * gridDimensions.y * gridDimensions.z / 1024.0f) * 1024;
        //maximumGridIds = 3072;

        Debug.Log("total cells: "+ maximumGridIds);
        offsetsBuffer = new ComputeBuffer(maximumGridIds, sizeof(int)); //size must be a multiple of 1024
        sumsBuffer = new ComputeBuffer(1024, sizeof(int));
        totalSumBuffer = new ComputeBuffer(1, sizeof(int));
	}
    void Start()
    {
        BoidData[] boidData = new BoidData[boidsCount]; 
        
        for (int i = 0; i < boidsCount; i++)
        {
            boidData[i].position = new Vector3(Random.Range(-gridBounds.x, gridBounds.x), Random.Range(-gridBounds.y, gridBounds.y), Random.Range(-gridBounds.z, gridBounds.z)) * 0.9f;
            boidData[i].velocity = Random.Range(minSpeed, maxSpeed) * Random.insideUnitSphere;
        }

        boidBuffer0.SetData(boidData);
    }

    void Update()
    {
        boidBlockCount = Mathf.CeilToInt(boidsCount/512.0f);
        gridBlockCount = maximumGridIds/1024;

        // Clear the Offset array:
        boidGridComp.SetInt(totalCellsID, maximumGridIds);
        boidGridComp.SetBuffer(0, offsetsID, offsetsBuffer); 
        boidGridComp.Dispatch(0, 2 * gridBlockCount, 1, 1); 

        
        // Fill the offset buffer with counts of boids in each cell
        boidGridComp.SetInt(boidCountID, boidsCount); 
        boidGridComp.SetFloat(cellSizeID, gridCellSize);
        boidGridComp.SetInt(xDimID, gridDimensions.x);
        boidGridComp.SetInt(yDimID, gridDimensions.y);
        boidGridComp.SetInt(zDimID, gridDimensions.z);
        boidGridComp.SetBuffer(1, boidBufferInID, boidBuffer0);
        boidGridComp.SetBuffer(1, gridBufferID, gridBuffer);
        boidGridComp.SetBuffer(1, offsetsID, offsetsBuffer); 
        boidGridComp.Dispatch(1, boidBlockCount, 1, 1); 


        // Prefix sum scan on each block of the offset buffer 
        prefixSumComp.SetBuffer(0, offsetsID, offsetsBuffer);
        prefixSumComp.SetBuffer(0, sumsOutID, sumsBuffer);
        prefixSumComp.Dispatch(0, gridBlockCount, 1, 1); 


        // Prefix sum scan on the sums buffer
        prefixSumComp.SetBuffer(0, offsetsID, sumsBuffer);
        prefixSumComp.SetBuffer(0, sumsOutID, totalSumBuffer); //dummy buffer
        prefixSumComp.Dispatch(0, 1, 1, 1); 
        

        // Apply the sums back into the offsets buffer:
        if (gridBlockCount > 1)
        {
            prefixSumComp.SetBuffer(2, offsetsID, offsetsBuffer);
            prefixSumComp.SetBuffer(2, sumsInID, sumsBuffer); 
            prefixSumComp.Dispatch(2, gridBlockCount - 1, 1, 1); 
        }

        
        // Reorder boid buffer
        boidGridComp.SetInt(boidCountID, boidsCount); 
        boidGridComp.SetBuffer(2, gridBufferID, gridBuffer);
        boidGridComp.SetBuffer(2, offsetsID, offsetsBuffer); 
        boidGridComp.SetBuffer(2, boidBufferInID, boidBuffer0);
        boidGridComp.SetBuffer(2, boidBufferOutID, boidBuffer1);
        boidGridComp.Dispatch(2, boidBlockCount, 1, 1); 


        // Update and displace boids
        boidBehaviorComp.SetFloat(cellSizeID, gridCellSize);
        boidBehaviorComp.SetFloat(xboundID, gridBounds.x);
        boidBehaviorComp.SetFloat(yboundID, gridBounds.y);
        boidBehaviorComp.SetFloat(zboundID, gridBounds.z);
        boidBehaviorComp.SetInt(xDimID, gridDimensions.x);
        boidBehaviorComp.SetInt(yDimID, gridDimensions.y);
        boidBehaviorComp.SetInt(zDimID, gridDimensions.z);
        boidBehaviorComp.SetFloat(minSpeedID, minSpeed);
        boidBehaviorComp.SetFloat(maxSpeedID, maxSpeed);
        boidBehaviorComp.SetInt(boidCountID, boidsCount);
        boidBehaviorComp.SetFloat(visualRangeSqID, visualRangeSq);
        boidBehaviorComp.SetFloat(minDistanceSqID, minDistanceSq);
        boidBehaviorComp.SetFloat(cohesionFactorID, cohesionFactor);
        boidBehaviorComp.SetFloat(separationFactorID, separationFactor);
        boidBehaviorComp.SetFloat(alignmentFactorID, alignmentFactor);
        boidBehaviorComp.SetFloat(turnSpeedID, turnSpeed);
        boidBehaviorComp.SetFloat(deltaTimeID, Time.deltaTime);
        boidBehaviorComp.SetBuffer(0, offsetsID, offsetsBuffer); 
        boidBehaviorComp.SetBuffer(0, boidBufferInID, boidBuffer1); 
        boidBehaviorComp.SetBuffer(0, boidBufferOutID, boidBuffer0); 
        boidBehaviorComp.Dispatch(0, boidBlockCount, 1, 1);

        
        RenderParams rp = new RenderParams(boidMaterial);
        drawBounds =  new Bounds(Vector3.zero, 2 * (gridBounds + (maxSpeed * maxSpeed/(2*turnSpeed) + 3.0f) * Vector3.one));
        rp.worldBounds = drawBounds;
        rp.matProps = new MaterialPropertyBlock();
        rp.shadowCastingMode = shadowCastingMode;
        rp.receiveShadows = receiveShadows;
        rp.matProps.SetBuffer(boidBufferInID, boidBuffer0); //change for output buffer
        rp.matProps.SetFloat(scaleId, scale);
        rp.matProps.SetFloat(timeID, Time.time);

        Graphics.RenderMeshPrimitives(rp, boidMesh, 0, boidsCount);
    }

    private void OnDestroy() 
    {
        gridBuffer?.Release();
        boidBuffer0?.Release();
        boidBuffer1?.Release();
        offsetsBuffer?.Release();
        sumsBuffer?.Release();
        totalSumBuffer?.Release();
    }
}
