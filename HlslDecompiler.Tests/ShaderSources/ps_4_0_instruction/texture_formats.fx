SamplerState samp;
Texture2D<unorm float4> a;
Texture2D<snorm float4> b;
Texture2D<int4> c;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	float4 r1;
	float4 r2;
	r0.xy = (int2)texcoord.xy;
	r0.zw = int2(0, 0);
	r0 = c.Load(r0.xyz);
	r0 = asint((float4)r0);
	r1 = a.Sample(samp, texcoord.xy);
	r2 = b.Sample(samp, texcoord.xy);
	r1 = r1 + r2;
	o = asfloat(r0) + r1;

	return o;
}
