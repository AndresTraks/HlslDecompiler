float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return (float4)((texcoord < 0 ? -1 : 0) - (texcoord > 0 ? -1 : 0)) * length(texcoord.xy - k.xy) + (texcoord / k.w >= -texcoord / k.w ? frac(abs(texcoord / k.w)) : -frac(abs(texcoord / k.w))) * k.w + (-2 * saturate((texcoord - k.x) / (k.y - k.x)) + 3) * saturate((texcoord - k.x) / (k.y - k.x)) * saturate((texcoord - k.x) / (k.y - k.x)) + min(max(texcoord, -k.z), k.z);
}
