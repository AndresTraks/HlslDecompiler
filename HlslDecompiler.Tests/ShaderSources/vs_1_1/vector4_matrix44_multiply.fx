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

	o.position = mul(position, matrix_4x4);
	o.texcoord1 = mul(position.yxzw, matrix_4x4);
	o.texcoord2 = mul(abs(position.yxzw), matrix_4x4);
	o.texcoord3 = mul(float4(5, 2, 3, 4) * position, matrix_4x4);
	o.texcoord4 = mul(float4(position.xyz, 1), matrix_4x4);

	return o;
}
