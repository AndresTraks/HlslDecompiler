struct VS_OUT
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = float4(float3(2, 11, 23) * position.x + float3(3, 13, 29) * position.y, 97 * position.x) + float4(5, 17, 31, 101) * position.zzzy + float4(7, 19, 37, 103) * position.wwwz;
	o.texcoord = float4(109, 137, 41, 59) * position.x + float4(113, 139, 43, 61) * position.y + float4(127, 149, 47, 67) * position.z;

	return o;
}
