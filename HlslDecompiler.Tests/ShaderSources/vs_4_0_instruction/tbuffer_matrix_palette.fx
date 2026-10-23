cbuffer C : register(b0)
{
	float4x4 vp;
	float weight;
	uint vertexCount;
};

tbuffer Palette : register(t0)
{
	float4x3 bones[64];
};

Buffer<float4> deltas : register(t1);

struct VS_IN
{
	float3 position : POSITION;
	uint4 bones : BONES;
	float4 weights : WEIGHTS;
	uint sv_vertexid : SV_VertexID;
};

float4 main(VS_IN i) : SV_Position
{
	float4 o;

	int4 r0;
	float4 r1;
	int4 r2;
	int4 r3;
	float4 r4;
	r0.xy = i.bones.xy * int2(3, 3);
	r1 = transpose(bones[r0.y / 3])[r0.y % 3];
	r0 = asint(transpose(bones[r0.x / 3])[r0.x % 3]);
	r2 = asint(deltas.Load(i.sv_vertexid));
	r2.xyz = asint(asfloat(r2.xyz) * weight + i.position.xyz);
	r2.w = i.sv_vertexid + vertexCount;
	r3 = asint(deltas.Load(r2.w));
	r2.w = asint(-(weight) + 1);
	r2.xyz = asint(asfloat(r3.xyz) * asfloat(r2.www) + asfloat(r2.xyz));
	r2.w = 1065353216;
	r1.x = dot(asfloat(r2), r1);
	r3 = i.bones.xxyy * int4(3, 3, 3, 3) + int4(2, 1, 2, 1);
	r4 = transpose(bones[r3.w / 3])[r3.w % 3];
	r1.y = dot(asfloat(r2), r4);
	r4 = transpose(bones[r3.z / 3])[r3.z % 3];
	r1.z = dot(asfloat(r2), r4);
	r1.xyz = r1.xyz * i.weights.yyy;
	r0.x = asint(dot(asfloat(r2), asfloat(r0)));
	r4 = transpose(bones[r3.y / 3])[r3.y % 3];
	r3 = asint(transpose(bones[r3.x / 3])[r3.x % 3]);
	r0.z = asint(dot(asfloat(r2), asfloat(r3)));
	r0.y = asint(dot(asfloat(r2), r4));
	r0.xyz = asint(asfloat(r0.xyz) * i.weights.xxx + r1.xyz);
	r0.w = 1065353216;
	o.x = dot(asfloat(r0), transpose(vp)[0]);
	o.y = dot(asfloat(r0), transpose(vp)[1]);
	o.z = dot(asfloat(r0), transpose(vp)[2]);
	o.w = dot(asfloat(r0), transpose(vp)[3]);

	return o;
}
