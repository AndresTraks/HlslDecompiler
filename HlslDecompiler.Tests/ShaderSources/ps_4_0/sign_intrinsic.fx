float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return (float4)sign(texcoord) * length(texcoord.xy - k.xy) + fmod(texcoord, k.w) + smoothstep(k.x, k.y, texcoord) + clamp(texcoord, -k.z, k.z);
}
