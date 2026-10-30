float4x4 bones[10];
float4 col[16];

struct VS_IN
{
	float4 position : POSITION;
	float4 blendindices : BLENDINDICES;
};

float4 main(VS_IN i) : POSITION
{
	float4 o;

	float4 r0;
	int2 a0;
	r0.x = i.blendindices.y + i.blendindices.y;
	r0.x = frac(r0.x);
	r0.y = i.blendindices.y * 2 + 1;
	r0.x = r0.y + -r0.x;
	a0.x = r0.x;
	r0.x = 2 + i.blendindices.x;
	r0.y = frac(i.blendindices.x);
	r0.x = r0.x + -r0.y;
	r0.x = r0.x * 4;
	a0.y = r0.x;
	r0.x = dot(i.position, transpose(bones[a0.y / 4])[0]);
	r0.y = dot(i.position, transpose(bones[a0.y / 4])[1]);
	r0.z = dot(i.position, transpose(bones[a0.y / 4])[2]);
	r0.w = dot(i.position, transpose(bones[a0.y / 4])[3]);
	o = r0 + col[a0.x];

	return o;
}
