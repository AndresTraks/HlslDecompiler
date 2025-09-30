struct VS_IN
{
	float4 position : POSITION;
	float4 position1 : POSITION1;
	float4 position2 : POSITION2;
};

float4 main(VS_IN i) : POSITION
{
	return float4(length(i.position), length(i.position1.xyz), -length(i.position2.xy), -5 * length(i.position2 + 2));
}
