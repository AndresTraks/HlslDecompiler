float2 resolution;

Texture2D sceneTex;
Texture2D depthTex;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	int r1;
	float4 r2;
	float4 r3;
	r0.yz = i.sv_position.xy / resolution.xy;
	r1 = asint(r0.y * 8);
	r1 = asint(frac(asfloat(r1.x)));
	r1 = (asfloat(r1.x) < 0.5) ? -1 : 0;
	r1 = r1.x & 1065353216;
	r2.xy = (int2)i.sv_position.xy;
	r2.zw = int2(0, 0);
	r3 = r2 + int4(1, 0, 0, 0);
	r2 = sceneTex.Load(r2.xyw);
	r3 = depthTex.Load(r3.xyz);
	r0.x = r3.x;
	r0.w = i.texcoord.x;
	o = r2 * asfloat(r1.x) + r0;

	return o;
}
