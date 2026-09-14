float3x2 matrix_3x2;

struct VS_OUT
{
	float4 position : POSITION;
	float2 texcoord1 : TEXCOORD1;
	float2 texcoord2 : TEXCOORD2;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = float4(mul(position.xy, (float2x2)matrix_3x2), mul(position.yx, (float2x2)matrix_3x2));
	o.texcoord1 = mul(abs(position.yx), (float2x2)matrix_3x2);
	o.texcoord2 = mul(2 * position.xy, (float2x2)matrix_3x2);

	return o;
}
