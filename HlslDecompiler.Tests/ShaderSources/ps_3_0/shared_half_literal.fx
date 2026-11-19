float3 lightDir;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = tex2D(s0, texcoord);
	half3 t1 = t0.w * (2 * t0.xyz - half3(1, 1, 2)) + half3(0, 0, 1);
	return float4(dot(lightDir, t1), dot(lightDir, t1), dot(lightDir, t1), (half)1);
}
