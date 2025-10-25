float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	o.x = ddx(texcoord.x);
	o.y = ddy(texcoord.y);
	o.zw = float2(0, 0);

	return o;
}
