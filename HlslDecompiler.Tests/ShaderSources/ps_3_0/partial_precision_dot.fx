float4 k;
float4 k2;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half3 t0 = tex2D(s0, texcoord).xyz;
	half t1 = dot(k.xyz, t0);
	return t1 * k2;
}
