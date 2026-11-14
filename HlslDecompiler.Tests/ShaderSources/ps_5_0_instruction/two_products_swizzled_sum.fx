float4 c;
float4 k;

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = t.Sample(s, texcoord.xy);
	r1 = r0.yzwx * c.yzwx;
	r1 = r0.yzwx * k.yzwx + r1.wxyz;
	o = r0.yzwx * c.yzwx + r1;

	return o;
}
