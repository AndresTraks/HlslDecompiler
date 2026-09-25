sampler2D t0;
sampler2D t1;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tex2D(t0, i.texcoord.xy);
	r1.xy = r0.xy + i.texcoord1.xy;
	r1 = tex2D(t1, r1.xy);
	r0 = r0 * r1;
	o = r0;

	return o;
}
