struct VS_IN
{
	uint sv_vertexid : SV_VertexID;
	uint sv_instanceid : SV_InstanceID;
};

float4 main(VS_IN i) : SV_Position
{
	return float4(2 * (float2)uint2(i.sv_vertexid & 1, i.sv_vertexid >> 1) - 1, asfloat(i.sv_instanceid | 1056964608), 1);
}
