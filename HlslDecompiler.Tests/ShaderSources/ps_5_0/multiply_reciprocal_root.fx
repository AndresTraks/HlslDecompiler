cbuffer c : register(b0)
{
	float4 light;
};

float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	return float4((light.xyz - texcoord) * rsqrt(abs(light.w) + 1), 1);
}
