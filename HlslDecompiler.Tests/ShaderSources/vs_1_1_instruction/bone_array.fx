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

	int a0;
	float4 r0;
	a0 = i.blendindices.y;
	r0 = i.blendweight.y * bones[a0];
	a0 = i.blendindices.x;
	r0 = bones[a0] * i.blendweight.x + r0;
	a0 = i.blendindices.z;
	r0 = bones[a0] * i.blendweight.z + r0;
	o.x = dot(r0, transpose(wvp)[0]);
	o.y = dot(r0, transpose(wvp)[1]);
	o.z = dot(r0, transpose(wvp)[2]);
	o.w = dot(r0, transpose(wvp)[3]);

	return o;
}
