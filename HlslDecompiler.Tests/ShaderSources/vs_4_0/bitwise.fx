uint mask;

float4 main(uint sv_vertexid : SV_VertexID) : SV_Position
{
	return (mask ^ 3) | (sv_vertexid & 7);
}
