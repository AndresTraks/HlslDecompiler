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

	int4 r0;
	float4 r1;
	int3 r2;
	float3 r3;
	r0.x = i.blendindices.y;
	r1 = i.blendweight.y * dual[r0.x];
	r0 = asint(i.blendweight.y * real[r0.x]);
	r2.x = i.blendindices.x;
	r1 = dual[r2.x] * i.blendweight.x + r1;
	r0 = asint(real[r2.x] * i.blendweight.x + asfloat(r0));
	r2.x = asint(dot(asfloat(r0), asfloat(r0)));
	r2.x = asint(sqrt(asfloat(r2.x)));
	r1 = r1 / asfloat(r2.x);
	r0 = asint(asfloat(r0) / asfloat(r2.x));
	r2 = asint(asfloat(r0.xyz) * r1.www);
	r2 = asint(asfloat(r0.www) * r1.xyz + -(asfloat(r2.xyz)));
	r3 = r1.yzx * asfloat(r0.zxy);
	r1.xyz = asfloat(r0.yzx) * r1.zxy + -(r3.xyz);
	r1.xyz = r1.xyz + asfloat(r2.xyz);
	r2 = asint(asfloat(r0.xyz) * i.position.zxy);
	r2 = asint(asfloat(r0.zxy) * i.position.xyz + -(asfloat(r2.xyz)));
	r2 = asint(asfloat(r0.www) * i.position.yzx + asfloat(r2.xyz));
	r3 = asfloat(r0.zxy) * asfloat(r2.xyz);
	r0.xyz = asint(asfloat(r0.yzx) * asfloat(r2.yzx) + -(r3.xyz));
	r0.xyz = asint(asfloat(r0.xyz) * float3(2, 2, 2) + i.position.xyz);
	r0.xyz = asint(r1.xyz * float3(2, 2, 2) + asfloat(r0.xyz));
	r0.w = 1065353216;
	o.x = dot(asfloat(r0), transpose(vp)[0]);
	o.y = dot(asfloat(r0), transpose(vp)[1]);
	o.z = dot(asfloat(r0), transpose(vp)[2]);
	o.w = dot(asfloat(r0), transpose(vp)[3]);

	return o;
}
