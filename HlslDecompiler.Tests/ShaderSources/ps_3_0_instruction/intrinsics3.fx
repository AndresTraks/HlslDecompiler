float4 a;
float4 b;

struct PS_IN
{
	float3 normal : NORMAL;
	float3 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float3 r1;
	r0.xyz = i.normal.xyz;
	r1 = r0.zxy * i.texcoord.yzx;
	r0.xyz = r0.yzx * i.texcoord.zxy + -r1.xyz;
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = 1 / sqrt(r0.w);
	o.xyz = r0.www * r0.xyz;
	r0.xyz = a.xyz;
	r1 = r0.xyz + -b.xyz;
	r0.z = dot(r1.xyz, r1.xyz);
	r0.z = 1 / sqrt(r0.z);
	r0.z = 1 / r0.z;
	o.w = dot(r0.xy, b.xy) + r0.zz;

	return o;
}
