float count;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0 = float4(1, 2, 3, 4) + abs(texcoord);
	r1.x = max(count.x, texcoord.x);
	r1.x = -r1.x + texcoord.y;
	r0 = (r1.x >= 0) ? float4(1, 2, 3, 4) : r0;
	r2 = float4(2, 3, 4, 5) * texcoord;
	r1 = (r1.x >= 0) ? float4(2, 3, 4, 5) : r2;
	o = r0 + r1;

	return o;
}
