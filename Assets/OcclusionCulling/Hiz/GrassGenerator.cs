using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GrassGenerator : MonoBehaviour
{
    public Mesh grassMesh;
    public int subMeshIndex = 0;
    public Material grassMaterial;
    public int GrassCountPerRaw = 300;//每行草的数量
    public DepthTextureGenerator depthTextureGenerator;
    public ComputeShader compute;//剔除的ComputeShader
    public bool single_grass_debug=false;
    public bool scene_debug=false;
    public bool enable_shadow_screen = false;

    int m_grassCount;
    int kernel;
    Camera mainCamera;

    ComputeBuffer argsBuffer;
    ComputeBuffer grassMatrixBuffer;//所有草的世界坐标矩阵
    ComputeBuffer cullResultBuffer;//剔除后的结果

    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    int cullResultBufferId, vpMatrixId, positionBufferId, hizTextureId;
    private CommandBuffer cmdbuf;
    private CommandBuffer drawGrassBuf;
    private CommandBuffer debugGrassBuf;
    public bool applyCull = false;

    void Start()
    {
        m_grassCount = GrassCountPerRaw * GrassCountPerRaw;
        mainCamera = Camera.main;

        if(grassMesh != null) {
            args[0] = grassMesh.GetIndexCount(subMeshIndex);
            args[2] = grassMesh.GetIndexStart(subMeshIndex);
            args[3] = grassMesh.GetBaseVertex(subMeshIndex);
        }
        else
            args[0] = args[1] = args[2] = args[3] = 0;

        InitComputeBuffer();
        InitGrassPosition();
        InitComputeShader();
        cmdbuf = new CommandBuffer {name = "Cull"};
        drawGrassBuf = new CommandBuffer {name = "Draw grass"};
        debugGrassBuf = new CommandBuffer {name = "debug grass"};
        mainCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque,cmdbuf);
        mainCamera.AddCommandBuffer(CameraEvent.BeforeSkybox,drawGrassBuf);
        mainCamera.AddCommandBuffer(CameraEvent.BeforeSkybox,debugGrassBuf);
    }

    void InitComputeShader() {
        kernel = compute.FindKernel("GrassCulling");
        compute.SetInt("grassCount", m_grassCount);
        compute.SetInt("depthTextureSize", depthTextureGenerator.depthTextureSize);
        compute.SetBool("isOpenGL", Camera.main.projectionMatrix.Equals(GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false)));
        compute.SetBuffer(kernel, "grassMatrixBuffer", grassMatrixBuffer);
        
        cullResultBufferId = Shader.PropertyToID("cullResultBuffer");
        vpMatrixId = Shader.PropertyToID("vpMatrix");
        hizTextureId = Shader.PropertyToID("hizTexture");
        positionBufferId = Shader.PropertyToID("positionBuffer");
    }

    void InitComputeBuffer() {
        if(grassMatrixBuffer != null) return;
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
        grassMatrixBuffer = new ComputeBuffer(m_grassCount, sizeof(float) * 16);
        cullResultBuffer = new ComputeBuffer(m_grassCount, sizeof(float) * 16, ComputeBufferType.Append);
    }

    void Update(){
        cmdbuf.Clear();
        compute.SetBool("_ApplyCull",applyCull);
        cmdbuf.SetComputeTextureParam(compute,kernel,hizTextureId,depthTextureGenerator.depthTexture);
        cmdbuf.SetComputeMatrixParam(compute,vpMatrixId,GL.GetGPUProjectionMatrix(mainCamera.projectionMatrix, false) * mainCamera.worldToCameraMatrix);
        cmdbuf.SetComputeBufferCounterValue(cullResultBuffer,0);
        cmdbuf.SetComputeBufferParam(compute,kernel,cullResultBufferId,cullResultBuffer);
        cmdbuf.DispatchCompute(compute,kernel,1 + m_grassCount / 640,1,1);
        grassMaterial.SetBuffer(positionBufferId,cullResultBuffer);
        //获取实际要渲染的数量
        cmdbuf.CopyCounterValue(cullResultBuffer, argsBuffer, sizeof(uint));
        
        drawGrassBuf.Clear();
        drawGrassBuf.EnableShaderKeyword("LIGHTPROBE_SH");
        if(enable_shadow_screen)
            drawGrassBuf.EnableShaderKeyword("SHADOWS_SCREEN");
        else
            drawGrassBuf.DisableShaderKeyword("SHADOWS_SCREEN");

        drawGrassBuf.DrawMeshInstancedIndirect(grassMesh, subMeshIndex, grassMaterial, 0, argsBuffer,0);
        //frame buffer cannot see the information of the gpu-driven drawcall
        debugGrassBuf.Clear();
        // 感觉不启动SHADOWS_SCREEN的效果是最正常的
        if (single_grass_debug)
        {
            drawGrassBuf.Clear();
            debugGrassBuf.EnableShaderKeyword("LIGHTPROBE_SH");
            if(enable_shadow_screen)
                debugGrassBuf.EnableShaderKeyword("SHADOWS_SCREEN");
            else
                debugGrassBuf.DisableShaderKeyword("SHADOWS_SCREEN");
            debugGrassBuf.DrawMesh(grassMesh, Matrix4x4.TRS(new Vector3(-20,0,5), Quaternion.identity,  new Vector3(5,5,5)), grassMaterial, 0, 0);
        }

        if (scene_debug)
        {
            //this is for debug to draw grass in scene window
            //use Graphics.DrawInstanceIndirect will also draw meshes on the Scene panel
            drawGrassBuf.Clear();
            debugGrassBuf.Clear();
            if(enable_shadow_screen)
                grassMaterial.EnableKeyword("SHADOWS_SCREEN");
            else
                grassMaterial.DisableKeyword("SHADOWS_SCREEN");
            Graphics.DrawMeshInstancedIndirect(grassMesh, subMeshIndex, grassMaterial, new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), argsBuffer);
        }
        
    }

    //获取每个草的世界坐标矩阵
    void InitGrassPosition() {
        const int padding = 2;
        int width = (100 - padding * 2);
        int widthStart = -width / 2;
        float step = (float)width / GrassCountPerRaw;
        Matrix4x4[] grassMatrixs = new Matrix4x4[m_grassCount];
        for(int i = 0; i < GrassCountPerRaw; i++) {
            for(int j = 0; j < GrassCountPerRaw; j++) {
                Vector2 xz = new Vector2(widthStart + step * i, widthStart + step * j);
                Vector3 position = new Vector3(xz.x, GetGroundHeight(xz), xz.y);
                grassMatrixs[i * GrassCountPerRaw + j] = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
            }
        }
        grassMatrixBuffer.SetData(grassMatrixs);
    }

    //通过Raycast计算草的高度
    float GetGroundHeight(Vector2 xz) {
        RaycastHit hit;
        if(Physics.Raycast(new Vector3(xz.x, 10, xz.y), Vector3.down, out hit, 20)) {
            return 10 - hit.distance;
        }
        return 0;
    }

    void OnDisable() {
        grassMatrixBuffer?.Release();
        grassMatrixBuffer = null;

        cullResultBuffer?.Release();
        cullResultBuffer = null;

        argsBuffer?.Release();
        argsBuffer = null;
        mainCamera.RemoveCommandBuffer(CameraEvent.BeforeSkybox,cmdbuf);
    }

    private void OnDestroy()
    {
        cmdbuf?.Dispose();
        cmdbuf = null;
    }
}
