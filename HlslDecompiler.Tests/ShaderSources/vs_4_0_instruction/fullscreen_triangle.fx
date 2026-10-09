struct VS_OUT
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

VS_OUT main(uint sv_vertexid : SV_VertexID)
{
	VS_OUT o;

	int3 r0;
	r0.x = sv_vertexid << 1;
	r0.x = r0.x & 2;
	r0.z = sv_vertexid & 2;
	r0.xy = asint((float2)(uint2)r0.xz);
	o.sv_position.xy = asfloat(r0.xy) * float2(2, -2) + float2(-1, 1);
	o.texcoord = asfloat(r0.xy);
	o.sv_position.zw = float2(0, 1);

	return o;
}
