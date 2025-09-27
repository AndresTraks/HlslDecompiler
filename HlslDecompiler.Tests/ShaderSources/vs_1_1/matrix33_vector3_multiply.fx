float3x3 matrix_3x3;

struct VS_OUT
{
	float4 position : POSITION;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float3 texcoord3 : TEXCOORD3;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = float4(mul(matrix_3x3, position.xyz), 1);
	o.texcoord1 = mul(matrix_3x3, position.yxz);
	o.texcoord2 = mul(matrix_3x3, abs(position.yxz));
	o.texcoord3 = mul(matrix_3x3, float3(2 * position.x, position.y, 3 * position.z));

	return o;
}
