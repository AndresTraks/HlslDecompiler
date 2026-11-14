min16float4 c;
int n;

float4 main(min16float4 color : COLOR) : SV_Target
{
	float4 o;

	float4 r0;
	int2 r1;
	r0 = color;
	r1.x = 0;
	while (true) {
		r1.y = (r1.x >= n) ? -1 : 0;
		if (r1.y != 0) break;
		r0 = r0 * c + color;
		r1.x = r1.x + 1;
	}
	o = r0;

	return o;
}
