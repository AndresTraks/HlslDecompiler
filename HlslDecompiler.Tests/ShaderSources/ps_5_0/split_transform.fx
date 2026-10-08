cbuffer Decal : register(b0)
{
	float4x4 invViewProjection;
	float4x4 decalMatrix;
	float4 decalTint;
	float fade;
};

SamplerState linearSampler;
Texture2D depthMap;
Texture2D decalMap;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 t0 = float4(2 * i.texcoord - 1, depthMap.Sample(linearSampler, i.texcoord).x, 1);
	float t1 = dot(transpose(invViewProjection)[3], t0);
	float4 t2 = float4(mul(t0, (float4x3)invViewProjection), t1) / t1;
	float3 t3 = mul(t2, (float4x3)decalMatrix);
	float2 t4 = t3.xy + 0.5;
	float4 t5 = decalMap.Sample(linearSampler, t4);
	float3 t6 = step(abs(t3), 0.5);
	return float4(t5.xyz * decalTint.xyz, t6.z * t6.y * t6.x * t5.w * fade);
}
