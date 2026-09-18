sampler2D shadowMap;
float4 shadowParameters;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	r0 = tex2Dproj(shadowMap, texcoord);
	r0.x = r0.x + shadowParameters.x;
	r0.y = 1 / texcoord.w;
	r0.x = texcoord.z * r0.y + -r0.x;
	r0.y = shadowParameters.y;
	r0.x = (r0.x >= 0) ? r0.y : 1;
	r0 = r0.x * shadowParameters.z;
	o = r0;

	return o;
}
