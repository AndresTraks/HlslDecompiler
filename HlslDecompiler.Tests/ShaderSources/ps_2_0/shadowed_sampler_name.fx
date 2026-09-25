sampler2D t0;
sampler2D t1;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 t_0 = tex2D(t0, i.texcoord);
	return t_0 * tex2D(t1, t_0.xy + i.texcoord1);
}
