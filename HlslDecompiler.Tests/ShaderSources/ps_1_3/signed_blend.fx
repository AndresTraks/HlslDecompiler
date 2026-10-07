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
	float4 t2 = 0.5 * (t0 - (float4(t1 - 0.5, 0.5)));
	float3 t3 = lerp(t0.xyz, saturate(4 * (t2.xyz * (-2 * t1 + 1) + i.color1.xyz)), float3(0.5, 0.25, 0.75));
	float t4 = saturate(4 * (i.color1.w - t2.w)) + t2.z;
	float t5 = 0.25 * (dot(float3(0.300000012, -0.699999988, 0.400000006), t3) + 0.899999976 * t4) - 0.5;
	return float4(0.5 - t4 >= 0 ? 1 - t1 : t5 >= 0 ? t3 : 0.5 - t0.xyz, 0.5 - t4 >= 0 ? 0 : t4);
}
