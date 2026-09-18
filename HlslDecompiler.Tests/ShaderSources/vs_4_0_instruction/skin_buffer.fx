float4x4 viewProjection;

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

Buffer<float4> bones;

struct VS_IN
{
	float4 position : POSITION;
	float4 blendweight : BLENDWEIGHT;
	uint4 blendindices : BLENDINDICES;
};

float4 main(VS_IN i) : SV_Position
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	r0.xyz = float3(0, 0, 0);
	r1.x = 0;
	while (true) {
		r1.y = asfloat((r1.x >= 3) ? -1 : 0);
		if (asint(r1.y) != 0) break;
		r1.y = asfloat(-(int)r1.x);
		r2.xyz = asfloat((r1.xxx < int3(1, 2, 3)) ? -1 : 0);
		r3.y = asfloat(asint(r1.y) & asint(r2.y));
		r1.yz = asfloat((int2)r1.xx + int2(-3, 1));
		r3.z = (asint(r2.y) != 0) ? 0 : r1.y;
		r3.w = asfloat((asint(r2.z) == 0) ? -1 : 0);
		r3.x = r2.x;
		r2 = asfloat(asint(r3) & i.blendindices);
		r1.yw = asfloat(asint(r2.yw) | asint(r2.xz));
		r1.y = asfloat(asint(r1.w) | asint(r1.y));
		r1.w = asfloat(asint(r1.y) * 3);
		r2 = bones.Load(asint(r1.w));
		r1.yw = asfloat(asint(r1.yy) * int2(3, 3) + int2(1, 2));
		r3 = bones.Load(asint(r1.y));
		r4 = bones.Load(asint(r1.w));
		r2.x = dot(r2, i.position);
		r2.y = dot(r3, i.position);
		r2.z = dot(r4, i.position);
		r1.y = dot(i.blendweight, icb[r1.x]);
		r0.xyz = r2.xyz * r1.yyy + r0.xyz;
		r1.x = r1.z;
	}
	r0.w = 1;
	o.x = dot(r0, transpose(viewProjection)[0]);
	o.y = dot(r0, transpose(viewProjection)[1]);
	o.z = dot(r0, transpose(viewProjection)[2]);
	o.w = dot(r0, transpose(viewProjection)[3]);

	return o;
}
