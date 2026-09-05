uint mask;

float4 main(uint sv_vertexid : SV_VertexID) : SV_Position
{
	float4 o;

	float2 r0;
	r0.x = sv_vertexid.x & 7;
	r0.y = mask.x ^ 3;
	r0.x = r0.y | r0.x;
	o = r0.x;

	return o;
}
