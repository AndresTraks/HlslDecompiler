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
	float4 r1;
	int3 r2;
	r0.x = (float)(uint)stepCount;
	r0.x = 1 / r0.x;
	r0.y = max(abs(i.viewtangent.z), 0.0000999999975);
	r0.yz = i.viewtangent.xy / r0.yy;
	r0.yz = r0.yz * heightScale;
	r1.xy = i.texcoord.xy;
	r0.w = 1;
	r1.zw = float2(1, 1);
	r2.x = 0;
	while (true) {
		r2.y = ((uint)r2.x >= stepCount) ? -1 : 0;
		if (r2.y != 0) break;
		r2.y = asint(heightMap.SampleLevel(samp, r1.xy, 0).x);
		r2.z = (asfloat(r2.y) >= r0.w) ? -1 : 0;
		if (r2.z != 0) {
			r1.xyw = r1.xyz;
			r1.z = asfloat(r2.y);
			break;
		}
		r1.xy = -(r0.yz) * r0.xx + r1.xy;
		r0.w = -(r0.x) + r0.w;
		r2.x = r2.x + 1;
		r1.w = r1.z;
		r1.z = asfloat(r2.y);
	}
	r1.z = -(r0.w) + r1.z;
	r0.w = r0.x + r0.w;
	r0.w = -(r0.w) + r1.w;
	r0.w = -(r0.w) + r1.z;
	r0.w = max(r0.w, 0.00000999999975);
	r0.w = r1.z / r0.w;
	r0.xy = r0.yz * r0.xx + r1.xy;
	r1.xy = -(r0.xy) + r1.xy;
	r0.xy = r0.ww * r1.xy + r0.xy;
	o = albedo.Sample(samp, r0.xy);

	return o;
}
