float4 march;

SamplerState samp;
Texture3D volume;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	r0.x = dot(i.texcoord1.xyz, i.texcoord1.xyz);
	r0.x = 1 / sqrt(r0.x);
	r0.xyz = r0.xxx * i.texcoord1.xyz;
	r1.xyz = i.texcoord.xyz;
	r2.xyz = float3(0, 0, 0);
	r0.w = 0;
	r1.w = 0;
	while (true) {
		r2.w = (r1.w >= 32) ? -1 : 0;
		if (asint(r2.w) != 0) break;
		r3 = volume.SampleLevel(samp, r1.xyz, 0);
		r2.w = r3.x * march.y;
		r3.x = -(r0.w) + 1;
		r3.y = r2.w * r3.x;
		r3.yzw = r3.yyy * march.zzz + r2.xyz;
		r2.w = r2.w * r3.x + r0.w;
		r3.x = asfloat((march.w < r2.w) ? -1 : 0);
		if (asint(r3.x) != 0) {
			r2.xyz = r3.yzw;
			r0.w = r2.w;
			break;
		}
		r1.xyz = r0.xyz * march.xxx + r1.xyz;
		r1.w = r1.w + 1;
		r2.xyz = r3.yzw;
		r0.w = r2.w;
	}
	o.xyz = r2.xyz;
	o.w = r0.w;

	return o;
}
