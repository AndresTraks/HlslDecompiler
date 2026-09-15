float4 offsets[4];
uint count;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 x0[4];

	x0[0] = texcoord;
	x0[1] = texcoord.yzwx + offsets[1];
	x0[2] = texcoord * offsets[2];
	x0[3] = offsets[3] - texcoord;
	float4 t0 = 0;
	[loop]
	for (int t1 = 0; t1 < count; t1 = t1 + 1) {
		t0 = t0 + x0[t1 & 3];
	}
	return t0;
}
