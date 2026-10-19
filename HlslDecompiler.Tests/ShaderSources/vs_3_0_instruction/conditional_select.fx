struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
	float4 tangent : TANGENT;
};

struct VS_OUT
{
	float4 position : POSITION;
	float4 normal : NORMAL;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	o.position = (i.position > 0.5) ? i.normal : float4(1, 2, 3, 4);
	r0 = (i.normal > 0.5) ? i.tangent : i.position;
	o.normal = r0;

	return o;
}
