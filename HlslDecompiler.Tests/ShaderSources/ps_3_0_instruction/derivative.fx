float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	o.x = ddx(texcoord.x);
	o.y = ddy(texcoord.y);
	o.zw = float2(1, 0);

	return o;
}
