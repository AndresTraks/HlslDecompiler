float4 k;
sampler2D s0;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tex2D(s0, i.texcoord.xy);
	r1 = lerp(r0, i.color, k.x);
	r0 = saturate(r0 + -i.color);
	r0 = r1 * k + r0;
	o = r0;

	return o;
}
