float4 cutoff;
sampler2D diffuse;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tex2D(diffuse, texcoord.xy);
	r1 = r0.w + -cutoff.x;
	r0 = r0 * cutoff.y;
	o = r0;
	clip(r1);

	return o;
}
