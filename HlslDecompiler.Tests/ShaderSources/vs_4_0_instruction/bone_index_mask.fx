cbuffer C : register(b0)
{
	float4x4 bones[8];
};

struct VS_IN
{
	float4 position : POSITION;
	int4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(VS_IN i) : SV_Position
{
	float4 o;

	int4 r0;
	int4 r1;
	float4 r2;
	r0.xy = i.blendindices.xy << 2;
	r0.y = r0.y & 28;
	r1.x = asint(dot(i.position, transpose(bones[r0.y / 4])[0]));
	r1.y = asint(dot(i.position, transpose(bones[r0.y / 4])[1]));
	r1.z = asint(dot(i.position, transpose(bones[r0.y / 4])[2]));
	r1.w = asint(dot(i.position, transpose(bones[r0.y / 4])[3]));
	r1 = asint(asfloat(r1) * i.blendweight.y);
	r2.x = dot(i.position, transpose(bones[r0.x / 4])[0]);
	r2.y = dot(i.position, transpose(bones[r0.x / 4])[1]);
	r2.z = dot(i.position, transpose(bones[r0.x / 4])[2]);
	r2.w = dot(i.position, transpose(bones[r0.x / 4])[3]);
	r0 = asint(r2 * i.blendweight.x + asfloat(r1));
	r1.x = i.blendindices.w + i.blendindices.z;
	r1.y = max(r1.x, -(r1.x));
	r1.xy = r1.xy & int2(-2147483648, 7);
	r1.z = -r1.y;
	r1.x = (r1.x != 0) ? r1.z : r1.y;
	r1.x = r1.x << 2;
	r2.x = dot(i.position, transpose(bones[r1.x / 4])[0]);
	r2.y = dot(i.position, transpose(bones[r1.x / 4])[1]);
	r2.z = dot(i.position, transpose(bones[r1.x / 4])[2]);
	r2.w = dot(i.position, transpose(bones[r1.x / 4])[3]);
	o = r2 * i.blendweight.z + asfloat(r0);

	return o;
}
