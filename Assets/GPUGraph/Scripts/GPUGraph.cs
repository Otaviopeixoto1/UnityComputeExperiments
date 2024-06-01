using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUGraph : MonoBehaviour
{
    const int maxResolution = 640;

    [SerializeField, Range(10, maxResolution)]
	private int resolution = 10;
    [SerializeField, Range(0.1f, 10f)]
	private float scale = 1.0f;
    [SerializeField]
	private FunctionLib.FunctionTypes function;
    [SerializeField]
	private Material material;
	[SerializeField]
	private Mesh mesh;
    [SerializeField]
	private ComputeShader computeShader;
    private ComputeBuffer positionsBuffer;

    [SerializeField]
	private UnityEngine.Rendering.ShadowCastingMode shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    [SerializeField]
	private bool receiveShadows = true;

    //Shader variables:
    private static readonly int 
        positionsId = Shader.PropertyToID("_Positions"),
        resolutionId = Shader.PropertyToID("_Resolution"),
		stepId = Shader.PropertyToID("_Step"),
        scaleId = Shader.PropertyToID("_Scale"),
		timeId = Shader.PropertyToID("_Time");
        
	void Awake () 
    {
		positionsBuffer = new ComputeBuffer(maxResolution * maxResolution, 3 * sizeof(float));
	}

    void Update()
    {
        // Compute shader pass:
        //---------------------
        float step = 2f / resolution;
        int numGroups = Mathf.CeilToInt(resolution / 8f);
        int kernel = (int)function;
		computeShader.SetInt(resolutionId, resolution);
		computeShader.SetFloat(stepId, step);
		computeShader.SetFloat(timeId, Time.time);
        computeShader.SetBuffer(kernel, positionsId, positionsBuffer);
        
        computeShader.Dispatch(kernel, numGroups, numGroups, 1);

        // Render pass:
        //-------------
        RenderParams rp = new RenderParams(material);
        rp.worldBounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
        rp.matProps = new MaterialPropertyBlock();
        // DEFAULT CASTING MODE IS DISABLED
        rp.shadowCastingMode = shadowCastingMode;
        rp.receiveShadows = receiveShadows;
        rp.matProps.SetBuffer(positionsId, positionsBuffer);
        rp.matProps.SetFloat(stepId, step);
        rp.matProps.SetFloat(scaleId, scale);
        
        
        Graphics.RenderMeshPrimitives(rp, mesh, 0, resolution * resolution);
    }

    private void OnDestroy() 
    {
        positionsBuffer?.Release();
    }

}
