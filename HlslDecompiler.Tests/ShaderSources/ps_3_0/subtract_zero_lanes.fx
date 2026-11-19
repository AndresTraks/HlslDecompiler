float bump;
float3 lightDir;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half3 t0 = tex2D(s0, texcoord).xyz;
	half3 t1 = bump * (t0 - float3(0, 0, 1)) + half3(0, 0, 1);
	return float4(dot(lightDir, t1), dot(lightDir, t1), dot(lightDir, t1), 1);
}
