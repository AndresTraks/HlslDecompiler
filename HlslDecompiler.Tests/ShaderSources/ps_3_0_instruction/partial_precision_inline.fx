float4 color;
sampler2D diffuse;
float4 light;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	half4 r0;
	half4 r1;
	r0 = tex2D(diffuse, texcoord.zw);
	r1 = r0 * color;
	o.xyz = r1.xyz * light.xyz + r0.xyz;
	o.w = half(r1.w);

	return o;
}
