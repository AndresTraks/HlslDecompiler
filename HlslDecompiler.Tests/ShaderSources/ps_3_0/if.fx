float count;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 r0 = float4(1, 2, 3, 4);
	float4 r1 = float4(2, 3, 4, 5);
	float check = max(count, texcoord.x);
	if (check > texcoord.y)
	{
		r0 += abs(texcoord);
		r1 *= texcoord;
	}
	return r0 + r1;
}
