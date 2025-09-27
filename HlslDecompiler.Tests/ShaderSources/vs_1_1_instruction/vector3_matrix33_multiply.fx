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

	float3 r0;
	o.position.x = dot(position.xyz, transpose(matrix_3x3)[0].xyz);
	o.position.y = dot(position.xyz, transpose(matrix_3x3)[1].xyz);
	o.position.z = dot(position.xyz, transpose(matrix_3x3)[2].xyz);
	o.texcoord1.x = dot(position.yxz, transpose(matrix_3x3)[0].xyz);
	o.texcoord1.y = dot(position.yxz, transpose(matrix_3x3)[1].xyz);
	o.texcoord1.z = dot(position.yxz, transpose(matrix_3x3)[2].xyz);
	r0 = max(-position.yxz, position.yxz);
	o.texcoord2.x = dot(r0.xyz, transpose(matrix_3x3)[0].xyz);
	o.texcoord2.y = dot(r0.xyz, transpose(matrix_3x3)[1].xyz);
	o.texcoord2.z = dot(r0.xyz, transpose(matrix_3x3)[2].xyz);
	r0 = position.yxz * float3(1, 2, 3);
	o.texcoord3.x = dot(r0.xyz, transpose(matrix_3x3)[0].xyz);
	o.texcoord3.y = dot(r0.xyz, transpose(matrix_3x3)[1].xyz);
	o.texcoord3.z = dot(r0.xyz, transpose(matrix_3x3)[2].xyz);
	o.position.w = 1;

	return o;
}
