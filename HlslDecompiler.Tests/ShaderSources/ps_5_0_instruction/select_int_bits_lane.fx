cbuffer c : register(b0)
{
	int4 n;
	float4 f;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float3 r0;
	int4 r1;
	r0 = (float3)n.xzw;
	r0.yz = r0.yz * float2(0.5, 0.5);
	r1 = (float4(0, 0, 0, 0) < texcoord) ? -1 : 0;
	o.xzw = (r1.xzw != 0) ? r0.xyz : f.xzw;
	o.y = (r1.y != 0) ? asfloat(n.y) : f.y;

	return o;
}
