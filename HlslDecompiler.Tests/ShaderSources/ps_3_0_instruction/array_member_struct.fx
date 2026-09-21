struct struct1
{
	float3 weights[3];
	float k;
};

struct1 g_Blend;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float3 r0;
	float3 r1;
	r0 = g_Blend.weights[0].xyz;
	r1 = g_Blend.weights[2].xyz;
	r0 = r0.xyz * r1.xyz + g_Blend.weights[1].xyz;
	o.xyz = r0.xyz * texcoord.xyz;
	o.w = g_Blend.k;

	return o;
}
