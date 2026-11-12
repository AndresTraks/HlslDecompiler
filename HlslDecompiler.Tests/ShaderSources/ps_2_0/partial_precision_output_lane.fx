float4 k;
float4 k2;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = tex2D(s0, texcoord);
	half3 t1 = t0.xyz;
	half4 t2 = float4(t1, t0.w) * k;
	return float4(t2.xyz * k2.xyz + t1, t2.w);
}
