float4 k;
sampler2D tex;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = tex2D(tex, texcoord);
	half4 t1 = t0 * (half4)(t0 * k);
	return t1.wwwy + t1.xyzx;
}
