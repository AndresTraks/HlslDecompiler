struct struct1
{
	float4x3 skin;
	float4 skinTint;
	float3x4 project;
	float4 projectTint;
	row_major float4x3 bone;
	float4 boneTint;
	float2x3 blend[2];
	float4 blendTint;
};

cbuffer Params : register(b0)
{
	struct1 g_Transform;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0.xyz = transpose(g_Transform.project)[1].xyz + g_Transform.projectTint.xyz;
	r1.xyz = g_Transform.bone[2].xyz + g_Transform.boneTint.xyz;
	r0.xyz = r0.xyz + r1.xyz;
	r1 = transpose(g_Transform.skin)[0] + g_Transform.skinTint;
	r1.xyz = r0.xyz + r1.xyz;
	r0.xy = transpose(g_Transform.blend[1])[2].xy + g_Transform.blendTint.xy;
	r0.zw = float2(0, 0);
	r0 = r0 + r1;
	o = r0 + texcoord;

	return o;
}
