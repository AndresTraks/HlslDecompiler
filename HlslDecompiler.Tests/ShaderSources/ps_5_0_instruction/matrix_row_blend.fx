row_major float3x4 bones[4];

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float3 r1;
	r0.xy = i.blendindices.xy * int2(3, 3);
	r1.x = dot(bones[r0.y / 3][0], i.texcoord);
	r1.y = dot(bones[r0.y / 3][1], i.texcoord);
	r1.z = dot(bones[r0.y / 3][2], i.texcoord);
	r0.yzw = r1.xyz * i.blendweight.yyy;
	r1.x = dot(bones[r0.x / 3][0], i.texcoord);
	r1.y = dot(bones[r0.x / 3][1], i.texcoord);
	r1.z = dot(bones[r0.x / 3][2], i.texcoord);
	o.xyz = r1.xyz * i.blendweight.xxx + r0.yzw;
	o.w = 1;

	return o;
}
