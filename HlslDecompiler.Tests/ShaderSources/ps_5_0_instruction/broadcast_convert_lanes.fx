cbuffer C : register(b0)
{
	int maxSteps;
	float limit;
};

struct PS_IN
{
	float4 sv_position : SV_Position;
	float3 dir : DIR;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	int r1;
	r0.x = asint(dot(i.dir.xyz, i.dir.xyz));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.yz = int2(0, 0);
	while (true) {
		r0.w = (r0.y >= maxSteps) ? -1 : 0;
		if (r0.w != 0) break;
		r0.w = asint(asfloat(r0.x) * 0.100000001 + asfloat(r0.z));
		r1 = (limit < asfloat(r0.w)) ? -1 : 0;
		if (r1.x != 0) {
			r0.z = r0.w;
			break;
		}
		r0.y = r0.y + 1;
		r0.yz = r0.yw;
	}
	r0.x = asint((float)r0.y);
	r0.y = asint((float)maxSteps);
	r0.x = asint(asfloat(r0.x) / asfloat(r0.y));
	r0.x = asint(-(asfloat(r0.x)) + 1);
	o.xyz = asfloat(r0.zzz) * asfloat(r0.xxx);
	o.w = 1;

	return o;
}
