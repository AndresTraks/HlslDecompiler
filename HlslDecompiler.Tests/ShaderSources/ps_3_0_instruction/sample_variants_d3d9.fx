float4 k;
sampler2D tex;

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0.xy = ddx(i.texcoord1.xy);
	r0.zw = ddy(i.texcoord1.xy);
	r0 = r0 * k.yyzz;
	r0 = tex2Dgrad(tex, i.texcoord1.xy, r0.xy, r0.zw);
	r1.xyz = float3(1, 1, 0) * i.texcoord1.xyx;
	r1.w = k.x;
	r1 = tex2Dbias(tex, r1);
	r2 = tex2Dproj(tex, i.texcoord);
	r1 = r2 * k.w + r1;
	o = r0 + r1;

	return o;
}
