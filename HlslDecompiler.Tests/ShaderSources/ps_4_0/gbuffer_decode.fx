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
	float2 t1 = 0.0000305180438 * float2((float)(gbuffer1.Load(t0).x & 65535), (float)((uint)gbuffer1.Load(t0).x >> 16)) - 1;
	float t2 = 1 - abs(t1.x) - abs(t1.y);
	float t3 = max(-t2, 0);
	float2 t4 = (t1 >= 0 ? -t3 : t3) + t1;
	float3 t5 = -i.texcoord1 * asfloat(gbuffer1.Load(t0).y) + lightPosition.xyz;
	float t6 = saturate(1 - length(t5) / lightPosition.w);
	float t7 = saturate(dot(normalize(float3(t4, t2)), normalize(t5))) * t6 * t6;
	return float4(t7 * gbuffer0.Sample(samp, i.texcoord).xyz * lightColour.xyz, 1);
}
