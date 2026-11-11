float4 k;
sampler2D tex;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = tex2D(tex, texcoord);
	half3 t1 = t0.wyx;
	half4 t2 = float4(t1.zy, t0.z, t1.x) * k;
	half3 t3 = t1 * t2.wyx;
	return t3.xxxy + half4(t3.zy, t0.z * t2.z, t3.z);
}
