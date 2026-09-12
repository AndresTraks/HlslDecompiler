float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float t0 = (((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) * ((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) + (texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) * (((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) * ((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) + (texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) + ((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) * ((texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x) + (texcoord.y + texcoord.x) * (texcoord.y + texcoord.x) + texcoord.y + texcoord.x;
	float t1 = t0 * t0 + t0;
	float t2 = t1 * t1 + t1;
	return t2 * t2 + t2;
}
