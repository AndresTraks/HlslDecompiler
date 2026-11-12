float4 k;
float4 k2;
float4 k3;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = saturate(tex2D(s0, texcoord) * k + k2);
	return t0 * k3;
}
