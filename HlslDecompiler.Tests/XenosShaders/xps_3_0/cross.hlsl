float3 k;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4(-texcoord.zxy * k.yzx + reflect(texcoord, k) + texcoord.yzx * k.zxy, 1);
}
