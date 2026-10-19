cbuffer Parallax : register(b0)
{
	float heightScale;
	uint stepCount;
};

SamplerState samp;
Texture2D heightMap;
Texture2D albedo;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float3 viewtangent : VIEWTANGENT;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	int3 r2;
	r0.x = (float)(uint)stepCount;
	r0.x = 1 / r0.x;
	r0.y = max(abs(i.viewtangent.z), 0.0000999999975);
	r0.yz = i.viewtangent.xy / r0.yy;
	r0.yz = r0.yz * heightScale;
	r1.xy = asint(i.texcoord.xy);
	r0.w = 1;
	r1.zw = int2(1065353216, 1065353216);
	r2.x = 0;
	while (true) {
		r2.y = ((uint)r2.x >= stepCount) ? -1 : 0;
		if (r2.y != 0) break;
		r2.y = asint(heightMap.SampleLevel(samp, asfloat(r1.xy), 0).x);
		r2.z = (asfloat(r2.y) >= r0.w) ? -1 : 0;
		if (r2.z != 0) {
			r1.xyw = r1.xyz;
			r1.z = r2.y;
			break;
		}
		r1.xy = asint(-(r0.yz) * r0.xx + asfloat(r1.xy));
		r0.w = -(r0.x) + r0.w;
		r2.x = r2.x + 1;
		r1.w = r1.z;
		r1.z = r2.y;
	}
	r1.z = asint(-(r0.w) + asfloat(r1.z));
	r0.w = r0.x + r0.w;
	r0.w = -(r0.w) + asfloat(r1.w);
	r0.w = -(r0.w) + asfloat(r1.z);
	r0.w = max(r0.w, 0.00000999999975);
	r0.w = asfloat(r1.z) / r0.w;
	r0.xy = r0.yz * r0.xx + asfloat(r1.xy);
	r1.xy = asint(-(r0.xy) + asfloat(r1.xy));
	r0.xy = r0.ww * asfloat(r1.xy) + r0.xy;
	o = albedo.Sample(samp, r0.xy);

	return o;
}
