sampler2D s0;
sampler2D s1;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float4 t0 = tex2D(s0, i.texcoord);
	return float4(2 * t0.xyz * tex2D(s1, i.texcoord1).xyz, t0.w) * i.color;
}
