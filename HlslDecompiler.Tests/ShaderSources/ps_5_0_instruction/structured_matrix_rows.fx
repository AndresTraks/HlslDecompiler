cbuffer c : register(b0)
{
	uint idx;
};

StructuredBuffer<float4x4> mats : register(t0);

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float3 r3;
	r0 = transpose(mats[idx])[3];
	r0.w = dot(r0, texcoord);
	r1 = transpose(mats[idx])[1];
	r2 = transpose(mats[idx])[2];
	r3 = r1.zxy * r2.yzx;
	r3 = r1.yzx * r2.zxy + -(r3.xyz);
	r0.y = dot(r1, texcoord);
	r0.z = dot(r2, texcoord);
	r1 = transpose(mats[idx])[0];
	r2.x = dot(r3.xyz, r1.xyz);
	r0.x = dot(r1, texcoord);
	o = r0 + r2.x;

	return o;
}
