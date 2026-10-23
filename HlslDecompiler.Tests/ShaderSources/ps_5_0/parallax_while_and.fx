cbuffer C : register(b0)
{
	float scale;
	int minSteps;
	int maxSteps;
};

SamplerState s;
Texture2D albedo;
Texture2D<float> height;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float3 view : VIEW;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t2 = normalize(i.view);
	float2 t1 = t2.xy / t2.z * scale;
	float t0 = 1 / trunc(lerp((float)maxSteps, (float)minSteps, abs(t2.z)));
	float2 t3 = t0 * t1;
	float2 t4 = float2(0, height.SampleLevel(s, i.texcoord, 0).x);
	float2 t5 = i.texcoord;
	while (t4.x < 1 && t4.x < t4.y) {
		t4 = float2(t0 + t4.x, height.SampleLevel(s, -t1 * t0 + t5, 0).x);
		t5 = t5 - t1 * t0;
	}
	float t6 = height.SampleLevel(s, t1 * t0 + t5, 0).x;
	float t7 = (t4.y - t4.x) / (t4.y - t4.x - (t6 - t4.x + t0));
	return albedo.Sample(s, t7 * t3 + t5);
}
