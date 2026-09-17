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

	float4 r0;
	float4 r1;
	float3 r2;
	r0.xy = asfloat((int2)i.sv_position.xy);
	r0.zw = asfloat(int2(0, 0));
	r0 = asfloat(gbuffer1.Load(asint(r0.xyz)));
	r0.z = asfloat(asint(r0.x) & 65535);
	r1.x = (float)asint(r0.z);
	r0.x = asfloat((uint)asint(r0.x) >> 16);
	r0.yzw = -(i.texcoord1.xyz) * r0.yyy + lightPosition.xyz;
	r1.y = (float)asint(r0.x);
	r1.xy = r1.xy * float2(0.0000305180438, 0.0000305180438) + float2(-1, -1);
	r0.x = -(abs(r1.x)) + 1;
	r2.z = -(abs(r1.y)) + r0.x;
	r0.x = max(-(r2.z), 0);
	r1.zw = asfloat((r1.xy >= float2(0, 0)) ? -1 : 0);
	r1.zw = (asint(r1.zw) != 0) ? -(r0.xx) : r0.xx;
	r2.xy = r1.zw + r1.xy;
	r0.x = dot(r2.xyz, r2.xyz);
	r0.x = 1 / sqrt(r0.x);
	r1.xyz = r0.xxx * r2.xyz;
	r0.x = dot(r0.yzw, r0.yzw);
	r1.w = 1 / sqrt(r0.x);
	r0.x = sqrt(r0.x);
	r0.x = r0.x / lightPosition.w;
	r0.x = saturate(-(r0.x) + 1);
	r0.x = r0.x * r0.x;
	r0.yzw = r0.yzw * r1.www;
	r0.y = saturate(dot(r1.xyz, r0.yzw));
	r0.x = r0.y * r0.x;
	r1 = gbuffer0.Sample(samp, i.texcoord.xy);
	r0.yzw = r1.xyz * lightColour.xyz;
	o.xyz = r0.xxx * r0.yzw;
	o.w = 1;

	return o;
}
