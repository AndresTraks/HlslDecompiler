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

	float4 r0;
	o.position.x = dot(position, transpose(matrix_4x4)[0]);
	o.position.y = dot(position, transpose(matrix_4x4)[1]);
	o.position.z = dot(position, transpose(matrix_4x4)[2]);
	o.position.w = dot(position, transpose(matrix_4x4)[3]);
	o.texcoord1.x = dot(position.yxzw, transpose(matrix_4x4)[0]);
	o.texcoord1.y = dot(position.yxzw, transpose(matrix_4x4)[1]);
	o.texcoord1.z = dot(position.yxzw, transpose(matrix_4x4)[2]);
	o.texcoord1.w = dot(position.yxzw, transpose(matrix_4x4)[3]);
	r0 = max(-position.yxzw, position.yxzw);
	o.texcoord2.x = dot(r0, transpose(matrix_4x4)[0]);
	o.texcoord2.y = dot(r0, transpose(matrix_4x4)[1]);
	o.texcoord2.z = dot(r0, transpose(matrix_4x4)[2]);
	o.texcoord2.w = dot(r0, transpose(matrix_4x4)[3]);
	r0 = position * float4(5, 2, 3, 4);
	o.texcoord3.x = dot(r0, transpose(matrix_4x4)[0]);
	o.texcoord3.y = dot(r0, transpose(matrix_4x4)[1]);
	o.texcoord3.z = dot(r0, transpose(matrix_4x4)[2]);
	o.texcoord3.w = dot(r0, transpose(matrix_4x4)[3]);
	r0 = position.xyzx * float4(1, 1, 1, 0) + float4(0, 0, 0, 1);
	o.texcoord4.x = dot(r0, transpose(matrix_4x4)[0]);
	o.texcoord4.y = dot(r0, transpose(matrix_4x4)[1]);
	o.texcoord4.z = dot(r0, transpose(matrix_4x4)[2]);
	o.texcoord4.w = dot(r0, transpose(matrix_4x4)[3]);

	return o;
}
