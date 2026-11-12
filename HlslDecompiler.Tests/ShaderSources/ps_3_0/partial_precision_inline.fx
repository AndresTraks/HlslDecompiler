float4 color;
sampler2D diffuse;
float4 light;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = tex2D(diffuse, texcoord.zw);
	half3 t1 = t0.xyz;
	half4 t2 = float4(t1, t0.w) * color;
	return float4(t2.xyz * light.xyz + t1, (half)t2.w);
}
