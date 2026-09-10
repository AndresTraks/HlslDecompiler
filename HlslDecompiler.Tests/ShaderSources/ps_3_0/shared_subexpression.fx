float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float t0 = (((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) * ((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) + (texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) * (((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) * ((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) + (texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) + ((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) * ((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) + (texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x;
	return ((t0 * t0 + t0) * (t0 * t0 + t0) + t0 * t0 + t0) * ((t0 * t0 + t0) * (t0 * t0 + t0) + t0 * t0 + t0) + (t0 * t0 + t0) * (t0 * t0 + t0) + t0 * t0 + t0;
}
