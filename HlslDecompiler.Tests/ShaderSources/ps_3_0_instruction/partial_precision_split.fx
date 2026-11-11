float4 color;
sampler2D diffuse;
float4 light;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	half4 r1;
	r0 = half4(tex2D(diffuse, texcoord.zw));
	r1 = r0 * color;
	r0.xyz = r0.www * r1.xyz;
	o.xyz = r1.xyz * light.xyz + r0.xyz;
	o.w = r1.w * light.w;

	return o;
}
