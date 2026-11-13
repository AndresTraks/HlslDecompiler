sampler2D detail : register(s1);
sampler2D offsetMap;
float4 tint;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half3 t0 = tex2D(offsetMap, texcoord).xyw;
	float4 t1 = tex2Dbias(detail, half4(t0.xy, 0, t0.z));
	return t1 * tint;
}
