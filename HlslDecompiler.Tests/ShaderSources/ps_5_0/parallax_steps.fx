cbuffer Parallax : register(b0)
{
	float heightScale;
	uint stepCount;
};

SamplerState samp;
Texture2D heightMap;
Texture2D albedo;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float3 viewtangent : VIEWTANGENT;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0 = float3(1 / (float)stepCount, i.viewtangent.xy / max(abs(i.viewtangent.z), 0.0000999999975) * heightScale.xx);
	float4 t1 = float4(i.texcoord, 1, 1);
	float t2 = 1;
	for (uint t3 = 0; t3 < stepCount; t3 = t3 + 1) {
		float t4 = heightMap.SampleLevel(samp, t1.xy, 0).x;
		if (t4 >= t2) {
			t1.xywz = float4(t1.xyz, t4);
			break;
		}
		t1.xywz = float4(-t0.yz * t0.x + t1.xy, t1.z, t4);
		t2 = t2 - t0.x;
	}
	float t5 = (t1.z - t2) / max(t1.z - t2 - (t1.w - (t0.x + t2)), 0.00000999999975);
	return albedo.Sample(samp, lerp(t0.yz * t0.x + t1.xy, t1.xy, t5));
}
