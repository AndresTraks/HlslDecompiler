sampler2D detail : register(s1);
sampler2D offsetMap;
float4 tint;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	r0 = half4(tex2D(offsetMap, texcoord.xy));
	r0 = half4(r0.xyxw * float4(1, 1, 0, 1));
	r0 = tex2Dbias(detail, r0);
	o = r0 * tint;

	return o;
}
