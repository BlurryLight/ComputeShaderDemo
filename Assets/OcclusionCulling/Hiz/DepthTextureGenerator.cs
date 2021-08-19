using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class DepthTextureGenerator : MonoBehaviour {
    public Shader depthTextureShader;//用来生成mipmap的shader

    RenderTexture m_depthTexture;//带 mipmap 的深度图
    public RenderTexture depthTexture => m_depthTexture;

    int m_depthTextureSize = 0;
    public int depthTextureSize {
        get {
            if (m_depthTextureSize == 0)
            {
                m_depthTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(Screen.width, Screen.height));
            }
            return m_depthTextureSize;
        }
    }

    Material m_depthTextureMaterial;
    const RenderTextureFormat m_depthTextureFormat = RenderTextureFormat.RHalf;//深度取值范围0-1，单通道即可。

    int m_textureSizeShaderID;
    int m_depthTextureShaderID;
    private CommandBuffer cmdbuf;

    private void Awake()
    {
        #if UNITY_EDITOR
                    QualitySettings.vSyncCount = 0;  // VSync must be disabled
                    Application.targetFrameRate = 15;
        #endif
    }

    void Start() {
        m_depthTextureMaterial = new Material(depthTextureShader);
        Camera.main.depthTextureMode |= DepthTextureMode.Depth;

        m_textureSizeShaderID = Shader.PropertyToID("_MainTexSize");
        m_depthTextureShaderID = Shader.PropertyToID("_CameraDepthTexture");

        InitDepthTexture();
        Camera.main.AddCommandBuffer(CameraEvent.AfterDepthTexture,cmdbuf);
    }

    void InitDepthTexture() {
        if(m_depthTexture != null) return;
        m_depthTexture = new RenderTexture(depthTextureSize, depthTextureSize, 0, m_depthTextureFormat);
        m_depthTexture.autoGenerateMips = false;
        m_depthTexture.useMipMap = true;
        m_depthTexture.filterMode = FilterMode.Point;
        m_depthTexture.Create();
        cmdbuf = new CommandBuffer() {name = "mipmap depth"};
        

    }

    //生成mipmap
    void OnPreRender() {
        cmdbuf.Clear(); 
        int w = m_depthTexture.width;
        int mipmapLevel = 0;
        RenderTexture currentRenderTexture = null;//当前mipmapLevel对应的mipmap
        RenderTexture preRenderTexture = null;//上一层的mipmap，即mipmapLevel-1对应的mipmap

        //如果当前的mipmap的宽高大于8，则计算下一层的mipmap
        while(w > 8) {
            currentRenderTexture = RenderTexture.GetTemporary(w, w, 0, m_depthTextureFormat);
            currentRenderTexture.filterMode = FilterMode.Point;
            if(preRenderTexture == null) {
                //Mipmap[0]即copy原始的深度图
                cmdbuf.Blit(null, currentRenderTexture,m_depthTextureMaterial,1);
            }
            else {
                //将Mipmap[i] Blit到Mipmap[i+1]上
                m_depthTextureMaterial.SetInt(m_textureSizeShaderID, w);
                cmdbuf.Blit(preRenderTexture, currentRenderTexture, m_depthTextureMaterial,0);
                RenderTexture.ReleaseTemporary(preRenderTexture);
            }
            cmdbuf.CopyTexture(currentRenderTexture, 0, 0, m_depthTexture, 0, mipmapLevel);
            preRenderTexture = currentRenderTexture;

            w /= 2;
            mipmapLevel++;
        }
        RenderTexture.ReleaseTemporary(preRenderTexture);
    }

    private void OnDisable()
    {
        Camera.main.RemoveCommandBuffer(CameraEvent.AfterDepthTexture,cmdbuf);
    }

    void OnDestroy() {
        m_depthTexture?.Release();
        Destroy(m_depthTexture);
        cmdbuf?.Dispose();
        cmdbuf = null;
    }
}
