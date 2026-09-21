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
	float4 t0 = transpose(g_Transform.skin)[0] + g_Transform.skinTint;
	float3 t1 = transpose(g_Transform.project)[1] + g_Transform.projectTint.xyz + (g_Transform.bone[2] + g_Transform.boneTint.xyz) + t0.xyz;
	return float4(transpose(g_Transform.blend[1])[2] + g_Transform.blendTint.xy + t1.xy, t1.z, t0.w) + texcoord;
}
