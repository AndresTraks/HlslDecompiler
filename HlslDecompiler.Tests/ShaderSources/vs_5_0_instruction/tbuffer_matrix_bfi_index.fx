tbuffer tb : register(t0)
{
	float4x4 boneMats[16];
	float4 tint;
};

struct VS_IN
{
	float3 position : POSITION;
	uint2 blendindices : BLENDINDICES;
	float2 blendweight : BLENDWEIGHT;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	int4 r0;
	float4 r1;
	float4 r2;
	int4 r3;
	float4 r4;
	r0.xy = i.blendindices.xy << int2(2, 2);
	r1 = transpose(boneMats[r0.y / 4])[r0.y % 4];
	r0 = asint(transpose(boneMats[r0.x / 4])[r0.x % 4]);
	r2.xyz = i.position.xyz;
	r2.w = 1;
	r1.x = dot(r2, r1);
	r3.xy = (int2(2, 1) & ~(int2(1073741823, 1073741823) << int2(2, 2))) | ((i.blendindices.yy << int2(2, 2)) & (int2(1073741823, 1073741823) << int2(2, 2)));
	r4 = transpose(boneMats[r3.y / 4])[r3.y % 4];
	r3 = asint(transpose(boneMats[r3.x / 4])[r3.x % 4]);
	r1.z = dot(r2, asfloat(r3));
	r1.y = dot(r2, r4);
	r3 = (int4(3, 2, 1, 3) & ~(int4(1073741823, 1073741823, 1073741823, 1073741823) << int4(2, 2, 2, 2))) | ((i.blendindices.xxxy << int4(2, 2, 2, 2)) & (int4(1073741823, 1073741823, 1073741823, 1073741823) << int4(2, 2, 2, 2)));
	r4 = transpose(boneMats[r3.w / 4])[r3.w % 4];
	r1.w = dot(r2, r4);
	r1 = r1 * i.blendweight.y;
	r0.x = asint(dot(r2, asfloat(r0)));
	r4 = transpose(boneMats[r3.z / 4])[r3.z % 4];
	r0.y = asint(dot(r2, r4));
	r4 = transpose(boneMats[r3.y / 4])[r3.y % 4];
	r3 = asint(transpose(boneMats[r3.x / 4])[r3.x % 4]);
	r0.w = asint(dot(r2, asfloat(r3)));
	r0.z = asint(dot(r2, r4));
	o.sv_position = asfloat(r0) * i.blendweight.x + r1;
	r0 = asint(tint);
	o.color = asfloat(r0);

	return o;
}
