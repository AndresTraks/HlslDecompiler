float4 real[32];
float4 dual[32];
float4x4 vp;

struct VS_IN
{
	float3 position : POSITION;
	uint4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(VS_IN i) : SV_Position
{
	float4 o;

	float4 r0;
	float4 r1;
	float3 r2;
	float3 r3;
	r0.x = (float)i.blendindices.y;
	r1 = i.blendweight.y * dual[r0.x];
	r0 = i.blendweight.y * real[r0.x];
	r2.x = (float)i.blendindices.x;
	r1 = dual[r2.x] * i.blendweight.x + r1;
	r0 = real[r2.x] * i.blendweight.x + r0;
	r2.x = dot(r0, r0);
	r2.x = sqrt(r2.x);
	r1 = r1 / r2.x;
	r0 = r0 / r2.x;
	r2 = r0.xyz * r1.www;
	r2 = r0.www * r1.xyz + -(r2.xyz);
	r3 = r1.yzx * r0.zxy;
	r1.xyz = r0.yzx * r1.zxy + -(r3.xyz);
	r1.xyz = r1.xyz + r2.xyz;
	r2 = r0.xyz * i.position.zxy;
	r2 = r0.zxy * i.position.xyz + -(r2.xyz);
	r2 = r0.www * i.position.yzx + r2.xyz;
	r3 = r0.zxy * r2.xyz;
	r0.xyz = r0.yzx * r2.yzx + -(r3.xyz);
	r0.xyz = r0.xyz * float3(2, 2, 2) + i.position.xyz;
	r0.xyz = r1.xyz * float3(2, 2, 2) + r0.xyz;
	r0.w = 1;
	o.x = dot(r0, transpose(vp)[0]);
	o.y = dot(r0, transpose(vp)[1]);
	o.z = dot(r0, transpose(vp)[2]);
	o.w = dot(r0, transpose(vp)[3]);

	return o;
}
