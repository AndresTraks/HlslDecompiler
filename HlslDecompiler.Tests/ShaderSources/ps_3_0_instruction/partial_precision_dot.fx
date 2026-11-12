float4 k;
float4 k2;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	half4 r0;
	r0 = tex2D(s0, texcoord.xy);
	r0.x = dot(r0.xyz, k.xyz);
	o = r0.x * k2;

	return o;
}
