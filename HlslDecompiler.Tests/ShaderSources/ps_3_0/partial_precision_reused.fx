float4 k;
sampler2D tex;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = tex2D(tex, texcoord);
	half3 t1 = t0.wyx;
	half3 t2 = t1 * (half3)(t1 * k.wyx);
	return t2.xxxy + float4(t2.zy, (half)(t0.z * (half)(t0.z * k.z)), t2.z);
}
