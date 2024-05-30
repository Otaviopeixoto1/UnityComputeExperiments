using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    const int chunkSize = 33; //(including "phantom cells" necessary for the unique vert generation scheme)
    const int densityDim = chunkSize + 1;
    [SerializeField] private bool densityGizmos = false;
    [SerializeField] private ComputeShader densityGenShader;
    [SerializeField] private ComputeShader marchingCubesShader;
    [SerializeField] private ComputeShader prefixSumScanShader;
    [SerializeField] private Material terrainMaterial;
    private ComputeBuffer densityBuffer;
    private ComputeBuffer cellDataBuffer;

    private ComputeBuffer offsetBuffer;
    private ComputeBuffer sumsBuffer;
    private GraphicsBuffer indirectDrawBuffer;

    private ComputeBuffer vertexBuffer;
    private GraphicsBuffer indexBuffer;

    //Constant Buffers:
    private ComputeBuffer MCEdgeLUTsBuffer;
    private ComputeBuffer triangleTable;
    private ComputeBuffer numTrianglesTable;

    private int cellCount;
    private int densityCellCount;
    private int maximumCellIds;

    private Mesh triangleMesh;

    private static readonly int 
        chunkSizeID = Shader.PropertyToID("chunkSize"),
        totalCellsID = Shader.PropertyToID("totalCells"),

        densityVolInID = Shader.PropertyToID("densityVolumeIn"),
        densityVolOutID = Shader.PropertyToID("densityVolumeOut"),
        cellDataID = Shader.PropertyToID("cellData"),
        offsetsID = Shader.PropertyToID("offsetBuffer"),

        edgeLUTsID = Shader.PropertyToID("MCEdgeLUTs"),
        numTriTableID = Shader.PropertyToID("caseToTriangleNum"),
        triTableID = Shader.PropertyToID("triangleTable"),

        vertBufferID = Shader.PropertyToID("vertexBuffer"),
        indBufferID = Shader.PropertyToID("indexBuffer"),

        indArgsID = Shader.PropertyToID("indirectArgsBuffer"),
        sumsInID = Shader.PropertyToID("sumsBufferIn"),
        sumsOutID = Shader.PropertyToID("sumsBufferOut");

    

    struct Vert 
    {
        public float4 position;
        public float4 normal;

        public static int Size()
        {
            return 8 * sizeof(float);
        }
    };
    
    private Mesh GenerateTriangleMesh()
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices = {
            new Vector3(0, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 1),
        };
        mesh.vertices = vertices;

        int[] tris = {0, 1, 2};
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        return mesh;
    }


    float[] densityData;
    private void OnDrawGizmos() 
    {
        if (densityData == null || densityData.Length == 0 || !densityGizmos) {
            return;
        }

        for (int x = 0; x < densityDim; x+=4) 
        {
            for (int y = 0; y < densityDim; y+=4) 
            {
                for (int z = 0; z < densityDim; z+=4) 
                {
                    int index = ((densityDim) * (densityDim) * z) + ((densityDim) * y) + x;
                    float noiseValue = densityData[index];
                    Gizmos.color = Color.Lerp(Color.black, Color.white, noiseValue);
                    Gizmos.DrawCube(new Vector3(x, y, z), Vector3.one * .4f);
                }
            }
        }
    }

    
    
    void Start()
    {
        triangleMesh = GenerateTriangleMesh();

        cellCount = chunkSize * chunkSize * chunkSize; // MAX = 1024 * 1024
        densityCellCount = densityDim * densityDim * densityDim;
        maximumCellIds = Mathf.CeilToInt(cellCount / 1024.0f) * 1024;

        densityBuffer = new ComputeBuffer(densityCellCount, sizeof(float)); 
        cellDataBuffer = new ComputeBuffer(cellCount, sizeof(uint));

        offsetBuffer = new ComputeBuffer(maximumCellIds, 2 * sizeof(uint));
        sumsBuffer = new ComputeBuffer(1024, 2 * sizeof(uint));

        indirectDrawBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        GraphicsBuffer.IndirectDrawIndexedArgs[] indirectDrawData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        /* For RenderPrimitivesIndexedIndirect:
        indirectDrawData[0].indexCountPerInstance = 0;
        indirectDrawData[0].baseVertexIndex = 0;
        indirectDrawData[0].startIndex = 0;
        indirectDrawData[0].instanceCount = 1;
        indirectDrawData[0].startInstance = 0;
        */
        indirectDrawData[0].indexCountPerInstance = triangleMesh.GetIndexCount(0);
        indirectDrawData[0].baseVertexIndex = 0;
        indirectDrawData[0].startIndex = 0;
        indirectDrawData[0].instanceCount = 0;
        indirectDrawData[0].startInstance = 0;
        indirectDrawBuffer.SetData(indirectDrawData);

        vertexBuffer = new ComputeBuffer(3 * cellCount, Vert.Size());
        indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, 3 * 5 * cellCount, sizeof(uint));
        

        MCEdgeLUTsBuffer = new ComputeBuffer(1, MCTables.EdgeLUTs.Size(), ComputeBufferType.Constant);
        MCEdgeLUTsBuffer.SetData(MCTables.EdgeLUTs.GetEdgeLUTs());

        numTrianglesTable = new ComputeBuffer(256, sizeof(uint));
        numTrianglesTable.SetData(MCTables.caseToTriangleNum);

        triangleTable = new ComputeBuffer(1280, 4*sizeof(int));
        triangleTable.SetData(MCTables.triangleTable);


        densityData = new float[densityCellCount];
        GenerateDensity();

        MCUpdate();
        
    
    }


    void Update()
    {
        //terrainMaterial.SetBuffer("vertexBuffer", vertexBuffer);
        //terrainMaterial.SetBuffer("indexBuffer", indexBuffer);
        RenderParams rp = new RenderParams(terrainMaterial);
        rp.worldBounds = new Bounds(Vector3.zero, 6400*Vector3.one); // use tighter bounds
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("vertexBuffer", vertexBuffer);
        rp.matProps.SetBuffer("indexBuffer", indexBuffer);

        //DOESNT WORK:
        //Graphics.RenderPrimitivesIndexedIndirect(rp, MeshTopology.Triangles, indexBuffer, indirectDrawBuffer, 1);

        //HACK:
        Graphics.RenderMeshIndirect(rp, triangleMesh, indirectDrawBuffer);

    }

    private void OnDestroy() 
    {
        densityBuffer?.Release();
        cellDataBuffer?.Release();

        offsetBuffer?.Release();
        sumsBuffer?.Release();
        indirectDrawBuffer?.Release();

        vertexBuffer?.Release();
        indexBuffer?.Release();

        MCEdgeLUTsBuffer?.Release();
        triangleTable?.Release();
        numTrianglesTable?.Release();
    }
    
    private void GenerateDensity()
    {
        int dispatchDim = Mathf.CeilToInt(densityDim/8.0f);
        //Debug.Log(dispatchDim);
        densityGenShader.SetInt(totalCellsID, densityCellCount);
        densityGenShader.SetInt(chunkSizeID, chunkSize);
        densityGenShader.SetBuffer(0, densityVolOutID, densityBuffer); 
        densityGenShader.Dispatch(0, dispatchDim, dispatchDim, dispatchDim); 

        densityBuffer.GetData(densityData);
    }
    
    
    private void MCUpdate()
    {
        // 1) Clear the Offset array:
        int dispatchCount = Mathf.CeilToInt(maximumCellIds/512.0f); //ceil not necessary here
        marchingCubesShader.SetInt(totalCellsID, maximumCellIds);
        marchingCubesShader.SetBuffer(0, offsetsID, offsetBuffer); 
        marchingCubesShader.Dispatch(0, dispatchCount, 1, 1); 

        // 2) Mark Cells:
        int dispatchDim = Mathf.CeilToInt(densityDim/8.0f);
        marchingCubesShader.SetInt(chunkSizeID, chunkSize);
        marchingCubesShader.SetBuffer(1, densityVolInID, densityBuffer); 
        marchingCubesShader.SetBuffer(1, cellDataID, cellDataBuffer);
        marchingCubesShader.SetBuffer(1, offsetsID, offsetBuffer);
        marchingCubesShader.SetBuffer(1, numTriTableID, numTrianglesTable);
        marchingCubesShader.Dispatch(1, dispatchDim, dispatchDim, dispatchDim);

        /*
        uint[] offsets = new uint[2*maximumCellIds];
        offsetBuffer.GetData(offsets);
        for (int i = 0; i < maximumCellIds; i++)
        {
            Debug.Log("("+ offsets[2*i] +","+ offsets[2*i+1]  +")");
        }*/


        // 3) Scan:

        //clear sum buffer:
        prefixSumScanShader.SetBuffer(0, sumsOutID, sumsBuffer);
        prefixSumScanShader.Dispatch(0, 1024/512, 1, 1);

        //initial block sum:
        int offsetBlockCount = maximumCellIds/1024;
        prefixSumScanShader.SetBuffer(1, offsetsID, offsetBuffer);
        prefixSumScanShader.SetBuffer(1, sumsOutID, sumsBuffer);
        prefixSumScanShader.Dispatch(1, offsetBlockCount, 1, 1);

        //sum the blocks:
        // Prefix sum scan on the sums buffer
        prefixSumScanShader.SetBuffer(2, offsetsID, sumsBuffer);
        prefixSumScanShader.SetBuffer(2, indArgsID, indirectDrawBuffer); //final sums buffer
        prefixSumScanShader.Dispatch(2, 1, 1, 1); 
        
        

        // Apply the sums back into the offsets buffer:
        if (offsetBlockCount > 1)
        {
            prefixSumScanShader.SetBuffer(3, offsetsID, offsetBuffer);
            prefixSumScanShader.SetBuffer(3, sumsInID, sumsBuffer); 
            prefixSumScanShader.Dispatch(3, offsetBlockCount - 1, 1, 1); 
        }

        /*
        uint[] offsets = new uint[2*maximumCellIds];
        offsetBuffer.GetData(offsets);
        for (int i = 0; i < maximumCellIds; i++)
        {
            Debug.Log("("+ offsets[2*i] +","+ offsets[2*i+1]  +")");
        }*/

        GraphicsBuffer.IndirectDrawIndexedArgs[] indirectDrawData2 = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        indirectDrawBuffer.GetData(indirectDrawData2);
        Debug.Log("index count: " +  indirectDrawData2[0].indexCountPerInstance);
        Debug.Log("instance count: " +  indirectDrawData2[0].instanceCount);
        Debug.Log("startIndex: " +  indirectDrawData2[0].startIndex);
        Debug.Log("startInstance: " +  indirectDrawData2[0].startInstance);
        Debug.Log("baseVertexIndex: " +  indirectDrawData2[0].baseVertexIndex);


        // 4) Generate verts and index buffer:
        marchingCubesShader.SetConstantBuffer(edgeLUTsID, MCEdgeLUTsBuffer, 0, MCTables.EdgeLUTs.Size());
        marchingCubesShader.SetBuffer(2, densityVolInID, densityBuffer);
        marchingCubesShader.SetBuffer(2, cellDataID, cellDataBuffer);
        marchingCubesShader.SetBuffer(2, offsetsID, offsetBuffer);
        marchingCubesShader.SetBuffer(2, triTableID, triangleTable);
        marchingCubesShader.SetBuffer(2, vertBufferID, vertexBuffer);
        marchingCubesShader.SetBuffer(2, indBufferID, indexBuffer);
        marchingCubesShader.Dispatch(2, dispatchDim, dispatchDim, dispatchDim);

        
        /*Vert[] verts = new Vert[3 * cellCount];
        vertexBuffer.GetData(verts);
        for (int i = 0; i < maximumCellIds; i++)
        {
            Debug.Log(verts[i].position);
        }
        
        uint[] ids = new uint[3 * 5 * cellCount];
        indexBuffer.GetData(ids);
        for (int i = 0; i < 3 * 2 * maximumCellIds; i+=3)
        {
            Debug.Log("(" + ids[i] + ", " + ids[i+1] + ", " + ids[i+2] + ")");
        }*/

    }
}
