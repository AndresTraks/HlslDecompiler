sampler2D diffuse;
float3x3 transform;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half3 t0 = tex2D(diffuse, texcoord).xyz;
	return float4(mul(t0, transform), 1);
}
