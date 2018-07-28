struct VS_OUT
{
	float4 position : POSITION;
	float4 position1 : POSITION1;
	float3 position2 : POSITION2;
	float3 position3 : POSITION3;
};

VS_OUT main() : POSITION
{
	VS_OUT o;

	o.position = 0;
	o.position1 = 0;
	o.position2 = 0;
	o.position3 = 0;

	return o;
}
