float4 limits;

SamplerState samp;
Texture2D heights;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	r0.xy = asint(texcoord.xy);
	r0.zw = int2(0, 0);
	while (true) {
		r1.x = (r0.w >= 16) ? -1 : 0;
		if (r1.x != 0) break;
		r1 = asint(heights.SampleLevel(samp, asfloat(r0.xy), 0));
		r0.z = asint(asfloat(r1.x) * limits.x + asfloat(r0.z));
		r1.x = (limits.y < asfloat(r0.z)) ? -1 : 0;
		if (r1.x != 0) {
			o.y = (float)r0.w;
			o.zw = float2(1, 1);
			o.x = asfloat(r0.z);
			return o;
		}
		r0.xy = asint(asfloat(r0.xy) + limits.zw);
		r0.w = r0.w + 1;
	}
	o.x = asfloat(r0.z);
	o.yzw = float3(16, 0, 1);

	return o;
}
