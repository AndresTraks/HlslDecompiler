float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return (float4)sign(texcoord) * length(texcoord.xy - k.xy) + fmod(texcoord, k.w) + (-2 * saturate((texcoord - k.x) / (k.y - k.x)) + 3) * saturate((texcoord - k.x) / (k.y - k.x)) * saturate((texcoord - k.x) / (k.y - k.x)) + min(max(texcoord, -k.z), k.z);
}
