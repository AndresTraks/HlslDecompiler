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

	o.position = i.position > 0.5 ? i.normal : float4(1, 2, 3, 4);
	o.normal = i.normal > 0.5 ? i.tangent : i.position;

	return o;
}
