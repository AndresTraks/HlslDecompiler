float4 march;

SamplerState samp;
Texture3D volume;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float3 texcoord[2] : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	int4 r2;
	int4 r3;
	r0.x = asint(dot(i.texcoord[1].xyz, i.texcoord[1].xyz));
	r0.x = asint(rsqrt(asfloat(r0.x)));
	r0.xyz = asint(asfloat(r0.xxx) * i.texcoord[1].xyz);
	r1.xyz = asint(i.texcoord[0].xyz);
	r2.xyz = int3(0, 0, 0);
	r0.w = 0;
	r1.w = 0;
	while (true) {
		r2.w = (r1.w >= 32) ? -1 : 0;
		if (r2.w != 0) break;
		r3 = asint(volume.SampleLevel(samp, asfloat(r1.xyz), 0));
		r2.w = asint(asfloat(r3.x) * march.y);
		r3.x = asint(-(asfloat(r0.w)) + 1);
		r3.y = asint(asfloat(r2.w) * asfloat(r3.x));
		r3.yzw = asint(asfloat(r3.yyy) * march.zzz + asfloat(r2.xyz));
		r2.w = asint(asfloat(r2.w) * asfloat(r3.x) + asfloat(r0.w));
		r3.x = (march.w < asfloat(r2.w)) ? -1 : 0;
		if (r3.x != 0) {
			r2.xyz = r3.yzw;
			r0.w = r2.w;
			break;
		}
		r1.xyz = asint(asfloat(r0.xyz) * march.xxx + asfloat(r1.xyz));
		r1.w = r1.w + 1;
		r2.xyz = r3.yzw;
		r0.w = r2.w;
	}
	o.xyz = asfloat(r2.xyz);
	o.w = asfloat(r0.w);

	return o;
}
