float4 lightPosition;
float4 lightColour;

SamplerState samp;
Texture2D gbuffer0;
Texture2D<uint4> gbuffer1;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	int3 t0 = int3((int2)i.sv_position.xy, 0);
	float t1 = 0.0000305180438 * (float)(gbuffer1.Load(t0).x & 65535) - 1;
	float t2 = 0.0000305180438 * (float)((uint)gbuffer1.Load(t0).x >> 16) - 1;
	float t3 = 1 - abs(t1) - abs(t2);
	float t4 = max(-t3, 0);
	float t5 = (t1 >= 0 ? -t4 : t4) + t1;
	float t6 = (t2 >= 0 ? -t4 : t4) + t2;
	float t7 = sqrt(t5 * t5 + t6 * t6 + t3 * t3);
	float3 t8 = -i.texcoord1 * asfloat(gbuffer1.Load(t0).y) + lightPosition.xyz;
	float t9 = length(t8);
	float t10 = saturate(1 - length(t8) / lightPosition.w);
	float t11 = saturate(t5 / t7 * (t8.x / t9) + t6 / t7 * (t8.y / t9) + t3 / t7 * (t8.z / t9)) * t10 * t10;
	return float4(t11 * gbuffer0.Sample(samp, i.texcoord).xyz * lightColour.xyz, 1);
}
