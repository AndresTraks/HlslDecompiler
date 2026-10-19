float4 weights;

SamplerState samp;
Texture2D source;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	int4 r2;
	r0 = int4(0, 0, 0, 0);
	r1.x = 0;
	while (true) {
		r1.y = (r1.x >= 8) ? -1 : 0;
		if (r1.y != 0) break;
		r1.y = asint((float)r1.x);
		r1.yz = asint(weights.zw * asfloat(r1.yy) + texcoord.xy);
		r2 = asint(source.SampleLevel(samp, asfloat(r1.yz), 0));
		r1.yzw = asint(asfloat(r2.xyz) * weights.xxx + asfloat(r0.xyz));
		r2.x = (asfloat(r2.x) < weights.y) ? -1 : 0;
		if (r2.x != 0) {
			r2.x = r1.x + 1;
			r0.xyz = r1.yzw;
			r1.x = r2.x;
			continue;
		}
		r0.w = asint(asfloat(r0.w) + 1);
		r1.x = r1.x + 1;
		r0.xyz = r1.yzw;
	}
	r1.x = asint(max(asfloat(r0.w), 1));
	o.xyz = asfloat(r0.xyz) / asfloat(r1.xxx);
	o.w = asfloat(r0.w);

	return o;
}
