float4x4 m;
float3x3 n;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return mul(texcoord, m) + float4(mul(texcoord.xyz, n), 0);
}
