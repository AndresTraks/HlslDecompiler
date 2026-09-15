float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float t0 = texcoord.y + texcoord.x;
	float t1 = t0 * t0 + t0;
	float t2 = t1 * t1 + t1;
	float t3 = t2 * t2 + t2;
	float t4 = t3 * t3 + t3;
	float t5 = t4 * t4 + t4;
	return t5 * t5 + t5;
}
