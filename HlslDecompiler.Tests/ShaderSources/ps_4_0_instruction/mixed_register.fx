float4 k;
int steps;

SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	r0.xyz = int3(0, 0, 0);
	while (true) {
		r0.w = (r0.z >= steps) ? -1 : 0;
		if (r0.w != 0) break;
		r0.w = asint((float)r0.z);
		r1.xy = asint(k.xy * asfloat(r0.ww) + texcoord.xy);
		r1 = asint(tex.SampleLevel(samp, asfloat(r1.xy), 0));
		r0.w = (k.z < asfloat(r1.x)) ? -1 : 0;
		r1.x = asint(asfloat(r0.x) + asfloat(r1.x));
		r1.y = r0.y + 1;
		r0.xy = (r0.ww != 0) ? r1.xy : r0.xy;
		r0.z = r0.z + 1;
	}
	r1.y = asint((float)r0.y);
	r0.y = asint(max(asfloat(r1.y), 1));
	r1.x = asint(asfloat(r0.x) / asfloat(r0.y));
	o.z = saturate(asfloat(r1.x));
	o.w = k.w;
	o.xy = asfloat(r1.xy);

	return o;
}
