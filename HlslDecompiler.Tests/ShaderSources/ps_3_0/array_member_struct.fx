struct struct1
{
	float3 weights[3];
	float k;
};

struct1 g_Blend;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4((g_Blend.weights[0] * g_Blend.weights[2] + g_Blend.weights[1]) * texcoord, g_Blend.k);
}
