cbuffer C : register(b0)
{
	int2 snap;
	uint count;
};

Texture2DMS<float4> ms;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float4 color : COLOR;
};

float4 main(PS_IN i) : SV_Target
{
	float4 t0 = 0;
	for (uint t1 = 0; t1 < count; t1 = t1 + 1) {
		float t2 = ms.GetSamplePosition(t1).y;
		float t3 = ms.GetSamplePosition(t1).x;
		t0 = t0 + EvaluateAttributeAtSample(i.color, t1) * (t3 * t2 + 1);
	}
	return float4(EvaluateAttributeCentroid(i.texcoord), EvaluateAttributeSnapped(i.texcoord, snap)) + t0 / (float4)count;
}
