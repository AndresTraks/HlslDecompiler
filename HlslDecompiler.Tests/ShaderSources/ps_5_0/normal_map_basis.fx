float3 lightDirection;

SamplerState samp;
Texture2D normalMap;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0 = normalize(i.normal);
	float t1 = dot(i.tangent.xyz, t0);
	float3 t2 = -t0.yzx * t1 + i.tangent.yzx;
	float3 t3 = normalize(t2);
	float3 t4 = normalMap.Sample(samp, i.texcoord).xyz;
	float3 t5 = 2 * t4 - 1;
	float3 t6 = t3.zxy * t5.x + (cross(t0, t3.zxy)) * i.tangent.w * t5.y + t0 * t5.z;
	float3 t7 = normalize(t6);
	return float4(0.5 * t7 + 0.5, saturate(lightDirection.x * t7.x + lightDirection.y * t7.y + lightDirection.z * t7.z));
}
