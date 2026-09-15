float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return float4(texcoord.x / (texcoord.y * k.x), texcoord.x / (texcoord.y / k.y), texcoord.z / texcoord.w * k.z, texcoord.y * texcoord.x / (texcoord.w * texcoord.z));
}
