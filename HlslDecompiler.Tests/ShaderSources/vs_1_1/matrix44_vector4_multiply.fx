float4x4 matrix_4x4;

struct VS_OUT
{
	float4 position : POSITION;
	float4 texcoord1 : TEXCOORD1;
	float4 texcoord2 : TEXCOORD2;
	float4 texcoord3 : TEXCOORD3;
	float4 texcoord4 : TEXCOORD4;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = mul(matrix_4x4, position);
	o.texcoord1 = mul(matrix_4x4, position.yxzw);
	o.texcoord2 = mul(matrix_4x4, abs(position.yxzw));
	o.texcoord3 = mul(matrix_4x4, float4(5, 2, 3, 4) * position);
	o.texcoord4 = mul(matrix_4x4, float4(position.xyz, 1));

	return o;
}
