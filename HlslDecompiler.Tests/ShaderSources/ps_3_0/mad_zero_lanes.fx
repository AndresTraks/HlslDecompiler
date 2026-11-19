float bump;
float3 lightDir;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half3 t0 = bump * (2 * (half3)(tex2D(s0, texcoord).xyz) - float3(1, 1, 2)) + half3(0, 0, 1);
	return float4(dot(lightDir, t0), dot(lightDir, t0), dot(lightDir, t0), 1);
}
