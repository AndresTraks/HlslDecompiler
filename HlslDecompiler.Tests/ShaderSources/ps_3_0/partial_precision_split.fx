float4 color;
sampler2D diffuse;
float4 light;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = tex2D(diffuse, texcoord.zw);
	half4 t1 = t0 * color;
	half3 t2 = t1.xyz;
	return float4(t2 * light.xyz + t0.w * t2, t1.w * light.w);
}
