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

	float4 r0;
	float4 r1;
	float3 r2;
	r0.xyz = position.yyy * transpose(matrix_3x3)[1].xyz;
	r1.xyz = transpose(matrix_3x3)[0].xyz * position.xxx + r0.xyz;
	o.position.xyz = transpose(matrix_3x3)[2].xyz * position.zzz + r1.xyz;
	r1.xyz = position.xxx * transpose(matrix_3x3)[1].xyz;
	r1.xyz = transpose(matrix_3x3)[0].xyz * position.yyy + r1.xyz;
	o.texcoord1 = transpose(matrix_3x3)[2].xyz * position.zzz + r1.xyz;
	r1.xyz = max(-position.yxz, position.yxz);
	r2 = r1.yyy * transpose(matrix_3x3)[1].xyz;
	r1.xyw = transpose(matrix_3x3)[0].xyz * r1.xxx + r2.xyz;
	o.texcoord2 = transpose(matrix_3x3)[2].xyz * r1.zzz + r1.xyw;
	r0.w = position.x + position.x;
	r0.xyz = transpose(matrix_3x3)[0].xyz * r0.www + r0.xyz;
	r0.w = position.z * 3;
	o.texcoord3 = transpose(matrix_3x3)[2].xyz * r0.www + r0.xyz;
	o.position.w = 1;

	return o;
}
