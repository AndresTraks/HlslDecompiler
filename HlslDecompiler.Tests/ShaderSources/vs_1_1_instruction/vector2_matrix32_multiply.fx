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

	float4 r0;
	r0 = position.xyyx * transpose(matrix_3x2)[0].xyxy;
	o.position.xz = r0.yw + r0.xz;
	r0 = position.xyyx * transpose(matrix_3x2)[1].xyxy;
	o.position.yw = r0.yw + r0.xz;
	r0.xy = max(-position.yx, position.yx);
	r0.zw = r0.xy * transpose(matrix_3x2)[0].xy;
	r0.xy = r0.xy * transpose(matrix_3x2)[1].xy;
	o.texcoord1 = r0.wy + r0.zx;
	r0.xy = position.xy + position.xy;
	r0.zw = r0.xy * transpose(matrix_3x2)[0].xy;
	r0.xy = r0.xy * transpose(matrix_3x2)[1].xy;
	o.texcoord2.y = r0.y + r0.x;
	o.texcoord2.x = r0.w + r0.z;

	return o;
}
