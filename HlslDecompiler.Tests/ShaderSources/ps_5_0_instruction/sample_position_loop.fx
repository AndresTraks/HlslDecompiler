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
	float4 o;

	float4 r0;
	int4 r1;
	float4 r2;
	r0 = float4(0, 0, 0, 0);
	r1.x = 0;
	while (true) {
		r1.y = ((uint)r1.x >= count) ? -1 : 0;
		if (r1.y != 0) break;
		r1.yz = asint(ms.GetSamplePosition(r1.x));
		r2 = EvaluateAttributeAtSample(i.color, r1.x);
		r1.y = asint(asfloat(r1.y) * asfloat(r1.z) + 1);
		r0 = r2 * asfloat(r1.y) + r0;
		r1.x = r1.x + 1;
	}
	r1.xy = asint(EvaluateAttributeCentroid(i.texcoord.xy));
	r1.zw = asint(EvaluateAttributeSnapped(i.texcoord.xy, snap.xy));
	r2.x = (float)(uint)count;
	r0 = r0 / r2.x;
	o = asfloat(r1) + r0;

	return o;
}
