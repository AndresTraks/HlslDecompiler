sampler2D s0;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	r0 = tex2D(s0, i.texcoord.xy);
	o.xy = r0.xx * i.texcoord.xy;
	o.zw = r0.xx * i.texcoord1.xy;

	return o;
}
