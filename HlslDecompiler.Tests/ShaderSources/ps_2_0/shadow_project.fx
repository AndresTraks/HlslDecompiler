sampler2D shadowMap;
float4 shadowParameters;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = tex2Dproj(shadowMap, texcoord).x;
	float t1 = texcoord.z / texcoord.w - (t0 + shadowParameters.x) >= 0 ? shadowParameters.y : 1;
	return t1 * shadowParameters.z;
}
