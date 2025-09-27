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

	o.position = float4(mul(position.xyz, matrix_3x3), 1);
	o.texcoord1 = mul(position.yxz, matrix_3x3);
	o.texcoord2 = mul(abs(position.yxz), matrix_3x3);
	o.texcoord3 = mul(float3(position.y, float2(2, 3) * position.xz), matrix_3x3);

	return o;
}
