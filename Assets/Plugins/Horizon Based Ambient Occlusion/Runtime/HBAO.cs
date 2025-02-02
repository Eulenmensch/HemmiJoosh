﻿using UnityEngine;

[ExecuteInEditMode, AddComponentMenu("Image Effects/HBAO")]
[RequireComponent(typeof(Camera))]
public class HBAO : HBAO_Core
{
    private RenderTexture[] _mrtTexDepth = new RenderTexture[4 * NUM_MRTS];
    private RenderTexture[] _mrtTexNrm = new RenderTexture[4 * NUM_MRTS];
    private RenderTexture[] _mrtTexAO = new RenderTexture[4 * NUM_MRTS];
    private RenderBuffer[][] _mrtRB = new RenderBuffer[NUM_MRTS][] { new RenderBuffer[4], new RenderBuffer[4], new RenderBuffer[4], new RenderBuffer[4] };
    private RenderBuffer[][] _mrtRBNrm = new RenderBuffer[NUM_MRTS][] { new RenderBuffer[4], new RenderBuffer[4], new RenderBuffer[4], new RenderBuffer[4] };

    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (hbaoShader == null || _hbaoCamera == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        _hbaoCamera.depthTextureMode |= DepthTextureMode.Depth;
        if (aoSettings.perPixelNormals == PerPixelNormals.Camera)
            _hbaoCamera.depthTextureMode |= DepthTextureMode.DepthNormals;

        CheckParameters();
        UpdateShaderProperties();
        UpdateShaderKeywords();

        if (generalSettings.deinterleaving == Deinterleaving._2x)
            RenderHBAODeinterleaved2x(source, destination);
        else if (generalSettings.deinterleaving == Deinterleaving._4x)
            RenderHBAODeinterleaved4x(source, destination);
        else
            RenderHBAO(source, destination);
    }

    private void RenderHBAO(RenderTexture source, RenderTexture destination)
    {
        RenderTexture rt = RenderTexture.GetTemporary(_renderTarget.fullWidth / _renderTarget.downsamplingFactor, _renderTarget.fullHeight / _renderTarget.downsamplingFactor);

        // AO rt clear
        RenderTexture lastActive = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(false, true, Color.white);
        RenderTexture.active = lastActive;

        Graphics.Blit(source, rt, _hbaoMaterial, GetAoPass()); // hbao
        if (blurSettings.type != Blur.None)
        {
            RenderTexture rtBlur = RenderTexture.GetTemporary((_renderTarget.fullWidth / _renderTarget.downsamplingFactor), (_renderTarget.fullHeight / _renderTarget.downsamplingFactor));
            Graphics.Blit(rt, rtBlur, _hbaoMaterial, GetBlurXPass()); // blur X
            rt.DiscardContents();
            Graphics.Blit(rtBlur, rt, _hbaoMaterial, GetBlurYPass()); // blur Y
            RenderTexture.ReleaseTemporary(rtBlur);
        }
        _hbaoMaterial.SetTexture(ShaderProperties.hbaoTex, rt);
        Graphics.Blit(source, destination, _hbaoMaterial, GetFinalPass()); // final pass
        RenderTexture.ReleaseTemporary(rt);
    }

    private void RenderHBAODeinterleaved2x(RenderTexture source, RenderTexture destination)
    {
        RenderTexture lastActive = RenderTexture.active;

        // initialize render textures & buffers
        for (int i = 0; i < NUM_MRTS; i++)
        {
            _mrtTexDepth[i] = RenderTexture.GetTemporary(_renderTarget.layerWidth, _renderTarget.layerHeight, 0, RenderTextureFormat.RFloat);
            _mrtTexNrm[i] = RenderTexture.GetTemporary(_renderTarget.layerWidth, _renderTarget.layerHeight, 0, RenderTextureFormat.ARGB2101010);
            _mrtTexAO[i] = RenderTexture.GetTemporary(_renderTarget.layerWidth, _renderTarget.layerHeight);
            _mrtRB[0][i] = _mrtTexDepth[i].colorBuffer;
            _mrtRBNrm[0][i] = _mrtTexNrm[i].colorBuffer;
            // AO rt clear
            RenderTexture.active = _mrtTexAO[i];
            GL.Clear(false, true, Color.white);
        }

        // deinterleave depth & normals 2x2
        _hbaoMaterial.SetVector(ShaderProperties.deinterleavingOffset[0], new Vector2(0, 0));
        _hbaoMaterial.SetVector(ShaderProperties.deinterleavingOffset[1], new Vector2(1, 0));
        _hbaoMaterial.SetVector(ShaderProperties.deinterleavingOffset[2], new Vector2(0, 1));
        _hbaoMaterial.SetVector(ShaderProperties.deinterleavingOffset[3], new Vector2(1, 1));
        Graphics.SetRenderTarget(_mrtRB[0], _mrtTexDepth[0].depthBuffer);
        _hbaoMaterial.SetPass(Pass.Depth_Deinterleaving_2x2);
        Graphics.DrawMeshNow(quadMesh, Matrix4x4.identity); // outputs 4 render textures
        Graphics.SetRenderTarget(_mrtRBNrm[0], _mrtTexNrm[0].depthBuffer);
        _hbaoMaterial.SetPass(Pass.Normals_Deinterleaving_2x2);
        Graphics.DrawMeshNow(quadMesh, Matrix4x4.identity); // outputs 4 render textures

        RenderTexture.active = lastActive;

        // calculate AO on each layer
        for (int i = 0; i < NUM_MRTS; i++)
        {
            _hbaoMaterial.SetTexture(ShaderProperties.depthTex, _mrtTexDepth[i]);
            _hbaoMaterial.SetTexture(ShaderProperties.normalsTex, _mrtTexNrm[i]);
            _hbaoMaterial.SetVector(ShaderProperties.jitter, _jitter[i]);
            Graphics.Blit(source, _mrtTexAO[i], _hbaoMaterial, GetAoDeinterleavedPass());
            _mrtTexDepth[i].DiscardContents();
            _mrtTexNrm[i].DiscardContents();
        }

        // build atlas
        RenderTexture rt1 = RenderTexture.GetTemporary(_renderTarget.fullWidth, _renderTarget.fullHeight);
        for (int i = 0; i < NUM_MRTS; i++)
        {
            _hbaoMaterial.SetVector(ShaderProperties.layerOffset, new Vector2((i & 1) * _renderTarget.layerWidth, (i >> 1) * _renderTarget.layerHeight));
            Graphics.Blit(_mrtTexAO[i], rt1, _hbaoMaterial, Pass.Atlas);
            RenderTexture.ReleaseTemporary(_mrtTexAO[i]);
            RenderTexture.ReleaseTemporary(_mrtTexNrm[i]);
            RenderTexture.ReleaseTemporary(_mrtTexDepth[i]);
        }

        // reinterleave
        RenderTexture rt2 = RenderTexture.GetTemporary(_renderTarget.fullWidth, _renderTarget.fullHeight);
        Graphics.Blit(rt1, rt2, _hbaoMaterial, Pass.Reinterleaving_2x2);
        rt1.DiscardContents();

        if (blurSettings.type != Blur.None)
        {
            Graphics.Blit(rt2, rt1, _hbaoMaterial, GetBlurXPass()); // blur X
            rt2.DiscardContents();
            Graphics.Blit(rt1, rt2, _hbaoMaterial, GetBlurYPass()); // blur Y
        }

        RenderTexture.ReleaseTemporary(rt1);

        _hbaoMaterial.SetTexture(ShaderProperties.hbaoTex, rt2);
        Graphics.Blit(source, destination, _hbaoMaterial, GetFinalPass()); // final pass

        RenderTexture.ReleaseTemporary(rt2);
    }

    private void RenderHBAODeinterleaved4x(RenderTexture source, RenderTexture destination)
    {
        RenderTexture lastActive = RenderTexture.active;

        // initialize render textures & buffers
        for (int i = 0; i < 4 * NUM_MRTS; i++)
        {
            _mrtTexDepth[i] = RenderTexture.GetTemporary(_renderTarget.layerWidth, _renderTarget.layerHeight, 0, RenderTextureFormat.RFloat);
            _mrtTexNrm[i] = RenderTexture.GetTemporary(_renderTarget.layerWidth, _renderTarget.layerHeight, 0, RenderTextureFormat.ARGB2101010);
            _mrtTexAO[i] = RenderTexture.GetTemporary(_renderTarget.layerWidth, _renderTarget.layerHeight);
            // AO rt clear
            RenderTexture.active = _mrtTexAO[i];
            GL.Clear(false, true, Color.white);
        }
        for (int i = 0; i < NUM_MRTS; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                _mrtRB[i][j] = _mrtTexDepth[j + NUM_MRTS * i].colorBuffer;
                _mrtRBNrm[i][j] = _mrtTexNrm[j + NUM_MRTS * i].colorBuffer;
            }
        }

        // deinterleave depth & normals 4x4
        for (int i = 0; i < NUM_MRTS; i++)
        {
            int offsetX = (i & 1) << 1;
            int offsetY = (i >> 1) << 1;
            _hbaoMaterial.SetVector(ShaderProperties.deinterleavingOffset[0], new Vector2(offsetX + 0, offsetY + 0));
            _hbaoMaterial.SetVector(ShaderProperties.deinterleavingOffset[1], new Vector2(offsetX + 1, offsetY + 0));
            _hbaoMaterial.SetVector(ShaderProperties.deinterleavingOffset[2], new Vector2(offsetX + 0, offsetY + 1));
            _hbaoMaterial.SetVector(ShaderProperties.deinterleavingOffset[3], new Vector2(offsetX + 1, offsetY + 1));
            Graphics.SetRenderTarget(_mrtRB[i], _mrtTexDepth[NUM_MRTS * i].depthBuffer);
            _hbaoMaterial.SetPass(Pass.Depth_Deinterleaving_4x4);
            Graphics.DrawMeshNow(quadMesh, Matrix4x4.identity); // outputs 4 render textures
            Graphics.SetRenderTarget(_mrtRBNrm[i], _mrtTexNrm[NUM_MRTS * i].depthBuffer);
            _hbaoMaterial.SetPass(Pass.Normals_Deinterleaving_4x4);
            Graphics.DrawMeshNow(quadMesh, Matrix4x4.identity); // outputs 4 render textures
        }

        RenderTexture.active = lastActive;

        // calculate AO on each layer
        for (int i = 0; i < 4 * NUM_MRTS; i++)
        {
            _hbaoMaterial.SetTexture(ShaderProperties.depthTex, _mrtTexDepth[i]);
            _hbaoMaterial.SetTexture(ShaderProperties.normalsTex, _mrtTexNrm[i]);
            _hbaoMaterial.SetVector(ShaderProperties.jitter, _jitter[i]);
            Graphics.Blit(source, _mrtTexAO[i], _hbaoMaterial, GetAoDeinterleavedPass());
            _mrtTexDepth[i].DiscardContents();
            _mrtTexNrm[i].DiscardContents();
        }

        // build atlas
        RenderTexture rt1 = RenderTexture.GetTemporary(_renderTarget.fullWidth, _renderTarget.fullHeight);
        for (int i = 0; i < 4 * NUM_MRTS; i++)
        {
            _hbaoMaterial.SetVector(ShaderProperties.layerOffset, new Vector2(((i & 1) + (((i & 7) >> 2) << 1)) * _renderTarget.layerWidth, (((i & 3) >> 1) + ((i >> 3) << 1)) * _renderTarget.layerHeight));
            Graphics.Blit(_mrtTexAO[i], rt1, _hbaoMaterial, Pass.Atlas);
            RenderTexture.ReleaseTemporary(_mrtTexAO[i]);
            RenderTexture.ReleaseTemporary(_mrtTexNrm[i]);
            RenderTexture.ReleaseTemporary(_mrtTexDepth[i]);
        }

        // reinterleave
        RenderTexture rt2 = RenderTexture.GetTemporary(_renderTarget.fullWidth, _renderTarget.fullHeight);
        Graphics.Blit(rt1, rt2, _hbaoMaterial, Pass.Reinterleaving_4x4);
        rt1.DiscardContents();

        if (blurSettings.type != Blur.None)
        {
            Graphics.Blit(rt2, rt1, _hbaoMaterial, GetBlurXPass()); // blur X
            rt2.DiscardContents();
            Graphics.Blit(rt1, rt2, _hbaoMaterial, GetBlurYPass()); // blur Y
        }

        RenderTexture.ReleaseTemporary(rt1);

        _hbaoMaterial.SetTexture(ShaderProperties.hbaoTex, rt2);
        Graphics.Blit(source, destination, _hbaoMaterial, GetFinalPass()); // final pass

        RenderTexture.ReleaseTemporary(rt2);
    }
}
