float4 parallax;

SamplerState samp;
Texture2D heightMap;
Texture2D albedoMap;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	int r4;
	r0.x = dot(i.texcoord1.xyz, i.texcoord1.xyz);
	r0.x = 1 / sqrt(r0.x);
	r0.xyz = r0.xxx * i.texcoord1.xyz;
	r0.xy = r0.xy * parallax.xx;
	r0.z = max(abs(r0.z), 0.00100000005);
	r0.xy = r0.xy / r0.zz;
	r0.xy = r0.xy / parallax.yy;
	r0.zw = ddx(i.texcoord.xy);
	r1.xy = ddy(i.texcoord.xy);
	r2 = heightMap.SampleGrad(samp, i.texcoord.xy, r0.zwzz, r1.xyxx);
	r1.z = float1(1) / parallax.y;
	r2.yz = i.texcoord.xy;
	r1.w = 1;
	r3.x = r2.x;
	r2.w = 0;
	while (true) {
		r4 = (r2.w >= 16) ? -1 : 0;
		if (r4.x != 0) break;
		r4 = (r3.x >= r1.w) ? -1 : 0;
		if (r4.x != 0) {
			break;
		}
		r1.w = -(r1.z) + r1.w;
		r2.yz = -(r0.xy) + r2.yz;
		r3 = heightMap.SampleGrad(samp, r2.yz, r0.zwzz, r1.xyxx);
		r2.w = r2.w + 1;
	}
	r0 = albedoMap.Sample(samp, r2.yz);
	r1.w = saturate(r1.w);
	o = r0 * r1.w;

	return o;
}
