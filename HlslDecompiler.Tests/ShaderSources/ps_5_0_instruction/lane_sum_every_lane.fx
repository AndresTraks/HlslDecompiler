float4 c;

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = t.Sample(s, texcoord.xy);
	r1 = r0.wxyz * c.wxyz;
	r1 = r1.zwxy + r1.yzwx;
	r1 = r0.zwxy * c.zwxy + r1;
	o = r0.wxyz * c.wxyz + r1;

	return o;
}
