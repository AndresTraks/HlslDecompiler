float3x4 bones[4];

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
	r0.xy = i.blendindices.xy << int2(2, 2);
	r1 = i.texcoord.yyy * transpose(bones[r0.y / 4])[1].xyz;
	r1 = transpose(bones[r0.y / 4])[0].xyz * i.texcoord.xxx + r1.xyz;
	r1 = transpose(bones[r0.y / 4])[2].xyz * i.texcoord.zzz + r1.xyz;
	r0.yzw = transpose(bones[r0.y / 4])[3].xyz * i.texcoord.www + r1.xyz;
	r0.yzw = r0.yzw * i.blendweight.yyy;
	r1 = i.texcoord.yyy * transpose(bones[r0.x / 4])[1].xyz;
	r1 = transpose(bones[r0.x / 4])[0].xyz * i.texcoord.xxx + r1.xyz;
	r1 = transpose(bones[r0.x / 4])[2].xyz * i.texcoord.zzz + r1.xyz;
	r1 = transpose(bones[r0.x / 4])[3].xyz * i.texcoord.www + r1.xyz;
	o.xyz = r1.xyz * i.blendweight.xxx + r0.yzw;
	o.w = 1;

	return o;
}
