float4 p;
sampler2D t;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float vface : VFACE;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float2 r1;
	r0.x = (i.vface > 0) ? 1 : -1;
	if (-r0.x < 0) {
		r0.xyz = float3(1, 1, 0) * i.texcoord.xyx;
		r0.w = p.x;
		o = tex2Dlod(t, r0);
	} else {
		r0.xy = i.texcoord.xy + i.texcoord.xy;
		r0.zw = ddx(i.texcoord.xy);
		r1 = ddy(i.texcoord.xy);
		r0 = tex2Dgrad(t, r0.xy, r0.zw, r1.xy);
		o = r0.zyxw;
	}

	return o;
}
