cbuffer Blur : register(b0)
{
	float2 texelSize;
	int radius;
	float sigma;
	float4 weights[4];
};

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

SamplerState linearSampler;
Texture2D source;
Texture2D depth;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	float4 r2;
	int3 r3;
	float4 r4;
	r0.x = asint(depth.Sample(linearSampler, texcoord.xy).x);
	r0.y = (asfloat(r0.x) >= 1) ? -1 : 0;
	if (r0.y != 0) discard;
	r0.y = -radius;
	r0.z = asint(dot(sigma, sigma));
	r1.y = 0;
	r2 = float4(0, 0, 0, 0);
	r0.w = 0;
	r1.z = r0.y;
	[loop]
	while (true) {
		r1.w = (radius < r1.z) ? -1 : 0;
		if (r1.w != 0) break;
		r1.x = asint((float)r1.z);
		r1.xw = asint(asfloat(r1.xy) * texelSize.xy + texcoord.xy);
		r3.x = asint(depth.Sample(linearSampler, asfloat(r1.xw)).x);
		r3.x = asint(-(asfloat(r0.x)) + asfloat(r3.x));
		r3.x = (0.00999999978 < abs(asfloat(r3.x))) ? -1 : 0;
		if (r3.x != 0) {
			r3.x = r1.z + 1;
			r1.z = r3.x;
			continue;
		}
		r3.x = max(-(r1.z), r1.z);
		r3.y = r3.x & 3;
		r3.x = (uint)r3.x >> 2;
		r3.x = asint(dot(weights[r3.x], icb[r3.y]));
		r3.y = r1.z * -(r1.z);
		r3.y = asint((float)r3.y);
		r3.y = asint(asfloat(r3.y) / asfloat(r0.z));
		r3.y = asint(asfloat(r3.y) * 1.44269502);
		r3.y = asint(exp2(asfloat(r3.y)));
		r3.z = asint(asfloat(r3.y) * asfloat(r3.x));
		r4 = source.Sample(linearSampler, asfloat(r1.xw));
		r2 = r4 * asfloat(r3.z) + r2;
		r0.w = asint(asfloat(r3.x) * asfloat(r3.y) + asfloat(r0.w));
		r1.z = r1.z + 1;
	}
	r0.x = (0 < asfloat(r0.w)) ? -1 : 0;
	r1 = asint(r2 / asfloat(r0.w));
	r2 = source.Sample(linearSampler, texcoord.xy);
	o = (r0.x != 0) ? asfloat(r1) : r2;

	return o;
}
