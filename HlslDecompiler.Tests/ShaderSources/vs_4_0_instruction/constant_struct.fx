struct VS_OUT
{
	float4 position : POSITION;
	float4 position1 : POSITION1;
	float3 position2 : POSITION2;
	float3 position3 : POSITION3;
};

VS_OUT main()
{
	VS_OUT o;

	o.position = float4(0, 0, 0, 0);
	o.position1 = float4(0, 0, 0, 0);
	o.position2 = float3(0, 0, 0);
	o.position3 = float3(0, 0, 0);

	return o;
}
