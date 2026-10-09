uint count;

struct L
{
	float3 pos;
	float radius;
	float3 colour;
	float pad;
};

SamplerState pt;
StructuredBuffer<L> lights : register(t0);
Texture2D albedo : register(t1);
Texture2D normal;
Texture2D posTex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float3 r3;
	float4 r4;
	float3 r5;
	r0.xyz = albedo.Sample(pt, texcoord.xy).xyz;
	r1.xyz = normal.Sample(pt, texcoord.xy).xyz;
	r0.w = dot(r1.xyz, r1.xyz);
	r0.w = rsqrt(r0.w);
	r1.xyz = r0.www * r1.xyz;
	r2.xyz = posTex.Sample(pt, texcoord.xy).xyz;
	r3 = float3(0, 0, 0);
	r0.w = 0;
	while (true) {
		r1.w = ((uint)r0.w >= count) ? -1 : 0;
		if (asint(r1.w) != 0) break;
		r4 = float4(lights[r0.w].pos, lights[r0.w].radius);
		r5 = lights[r0.w].colour;
		r4.xyz = -(r2.xyz) + r4.xyz;
		r1.w = dot(r4.xyz, r4.xyz);
		r1.w = sqrt(r1.w);
		r4.xyz = r4.xyz / r1.www;
		r2.w = saturate(dot(r1.xyz, r4.xyz));
		r4.xyz = r2.www * r5.xyz;
		r1.w = r1.w / r4.w;
		r1.w = saturate(-(r1.w) + 1);
		r3 = r4.xyz * r1.www + r3.xyz;
		r0.w = r0.w + 1;
	}
	o.xyz = r0.xyz * r3.xyz;
	o.w = 1;

	return o;
}
