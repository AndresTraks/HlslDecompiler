sampler2D s0;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float4 color1 : COLOR1;
};

float4 main(PS_IN i) : COLOR
{
	float4 t0 = tex2D(s0, i.texcoord);
	float3 t1 = saturate(i.texcoord1);
	clip(i.texcoord2);
	float3 t2 = 0.5 * (t0.xyz - (t1 - 0.5));
	float t3 = 0.25 * (0.300000012 * lerp(t0.x, saturate(4 * (t2.x * (-2 * t1.x + 1) + i.color1.x)), 0.5) + -0.699999988 * lerp(t0.y, saturate(4 * (t2.y * (-2 * t1.y + 1) + i.color1.y)), 0.25) + 0.400000006 * lerp(t0.z, saturate(4 * (t2.z * (-2 * t1.z + 1) + i.color1.z)), 0.75) + 0.899999976 * (saturate(4 * (i.color1.w - 0.5 * (t0.w - 0.5))) + t2.z));
	float4 t4 = float4(t2 * (-2 * t1 + 1) + i.color1.xyz, i.color1.w - 0.5 * (t0.w - 0.5));
	float4 t5 = 4 * t4;
	float3 t6 = lerp(t0.xyz, saturate(t5.xyz), float3(0.5, 0.25, 0.75));
	float t7 = saturate(t5.w) + t2.z;
	return float4(0.5 - t7 >= 0 ? 1 - t1 : 0.25 * (0.300000012 * t6.x + -0.699999988 * t6.y + 0.400000006 * t6.z + 0.899999976 * t7) - 0.5 >= 0 ? t6 : 0.5 - t0.xyz, 0.5 - t7 >= 0 ? 0 : t7);
}
