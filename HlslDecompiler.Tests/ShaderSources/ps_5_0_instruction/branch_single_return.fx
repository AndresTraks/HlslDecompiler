float k;

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	r0 = t.SampleLevel(s, texcoord.xy, 0);
	r1.x = (k < texcoord.x) ? -1 : 0;
	if (r1.x != 0) {
		r1.xy = asint(texcoord.xy + texcoord.xy);
		r1 = asint(t.SampleLevel(s, asfloat(r1.xy), 1));
		o = r0 * asfloat(r1);
	} else {
		o = r0;
	}

	return o;
}
