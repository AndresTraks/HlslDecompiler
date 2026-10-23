cbuffer C : register(b0)
{
	float2 texel;
	float threshold;
};

SamplerState lin;
Texture2D src;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	float4 r1;
	int3 r2;
	int2 r3;
	r0.xyz = asint(src.SampleLevel(lin, i.texcoord.xy, 0).xyz);
	r0.w = asint(dot(asfloat(r0.xyz), float3(0.298999995, 0.587000012, 0.114000000)));
	r1.xyz = src.SampleLevel(lin, i.texcoord.xy, 0, int2(0, -1)).xyz;
	r1.x = dot(r1.xyz, float3(0.298999995, 0.587000012, 0.114000000));
	r1.yzw = src.SampleLevel(lin, i.texcoord.xy, 0, int2(0, 1)).xyz;
	r1.y = dot(r1.yzw, float3(0.298999995, 0.587000012, 0.114000000));
	r2 = asint(src.SampleLevel(lin, i.texcoord.xy, 0, int2(1, 0)).xyz);
	r1.z = dot(asfloat(r2.xyz), float3(0.298999995, 0.587000012, 0.114000000));
	r2 = asint(src.SampleLevel(lin, i.texcoord.xy, 0, int2(-1, 0)).xyz);
	r1.w = dot(asfloat(r2.xyz), float3(0.298999995, 0.587000012, 0.114000000));
	r2.xy = asint(min(r1.yw, r1.xz));
	r2.x = asint(min(asfloat(r2.y), asfloat(r2.x)));
	r2.x = asint(min(asfloat(r0.w), asfloat(r2.x)));
	r2.yz = asint(max(r1.yw, r1.xz));
	r2.y = asint(max(asfloat(r2.z), asfloat(r2.y)));
	r2.y = asint(max(asfloat(r0.w), asfloat(r2.y)));
	r2.x = asint(-(asfloat(r2.x)) + asfloat(r2.y));
	r2.y = asint(asfloat(r2.y) * threshold);
	r2.x = (asfloat(r2.x) < asfloat(r2.y)) ? -1 : 0;
	if (r2.x != 0) {
		o.xyz = asfloat(r0.xyz);
		o.w = 1;
		return o;
	}
	r2.xy = asint(r1.yw + r1.xz);
	r2.x = asint(-(asfloat(r0.w)) * 2 + asfloat(r2.x));
	r2.y = asint(-(asfloat(r0.w)) * 2 + asfloat(r2.y));
	r2.x = (abs(asfloat(r2.x)) >= abs(asfloat(r2.y))) ? -1 : 0;
	r3.x = (r2.x != 0) ? 0 : asint(texel.x);
	r3.y = r2.x & asint(texel.y);
	r1.xyw = -(asfloat(r0.www)) + r1.xyw;
	r1.x = (r2.x != 0) ? abs(r1.x) : abs(r1.w);
	r0.w = asint(-(asfloat(r0.w)) + r1.z);
	r0.w = (r2.x != 0) ? asint(abs(r1.y)) : asint(abs(asfloat(r0.w)));
	r0.w = (r1.x >= asfloat(r0.w)) ? -1 : 0;
	r0.w = (r0.w != 0) ? -1090519040 : 1056964608;
	r1.xy = asfloat(r3.xy) * asfloat(r0.ww) + i.texcoord.xy;
	r1.xyz = src.SampleLevel(lin, r1.xy, 0).xyz;
	r1.xyz = -(asfloat(r0.xyz)) + r1.xyz;
	o.xyz = r1.xyz * float3(0.5, 0.5, 0.5) + asfloat(r0.xyz);
	o.w = 1;

	return o;
}
