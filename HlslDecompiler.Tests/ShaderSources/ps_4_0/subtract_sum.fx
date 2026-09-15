float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return float4(texcoord.x - (texcoord.y + k.z), texcoord.x - (texcoord.y - k.z), texcoord.y - k.w + texcoord.x, k.x * texcoord.x - dot(k.yz, texcoord.yz));
}
