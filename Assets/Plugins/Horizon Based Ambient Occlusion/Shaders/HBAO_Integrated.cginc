#ifndef HBAO_INTEGRATED_INCLUDED
#define HBAO_INTEGRATED_INCLUDED

    half4 frag(v2f i) : SV_Target {
#if UNITY_SINGLE_PASS_STEREO
		half4 gbuffer3 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_rt3Tex, UnityStereoTransformScreenSpaceTex(i.uv2));
#else
		half4 gbuffer3 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_rt3Tex, i.uv2);
#endif
		gbuffer3.rgb = -log2(gbuffer3.rgb);
		half4 occ = FetchOcclusion(i.uv2);
		half3 ao = lerp(_BaseColor.rgb, half3(1.0, 1.0, 1.0), occ.a);
	    gbuffer3.rgb *= ao;
#if COLOR_BLEEDING
	    gbuffer3.rgb += 1 - occ.rgb;
#endif
	    gbuffer3.rgb = exp2(-gbuffer3.rgb);

	    return gbuffer3;
    }

	half4 frag_multibounce(v2f i) : SV_Target {
#if UNITY_SINGLE_PASS_STEREO
		half4 gbuffer3 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_rt3Tex, UnityStereoTransformScreenSpaceTex(i.uv2));
#else
		half4 gbuffer3 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_rt3Tex, i.uv2);
#endif
		gbuffer3.rgb = -log2(gbuffer3.rgb);
		half4 occ = FetchOcclusion(i.uv2);
		half3 aoColor = lerp(_BaseColor.rgb, half3(1.0, 1.0, 1.0), occ.aaa);
		gbuffer3.rgb *= lerp(aoColor, MultiBounceAO(occ.a, lerp(gbuffer3.rgb, _BaseColor.rgb, _BaseColor.rgb)), _MultiBounceInfluence);
#if COLOR_BLEEDING
		gbuffer3.rgb += 1 - occ.rgb;
#endif
		gbuffer3.rgb = exp2(-gbuffer3.rgb);

		return gbuffer3;
	}

    half4 frag_blend(v2f i) : SV_Target {
		half4 occ = FetchOcclusion(i.uv2);
	    half3 ao = lerp(_BaseColor.rgb, half3(1.0, 1.0, 1.0), occ.a);
	    return half4(ao, 0);
    }

	half4 frag_blend_multibounce(v2f i) : SV_Target {
#if UNITY_SINGLE_PASS_STEREO
		float2 uv = UnityStereoTransformScreenSpaceTex(i.uv2);
		half3 rt3 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_rt3Tex, uv);
#else
		half3 rt3 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_rt3Tex, i.uv2);
#endif
		half4 occ = FetchOcclusion(i.uv2);
		half3 aoColor = lerp(_BaseColor.rgb, half3(1.0, 1.0, 1.0), occ.aaa);
		half3 ao = lerp(aoColor, MultiBounceAO(occ.a, lerp(rt3.rgb, _BaseColor.rgb, _BaseColor.rgb)), _MultiBounceInfluence);

		return half4(ao, 0);
	}

#endif // HBAO_INTEGRATED_INCLUDED
