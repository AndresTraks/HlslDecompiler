float4 bones[16];
float4x4 wvp;

struct VS_IN
{
	float4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(VS_IN i) : POSITION
{
	float4 o;

	float4 r0;
	int2 a0;
	float4 r1;
	r0.xyz = frac(i.blendindices.xyz);
	r0.xyz = -r0.xyz + i.blendindices.xyz;
	a0.x = r0.y;
	r1 = i.blendweight.y * bones[a0.x];
	a0 = r0.xz;
	r0 = bones[a0.x] * i.blendweight.x + r1;
	r0 = bones[a0.y] * i.blendweight.z + r0;
	o.x = dot(r0, transpose(wvp)[0]);
	o.y = dot(r0, transpose(wvp)[1]);
	o.z = dot(r0, transpose(wvp)[2]);
	o.w = dot(r0, transpose(wvp)[3]);

	return o;
}
