sampler2D shadowMap;
float4 shadowParameters;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = texcoord.z * rcp(texcoord.w) - (tex2Dproj(shadowMap, texcoord).x + shadowParameters.x) >= 0 ? shadowParameters.y : 1;
	return t0 * shadowParameters.z;
}
