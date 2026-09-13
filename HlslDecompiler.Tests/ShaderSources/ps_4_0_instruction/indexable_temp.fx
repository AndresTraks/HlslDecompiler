uint selector;

float4 main(nointerpolation uint4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int2 r0;
	int4 x0[4];
	x0[0].x = texcoord.x;
	r0.x = texcoord.y << 1;
	x0[1].x = r0.x;
	r0.x = texcoord.z + 1;
	x0[2].x = r0.x;
	x0[3].x = texcoord.w;
	r0.x = texcoord.x + selector;
	r0.x = r0.x & 3;
	r0.y = x0[r0.x].x;
	r0.y = r0.y + 5;
	x0[r0.x].x = r0.y;
	o.xw = (float2)(int2)r0.yx;
	r0.y = r0.x + 1;
	r0.x = r0.y & 3;
	r0.x = x0[r0.x].x;
	r0.y = x0[3].x;
	o.yz = (float2)(int2)r0.yx;

	return o;
}
