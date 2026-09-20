float4 lightPosition;
float4 lightColour;

SamplerState samp;
Texture2D gbuffer0;
Texture2D<uint4> gbuffer1;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	float3 r2;
	r0.xy = (int2)i.sv_position.xy;
	r0.zw = int2(0, 0);
	r0 = gbuffer1.Load(r0.xyz);
	r0.z = r0.x & 65535;
	r1.x = asint((float)(uint)r0.z);
	r0.x = (uint)r0.x >> 16;
	r0.yzw = asint(-(i.texcoord1.xyz) * asfloat(r0.yyy) + lightPosition.xyz);
	r1.y = asint((float)(uint)r0.x);
	r1.xy = asint(asfloat(r1.xy) * float2(0.0000305180438, 0.0000305180438) + float2(-1, -1));
	r0.x = asint(-(abs(asfloat(r1.x))) + 1);
	r2.z = -(abs(asfloat(r1.y))) + asfloat(r0.x);
	r0.x = asint(max(-(r2.z), 0));
	r1.zw = (asfloat(r1.xy) >= float2(0, 0)) ? -1 : 0;
	r1.zw = (r1.zw != 0) ? asint(-(asfloat(r0.xx))) : r0.xx;
	r2.xy = asfloat(r1.zw) + asfloat(r1.xy);
	r0.x = asint(dot(r2.xyz, r2.xyz));
	r0.x = asint(rsqrt(asfloat(r0.x)));
	r1.xyz = asint(asfloat(r0.xxx) * r2.xyz);
	r0.x = asint(dot(asfloat(r0.yzw), asfloat(r0.yzw)));
	r1.w = asint(rsqrt(asfloat(r0.x)));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.x = asint(asfloat(r0.x) / lightPosition.w);
	r0.x = asint(saturate(-(asfloat(r0.x)) + 1));
	r0.x = asint(asfloat(r0.x) * asfloat(r0.x));
	r0.yzw = asint(asfloat(r0.yzw) * asfloat(r1.www));
	r0.y = asint(saturate(dot(asfloat(r1.xyz), asfloat(r0.yzw))));
	r0.x = asint(asfloat(r0.y) * asfloat(r0.x));
	r1 = asint(gbuffer0.Sample(samp, i.texcoord.xy));
	r0.yzw = asint(asfloat(r1.xyz) * lightColour.xyz);
	o.xyz = asfloat(r0.xxx) * asfloat(r0.yzw);
	o.w = 1;

	return o;
}
