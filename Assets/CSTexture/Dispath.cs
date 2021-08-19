using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dispath : MonoBehaviour
{
    public ComputeShader computeShader;

    public Material mat;

    public Camera cam;
    // Start is called before the first frame update
    void Start()
    {
        var view = cam.worldToCameraMatrix;
        var proj = cam.projectionMatrix;
        var dxproj = GL.GetGPUProjectionMatrix(proj, false);
        
        RenderTexture tex = new RenderTexture(256,256,1);
        tex.enableRandomWrite = true;
        tex.Create();
        mat.mainTexture = tex;
        int kernelIndex = computeShader.FindKernel("CSMain");
        computeShader.SetTexture(kernelIndex,"Result",tex);
        computeShader.Dispatch(kernelIndex, 256 / 8, 256 / 8, 1);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
