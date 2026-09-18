sampler2D shadowMap;
float4 shadowParameters;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return (texcoord.z * rcp(texcoord.w) - (tex2Dproj(shadowMap, texcoord).x + shadowParameters.x) >= 0 ? shadowParameters.yyyy : 1) * shadowParameters.z;
}
