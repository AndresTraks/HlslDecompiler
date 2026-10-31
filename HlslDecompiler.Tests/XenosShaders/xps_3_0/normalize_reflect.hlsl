float3 l;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = i.texcoord1 * rsqrt(abs(dot(i.texcoord1, i.texcoord1))) + l;
	float t1 = saturate(dot(i.texcoord.zxy * rsqrt(abs(dot(i.texcoord, i.texcoord))), t0.zxy * rsqrt(abs(dot(t0.zxy, t0.zxy)))));
	return pow(t1, 16);
}
