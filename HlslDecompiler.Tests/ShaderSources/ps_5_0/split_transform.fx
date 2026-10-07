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
	float2 t3 = mul(t2, (float4x2)decalMatrix);
	float2 t4 = t3 + 0.5;
	float4 t5 = decalMap.Sample(linearSampler, t4);
	return float4(t5.xyz * decalTint.xyz, step(abs(dot(transpose(decalMatrix)[2], t2)), 0.5) * step(abs(t3.y), 0.5) * step(abs(t3.x), 0.5) * t5.w * fade);
}
