float4 k;
float4 k2;
float4 k3;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tex2D(s0, texcoord.xy);
	r1 = k;
	r0 = half4(saturate(r0 * r1 + k2));
	o = r0 * k3;

	return o;
}
