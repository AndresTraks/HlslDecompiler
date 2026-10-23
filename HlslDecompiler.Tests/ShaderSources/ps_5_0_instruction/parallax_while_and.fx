cbuffer C : register(b0)
{
	float scale;
	int minSteps;
	int maxSteps;
};

SamplerState s;
Texture2D albedo;
Texture2D<float> height;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float3 view : VIEW;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	int4 r2;
	r0.x = dot(i.view.xyz, i.view.xyz);
	r0.x = rsqrt(r0.x);
	r0.xyz = r0.xxx * i.view.xyz;
	r1.xy = (float2)float2(maxSteps, minSteps);
	r0.w = -(r1.x) + r1.y;
	r0.w = abs(r0.z) * r0.w + r1.x;
	r0.w = trunc(r0.w);
	r0.w = 1 / r0.w;
	r0.xy = r0.xy / r0.zz;
	r0.xy = r0.xy * scale;
	r1.xy = r0.ww * r0.xy;
	r0.z = height.SampleLevel(s, i.texcoord.xy, 0).x;
	r1.zw = i.texcoord.xy;
	r2.x = 0;
	r2.y = asint(r0.z);
	while (true) {
		r2.z = (asfloat(r2.x) < asfloat(r2.y)) ? -1 : 0;
		r2.w = (asfloat(r2.x) < 1) ? -1 : 0;
		r2.z = r2.w & r2.z;
		if (r2.z == 0) break;
		r1.zw = -(r0.xy) * r0.ww + r1.zw;
		r2.x = asint(r0.w + asfloat(r2.x));
		r2.y = asint(height.SampleLevel(s, r1.zw, 0).x);
	}
	r0.xy = r0.xy * r0.ww + r1.zw;
	r0.z = -(asfloat(r2.x)) + asfloat(r2.y);
	r0.x = height.SampleLevel(s, r0.xy, 0).x;
	r0.x = -(asfloat(r2.x)) + r0.x;
	r0.x = r0.w + r0.x;
	r0.x = -(r0.x) + r0.z;
	r0.x = r0.z / r0.x;
	r0.xy = r0.xx * r1.xy + r1.zw;
	o = albedo.Sample(s, r0.xy);

	return o;
}
