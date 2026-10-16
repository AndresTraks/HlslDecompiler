struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float r0;
	if (i.idx == 0) {
		o.yzw = i.texcoord.yzw;
		r0 = i.texcoord.x;
	} else {
		o.yzw = -(i.texcoord.yzw);
		r0 = -(i.texcoord.x);
	}
	o.x = r0.x + i.texcoord.y;

	return o;
}
